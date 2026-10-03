using System.Text.Json;
using FocusFun.Api.Ai;
using FocusFun.Api.Auth;
using FocusFun.Api.Data;
using FocusFun.Api.Storage;
using FocusFun.Core.Art;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FocusFun.Api.Endpoints;

/// <summary>
/// Admin: AI-drawn pictures for any topic's words. Optional: without an image key, drawing is off but approved pictures
/// keep working. Every picture is a draft until an admin approves it; sheets only ever use approved ones.
/// </summary>
public static class PictureEndpoints
{
    public static void MapPictureEndpoints(this IEndpointRouteBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(AuthEndpoints.AdminPolicy);

        admin.MapGet("/images", (IImageModel model, IOptions<ImageOptions> options) => new ImageSettings(
            model.IsConfigured, model.Provider, model.Models, model.DefaultModel, model.EditModel, options.Value.Versions, options.Value.MinApprovedForSheets));

        admin.MapGet("/categories/{id:guid}/pictures", async (Guid id, FocusFunDb db, IFileStorage storage, CancellationToken ct) =>
        {
            if (!await db.Categories.AnyAsync(c => c.Id == id, ct)) return Results.NotFound();
            var words = await db.CategoryWords.Where(w => w.CategoryId == id).OrderBy(w => w.Word)
                .Select(w => new { w.Id, w.Word, w.PictureHint }).ToListAsync(ct);
            var pictures = await db.WordPictures.Where(p => p.Word!.CategoryId == id && p.Status != PictureStatus.Rejected)
                .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
            decimal spent = await db.WordPictures.Where(p => p.Word!.CategoryId == id).SumAsync(p => p.CostUsd, ct);

            var byWord = new List<WordPictures>(words.Count);
            foreach (var w in words)
            {
                var list = new List<PictureDto>();
                foreach (var p in pictures.Where(p => p.WordId == w.Id)) list.Add(await ToDto(p, storage, ct));
                byWord.Add(new WordPictures(w.Id, w.Word, w.PictureHint, list));
            }
            return Results.Ok(new TopicPictures(spent, byWord));
        });

        admin.MapPost("/words/{id:guid}/pictures", async (Guid id, DrawInput body, FocusFunDb db, PictureMaker maker, IFileStorage storage, CancellationToken ct) =>
        {
            var word = await db.CategoryWords.Include(w => w.Category).SingleOrDefaultAsync(w => w.Id == id, ct);
            if (word is null) return Results.NotFound();
            return await Run(async () =>
            {
                var drawn = await maker.DrawAsync(word, body.Model, body.Count, body.MatchStyle ?? false, ct);
                var list = new List<PictureDto>();
                foreach (var p in drawn) list.Add(await ToDto(p, storage, ct));
                return list;
            });
        });

        admin.MapPost("/pictures/{id:guid}/edit", async (Guid id, EditInput body, FocusFunDb db, PictureMaker maker, IFileStorage storage, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Instruction) || body.Instruction.Length > 300)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["instruction"] = ["Say what to change (up to 300 characters)."] });
            var source = await db.WordPictures.FindAsync([id], ct);
            if (source is null) return Results.NotFound();
            return await Run(async () => await ToDto(await maker.EditAsync(source, body.Instruction, body.Model, ct), storage, ct));
        });

        admin.MapPut("/pictures/{id:guid}/status", async (Guid id, StatusInput body, FocusFunDb db, PictureMaker maker, IFileStorage storage, CancellationToken ct) =>
        {
            var picture = await db.WordPictures.FindAsync([id], ct);
            if (picture is null) return Results.NotFound();
            await maker.SetStatusAsync(picture, body.Status, ct);
            return Results.Ok(await ToDto(picture, storage, ct));
        });

        admin.MapPut("/pictures/{id:guid}/mirror-safe", async (Guid id, MirrorInput body, FocusFunDb db, IFileStorage storage, CancellationToken ct) =>
        {
            var picture = await db.WordPictures.FindAsync([id], ct);
            if (picture is null) return Results.NotFound();
            picture.MirrorSafe = body.MirrorSafe;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ToDto(picture, storage, ct));
        });

        admin.MapPut("/words/{id:guid}/picture-hint", async (Guid id, HintInput body, FocusFunDb db) =>
        {
            var word = await db.CategoryWords.FindAsync(id);
            if (word is null) return Results.NotFound();
            word.PictureHint = string.IsNullOrWhiteSpace(body.Hint) ? null : body.Hint.Trim()[..Math.Min(body.Hint.Trim().Length, 300)];
            await db.SaveChangesAsync();
            return Results.Ok(new { word.PictureHint });
        });
    }

    /// <summary>Provider and set-up problems become a readable 502/400 for the admin page instead of a server error.</summary>
    private static async Task<IResult> Run<T>(Func<Task<T>> job)
    {
        try
        {
            return Results.Ok(await job());
        }
        catch (InvalidOperationException e)
        {
            return Results.Problem(e.Message, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (HttpRequestException e)
        {
            return Results.Problem($"Couldn't reach the image service: {e.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<PictureDto> ToDto(WordPicture p, IFileStorage storage, CancellationToken ct)
    {
        var shape = p.ShapeJson is null ? null : JsonSerializer.Deserialize<PictureShape>(p.ShapeJson, PictureMaker.ShapeJson);
        return new(p.Id, p.WordId, p.Status, p.Model, p.CostUsd, p.EditedFromId, p.CreatedAt, await storage.GetDownloadUrlAsync(p.ImageKey, ct),
            p.MirrorSafe, shape?.Regions.Count ?? 0, shape is { Outline.Count: >= 8 });
    }

    public sealed record ImageSettings(
        bool Configured, string Provider, IReadOnlyList<ImageModelInfo> Models, string DefaultModel, string EditModel, int Versions, int MinApprovedForSheets);

    /// <param name="Areas">Colourable areas found (colour-by-number needs 3 or more).</param>
    /// <param name="HasOutline">An outline was traced (dot-to-dot can use it).</param>
    public sealed record PictureDto(
        Guid Id, Guid WordId, PictureStatus Status, string Model, decimal CostUsd, Guid? EditedFromId, DateTime CreatedAt, string Url,
        bool MirrorSafe, int Areas, bool HasOutline);

    public sealed record MirrorInput(bool MirrorSafe);

    public sealed record WordPictures(Guid WordId, string Word, string? PictureHint, IReadOnlyList<PictureDto> Pictures);

    public sealed record TopicPictures(decimal SpentUsd, IReadOnlyList<WordPictures> Words);

    public sealed record DrawInput(string? Model, int? Count, bool? MatchStyle);

    public sealed record EditInput(string Instruction, string? Model);

    public sealed record StatusInput(PictureStatus Status);

    public sealed record HintInput(string? Hint);
}
