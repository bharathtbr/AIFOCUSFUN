using System.Text.Json;
using FocusFun.Api.Data;
using FocusFun.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FocusFun.Api.Ai;

/// <summary>
/// Draws pictures of category words with the configured image model, cleans them into printable line art, stores them
/// in R2 and records them as drafts. Only the topic and the item (never anything about a child) are sent to the model.
/// </summary>
public sealed class PictureMaker(IImageModel model, IFileStorage storage, FocusFunDb db, IOptions<ImageOptions> options, ILogger<PictureMaker> logger)
{
    /// <summary>The house style every picture is asked for, so all topics print alike.</summary>
    public const string Style = """
        Black and white colouring-book line art for young children (ages 6–8).
        One single subject, whole and centred, on a plain white background.
        Thick, smooth, even black outlines only, with large simple shapes and white inside every shape so a child can colour it in.
        No shading, no grey, no solid black areas, no hatching, no background scenery, no text, no letters, no border or frame.
        Friendly and age-appropriate. If it shows a person, draw one friendly cartoon child in simple modest clothes,
        with the whole body visible and the pose clear.
        """;

    /// <summary>How picture shapes are stored (shared with <see cref="PictureLibrary"/>, which reads them back).</summary>
    public static readonly JsonSerializerOptions ShapeJson = new(JsonSerializerDefaults.Web);

    public static string DrawPrompt(string topic, string? topicDescription, string word, string? hint, bool matchStyle) => $"""
        {Style}
        Topic: {topic}{(string.IsNullOrWhiteSpace(topicDescription) ? string.Empty : $" ({topicDescription.Trim()})")}.
        Draw: {word}.{(string.IsNullOrWhiteSpace(hint) ? string.Empty : $" {hint.Trim()}")}
        {(matchStyle ? "Match the drawing style and line thickness of the reference pictures (and the same child, if they show one), but draw only the new subject." : string.Empty)}
        """.Trim();

    public static string EditPrompt(string word, string instruction) => $"""
        Edit this colouring-book line drawing of "{word}": {instruction.Trim()}
        Keep everything else the same: thick black outlines, white inside, no shading, no grey, plain white background, no text.
        """.Trim();

    /// <summary>Draws <paramref name="count"/> versions of a word, in parallel. Returns the new drafts; fails only if none worked.</summary>
    public async Task<IReadOnlyList<WordPicture>> DrawAsync(CategoryWord word, string? modelId, int? count, bool matchStyle, CancellationToken ct)
    {
        var category = word.Category ?? await db.Categories.SingleAsync(c => c.Id == word.CategoryId, ct);
        string chosen = Resolve(modelId, matchStyle ? model.EditModel : model.DefaultModel);
        var references = matchStyle ? await StyleReferencesAsync(word, ct) : [];
        string prompt = DrawPrompt(category.Name, category.Description, word.Word, word.PictureHint, matchStyle && references.Count > 0);

        int versions = Math.Clamp(count ?? options.Value.Versions, 1, 4);
        var results = await Task.WhenAll(Enumerable.Range(0, versions).Select(_ => TryDrawAsync(chosen, prompt, references, ct)));
        var images = results.OfType<GeneratedImage>().ToList();
        if (images.Count == 0)
            throw new InvalidOperationException(results.Length > 0 ? LastError ?? "The picture couldn't be drawn." : "Nothing to draw.");

        var pictures = new List<WordPicture>();
        foreach (var image in images)
            pictures.Add(await StoreAsync(category, word, image, prompt, editedFrom: null, ct));
        await db.SaveChangesAsync(ct);
        return pictures;
    }

    /// <summary>"Fix with words": edits an existing picture (its original, uncleaned image) into a new draft.</summary>
    public async Task<WordPicture> EditAsync(WordPicture source, string instruction, string? modelId, CancellationToken ct)
    {
        var word = await db.CategoryWords.Include(w => w.Category).SingleAsync(w => w.Id == source.WordId, ct);
        string chosen = Resolve(modelId, model.EditModel);
        string prompt = EditPrompt(word.Word, instruction);
        var original = await storage.GetBytesAsync(source.OriginalKey, ct);
        var image = await model.DrawAsync(chosen, prompt, [new ReferenceImage(original, "image/png")], ct);

        var picture = await StoreAsync(word.Category!, word, image, prompt, source.Id, ct);
        await db.SaveChangesAsync(ct);
        return picture;
    }

    /// <summary>Approving keeps one approved picture per word: an earlier approved one goes back to draft.</summary>
    public async Task SetStatusAsync(WordPicture picture, PictureStatus status, CancellationToken ct)
    {
        if (status == PictureStatus.Approved)
            await db.WordPictures
                .Where(p => p.WordId == picture.WordId && p.Id != picture.Id && p.Status == PictureStatus.Approved)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PictureStatus.Draft), ct);
        picture.Status = status;
        picture.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private string? LastError { get; set; }

    private async Task<GeneratedImage?> TryDrawAsync(string modelId, string prompt, IReadOnlyList<ReferenceImage> references, CancellationToken ct)
    {
        try
        {
            return await model.DrawAsync(modelId, prompt, references, ct);
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "A picture version failed");
            LastError = e.Message;
            return null;
        }
    }

    /// <summary>Up to two approved pictures of other words in the topic, so a new picture matches them (same child, same lines).</summary>
    private async Task<List<ReferenceImage>> StyleReferencesAsync(CategoryWord word, CancellationToken ct)
    {
        var keys = await db.WordPictures
            .Where(p => p.Word!.CategoryId == word.CategoryId && p.WordId != word.Id && p.Status == PictureStatus.Approved)
            .OrderBy(p => p.ReviewedAt).Select(p => p.ImageKey).Take(2).ToListAsync(ct);
        var references = new List<ReferenceImage>();
        foreach (string key in keys) references.Add(new ReferenceImage(await storage.GetBytesAsync(key, ct), "image/png"));
        return references;
    }

    private async Task<WordPicture> StoreAsync(Category category, CategoryWord word, GeneratedImage image, string prompt, Guid? editedFrom, CancellationToken ct)
    {
        var cleaned = LineArtCleaner.Clean(image.Bytes);
        var picture = new WordPicture
        {
            WordId = word.Id,
            Provider = image.Provider,
            Model = image.Model,
            Prompt = prompt,
            EditedFromId = editedFrom,
            ImageKey = string.Empty,
            OriginalKey = string.Empty,
            Width = cleaned.Width,
            Height = cleaned.Height,
            ShapeJson = JsonSerializer.Serialize(cleaned.Shape, ShapeJson),
            CostUsd = model.Models.FirstOrDefault(m => m.Id == image.Model)?.PricePerImage ?? 0,
        };
        string folder = $"pictures/{category.Key}/{word.Id:N}/{picture.Id:N}";
        picture.ImageKey = folder + ".png";
        picture.OriginalKey = folder + "-original" + (image.MimeType == "image/jpeg" ? ".jpg" : ".png");
        await storage.PutBytesAsync(picture.OriginalKey, image.Bytes, image.MimeType, ct);
        await storage.PutBytesAsync(picture.ImageKey, cleaned.Png, "image/png", ct);
        db.WordPictures.Add(picture);
        return picture;
    }

    private string Resolve(string? modelId, string fallback) =>
        !string.IsNullOrWhiteSpace(modelId) && model.Models.Any(m => m.Id == modelId) ? modelId : fallback;
}
