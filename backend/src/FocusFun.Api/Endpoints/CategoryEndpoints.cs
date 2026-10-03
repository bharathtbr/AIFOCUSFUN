using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FocusFun.Api.Ai;
using FocusFun.Api.Auth;
using FocusFun.Api.Data;
using FocusFun.Core.Art;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FocusFun.Api.Endpoints;

public static partial class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder api)
    {
        // For the sheet maker: active categories and whether each has pictures (picture sheets need art; word sheets work for any).
        api.MapGet("/categories", async (FocusFunDb db, IOptions<ImageOptions> images) =>
            (await Stats(db.Categories.Where(c => c.IsActive)).ToListAsync())
            .Select(s => new CategorySummary(s.Category.Key, s.Category.Name, s.WordCount, HasPictures(s, images.Value), HasImagePictures(s, images.Value))))
            .RequireAuthorization();

        var admin = api.MapGroup("/admin").RequireAuthorization(AuthEndpoints.AdminPolicy);

        admin.MapGet("/categories", async (FocusFunDb db, IOptions<ImageOptions> images) =>
            (await Stats(db.Categories).ToListAsync()).Select(s => ToAdmin(s, images.Value)));

        admin.MapPost("/categories", async (CategoryInput body, FocusFunDb db, IOptions<ImageOptions> images) =>
        {
            if (Validate(body) is { } problem) return problem;
            string key = Slug(body.Name);
            if (await db.Categories.AnyAsync(c => c.Key == key))
                return Results.Conflict(new { error = $"A category called \"{body.Name.Trim()}\" already exists." });
            var category = new Category
            {
                Key = key,
                Name = body.Name.Trim(),
                Description = Clean(body.Description),
                IsActive = body.IsActive ?? true,
                UseImagePictures = body.UseImagePictures ?? true,
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Ok(ToAdmin(new CategoryStats(category, 0, 0), images.Value));
        });

        admin.MapPut("/categories/{id:guid}", async (Guid id, CategoryInput body, FocusFunDb db, IOptions<ImageOptions> images) =>
        {
            if (Validate(body) is { } problem) return problem;
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id);
            if (category is null) return Results.NotFound();
            // The key stays fixed: saved sheets refer to it.
            category.Name = body.Name.Trim();
            category.Description = Clean(body.Description);
            category.IsActive = body.IsActive ?? category.IsActive;
            category.UseImagePictures = body.UseImagePictures ?? category.UseImagePictures;
            await db.SaveChangesAsync();
            return Results.Ok(ToAdmin(await Stats(db.Categories.Where(c => c.Id == id)).SingleAsync(), images.Value));
        });

        admin.MapGet("/categories/{id:guid}/words", async (Guid id, FocusFunDb db) =>
            await db.CategoryWords.Where(w => w.CategoryId == id).OrderBy(w => w.Word)
                .Select(w => new WordDto(w.Id, w.Word, w.Hindi, w.Telugu, w.Source, w.PictureHint))
                .ToListAsync());

        // Adds reviewed words (typed in, or accepted AI suggestions). Existing words are updated rather than duplicated.
        admin.MapPost("/categories/{id:guid}/words", async (Guid id, AddWordsInput body, FocusFunDb db) =>
        {
            var category = await db.Categories.Include(c => c.Words).SingleOrDefaultAsync(c => c.Id == id);
            if (category is null) return Results.NotFound();

            int added = 0;
            foreach (var input in body.Words)
            {
                string word = NormaliseWord(input.Word);
                if (word.Length is 0 or > 40) continue;
                var existing = category.Words.FirstOrDefault(w => w.Word == word);
                if (existing is null)
                {
                    // Added through the DbSet, not category.Words: the Id is set client-side, so EF would take a word found
                    // via the navigation for an existing row and UPDATE it (0 rows → DbUpdateConcurrencyException).
                    db.CategoryWords.Add(new CategoryWord
                    {
                        CategoryId = category.Id,
                        Word = word,
                        Hindi = Clean(input.Hindi),
                        Telugu = Clean(input.Telugu),
                        PictureHint = Hint(input.PictureHint),
                        Source = body.FromAi ? "ai" : "admin",
                    });
                    added++;
                }
                else
                {
                    existing.Hindi = Clean(input.Hindi) ?? existing.Hindi;
                    existing.Telugu = Clean(input.Telugu) ?? existing.Telugu;
                    existing.PictureHint = Hint(input.PictureHint) ?? existing.PictureHint;
                }
            }
            // Words kept from an AI suggestion (with the admin's edits) become that call's training label.
            if (body.AiCallId is { } callId && await db.AiCalls.FindAsync(callId) is { } call)
            {
                var kept = body.Words.Select(w => new WordInput(NormaliseWord(w.Word), Clean(w.Hindi), Clean(w.Telugu), Hint(w.PictureHint))).ToList();
                var earlier = call.Accepted is null ? [] : JsonSerializer.Deserialize<List<WordInput>>(call.Accepted, TrainingEndpoints.Json) ?? [];
                call.Accepted = JsonSerializer.Serialize(earlier.Concat(kept).DistinctBy(w => w.Word).ToList(), TrainingEndpoints.Json);
                call.ReviewedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { added });
        });

        admin.MapPut("/words/{id:guid}", async (Guid id, WordInput body, FocusFunDb db) =>
        {
            var word = await db.CategoryWords.FindAsync(id);
            if (word is null) return Results.NotFound();
            string text = NormaliseWord(body.Word);
            if (text.Length is 0 or > 40) return Results.ValidationProblem(new Dictionary<string, string[]> { ["word"] = ["Word is required (up to 40 characters)."] });
            if (await db.CategoryWords.AnyAsync(w => w.CategoryId == word.CategoryId && w.Id != id && w.Word == text))
                return Results.Conflict(new { error = $"\"{text}\" is already in this category." });
            word.Word = text;
            word.Hindi = Clean(body.Hindi);
            word.Telugu = Clean(body.Telugu);
            // Only changed when sent, so editing a word's spelling keeps its drawing hint.
            if (body.PictureHint is not null) word.PictureHint = Hint(body.PictureHint);
            await db.SaveChangesAsync();
            return Results.Ok(new WordDto(word.Id, word.Word, word.Hindi, word.Telugu, word.Source, word.PictureHint));
        });

        admin.MapDelete("/words/{id:guid}", async (Guid id, FocusFunDb db) =>
        {
            int removed = await db.CategoryWords.Where(w => w.Id == id).ExecuteDeleteAsync();
            return removed == 0 ? Results.NotFound() : Results.NoContent();
        });

        // Ask the AI (with web search) for new words. Nothing is saved: the admin picks which to keep.
        admin.MapPost("/categories/{id:guid}/suggest", async (Guid id, SuggestInput body, FocusFunDb db, WordFinder finder, CancellationToken ct) =>
        {
            var category = await db.Categories.Include(c => c.Words).SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return Results.NotFound();
            try
            {
                var suggestions = await finder.SuggestAsync(category.Name, category.Description, category.Words.Select(w => w.Word).ToList(),
                    Math.Clamp(body.Count ?? 15, 5, 40), body.Notes, ct);
                return Results.Ok(suggestions);
            }
            catch (InvalidOperationException e)
            {
                return Results.Problem(e.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });
    }

    private sealed record CategoryStats(Category Category, int WordCount, int ApprovedPictures);

    private static IQueryable<CategoryStats> Stats(IQueryable<Category> categories) =>
        categories.OrderBy(c => c.Name).Select(c => new CategoryStats(
            c,
            c.Words.Count,
            c.Words.SelectMany(w => w.Pictures).Count(p => p.Status == PictureStatus.Approved)));

    /// <summary>Drawn pictures in code, or enough approved image pictures that picture sheets will use them.</summary>
    private static bool HasPictures(CategoryStats s, ImageOptions images) =>
        PictureSets.Themes.Contains(s.Category.Key, StringComparer.OrdinalIgnoreCase) || HasImagePictures(s, images);

    /// <summary>Sheets will use the topic's approved AI pictures (which also unlocks dot-to-dot and colour-by-number for it).</summary>
    private static bool HasImagePictures(CategoryStats s, ImageOptions images) =>
        s.Category.UseImagePictures && s.ApprovedPictures >= images.MinApprovedForSheets;

    private static AdminCategory ToAdmin(CategoryStats s, ImageOptions images) => new(
        s.Category.Id, s.Category.Key, s.Category.Name, s.Category.Description, s.Category.IsActive, s.WordCount, HasPictures(s, images),
        PictureSets.Themes.Contains(s.Category.Key, StringComparer.OrdinalIgnoreCase), s.Category.UseImagePictures, s.ApprovedPictures);

    private static string? Hint(string? s) => Clean(s) is { } hint ? hint[..Math.Min(hint.Length, 300)] : null;

    private static IResult? Validate(CategoryInput body) =>
        string.IsNullOrWhiteSpace(body.Name) || body.Name.Trim().Length > 60 || Slug(body.Name).Length == 0
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required (up to 60 characters)."] })
            : null;

    private static string Slug(string name) => NonSlug().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');

    /// <summary>
    /// Trimmed, lowercase and in Unicode NFC, so a letter typed or pasted in different forms is stored once
    /// (the Word column compares exactly, see FocusFunDb).
    /// </summary>
    public static string NormaliseWord(string? word) => (word ?? string.Empty).Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();

    public sealed record CategorySummary(string Key, string Name, int WordCount, bool HasPictures, bool HasImagePictures);

    /// <param name="HasDrawnPictures">Pictures drawn in code (vehicles, animals, fruits).</param>
    /// <param name="UseImagePictures">Picture sheets switch to approved image pictures once there are enough.</param>
    public sealed record AdminCategory(
        Guid Id, string Key, string Name, string? Description, bool IsActive, int WordCount, bool HasPictures,
        bool HasDrawnPictures, bool UseImagePictures, int ApprovedPictures);

    public sealed record CategoryInput(string Name, string? Description, bool? IsActive, bool? UseImagePictures = null);

    public sealed record WordInput(string Word, string? Hindi, string? Telugu, string? PictureHint = null);

    public sealed record AddWordsInput(List<WordInput> Words, bool FromAi, Guid? AiCallId);

    public sealed record WordDto(Guid Id, string Word, string? Hindi, string? Telugu, string Source, string? PictureHint);

    public sealed record SuggestInput(int? Count, string? Notes);
}
