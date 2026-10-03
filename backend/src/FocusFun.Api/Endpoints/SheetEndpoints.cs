using FocusFun.Api.Ai;
using FocusFun.Api.Auth;
using FocusFun.Api.Data;
using FocusFun.Api.Storage;
using FocusFun.Core;
using Microsoft.EntityFrameworkCore;

namespace FocusFun.Api.Endpoints;

public static class SheetEndpoints
{
    private const string SvgType = "image/svg+xml";

    public static void MapSheetEndpoints(this IEndpointRouteBuilder api)
    {
        var sheets = api.MapGroup("/children/{childId:guid}/sheets").RequireAuthorization();

        // Generate a sheet to preview and print. Nothing is stored: the returned spec (with its seed) can be saved later.
        sheets.MapPost("/generate", async (Guid childId, CreateSheetInput body, HttpContext http, FocusFunDb db, SheetRenderer renderer, PictureLibrary pictures, CancellationToken ct) =>
        {
            var child = await FindChild(db, childId, http, ct);
            if (child is null) return Results.NotFound();

            var request = await ToRequest(body, child, db, pictures, ct);
            var result = renderer.Render(request, new SheetHeader(child.FirstName, body.Date ?? DateOnly.FromDateTime(DateTime.UtcNow)));
            return Results.Ok(new GeneratedSheet(request, result.Title, result.Instruction, result.Svg, result.AnswerKeySvg));
        });

        // Save a sheet the parent wants to keep: re-render it from its spec (same seed ⇒ same sheet), store both pages in R2, record the spec.
        sheets.MapPost("/", async (Guid childId, CreateSheetInput body, HttpContext http, FocusFunDb db, SheetRenderer renderer, IFileStorage storage, PictureLibrary pictures, CancellationToken ct) =>
        {
            var child = await FindChild(db, childId, http, ct);
            if (child is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(body.Seed))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["seed"] = ["Save a generated sheet by passing its seed."] });

            var request = await ToRequest(body, child, db, pictures, ct);
            var result = renderer.Render(request, new SheetHeader(child.FirstName, body.Date ?? DateOnly.FromDateTime(DateTime.UtcNow)));

            var sheet = new Sheet
            {
                ChildId = child.Id,
                Type = request.Type,
                Level = request.Level,
                Theme = request.Theme,
                Letters = request.Letters,
                Words = request.Words?.ToList(),
                Seed = request.Seed,
                PaperSize = request.PaperSize,
                Title = result.Title,
                Instruction = result.Instruction,
                SvgKey = string.Empty,
            };
            sheet.SvgKey = $"children/{child.Id}/sheets/{sheet.Id}.svg";
            await storage.PutTextAsync(sheet.SvgKey, result.Svg, SvgType, ct);
            if (result.AnswerKeySvg is not null)
            {
                sheet.AnswerKeySvgKey = $"children/{child.Id}/sheets/{sheet.Id}-answer-key.svg";
                await storage.PutTextAsync(sheet.AnswerKeySvgKey, result.AnswerKeySvg, SvgType, ct);
            }

            db.Sheets.Add(sheet);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ToDto(sheet, storage, ct));
        });

        sheets.MapGet("/", async (Guid childId, bool? favourites, int? take, HttpContext http, FocusFunDb db, IFileStorage storage, CancellationToken ct) =>
        {
            if (await FindChild(db, childId, http, ct) is null) return Results.NotFound();
            var query = db.Sheets.Where(s => s.ChildId == childId);
            if (favourites == true) query = query.Where(s => s.IsFavourite);
            var rows = await query.OrderByDescending(s => s.CreatedAt).Take(Math.Clamp(take ?? 30, 1, 100)).ToListAsync(ct);

            var list = new List<SheetDto>(rows.Count);
            foreach (var row in rows) list.Add(await ToDto(row, storage, ct));
            return Results.Ok(list);
        });

        sheets.MapGet("/{id:guid}", async (Guid childId, Guid id, HttpContext http, FocusFunDb db, IFileStorage storage, CancellationToken ct) =>
        {
            var sheet = await FindSheet(db, childId, id, http, ct);
            return sheet is null ? Results.NotFound() : Results.Ok(await ToDto(sheet, storage, ct));
        });

        sheets.MapPut("/{id:guid}/favourite", async (Guid childId, Guid id, FavouriteInput body, HttpContext http, FocusFunDb db, IFileStorage storage, CancellationToken ct) =>
        {
            var sheet = await FindSheet(db, childId, id, http, ct);
            if (sheet is null) return Results.NotFound();
            sheet.IsFavourite = body.IsFavourite;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ToDto(sheet, storage, ct));
        });
    }

    /// <summary>Builds the generator request, including the chosen category's approved words and approved image pictures.</summary>
    private static async Task<SheetRequest> ToRequest(CreateSheetInput body, Child child, FocusFunDb db, PictureLibrary pictures, CancellationToken ct)
    {
        string theme = (body.Theme ?? child.Themes.FirstOrDefault() ?? "vehicles").Trim().ToLowerInvariant();
        var words = await db.CategoryWords.Where(w => w.Category!.Key == theme).OrderBy(w => w.Word).Select(w => w.Word).ToListAsync(ct);
        return new SheetRequest(
            body.Type,
            body.Level,
            string.IsNullOrWhiteSpace(body.Seed) ? SeededRandom.NewSeed() : body.Seed.Trim(),
            theme,
            body.PaperSize ?? child.PaperSize,
            string.IsNullOrWhiteSpace(body.Letters) ? null : body.Letters.Trim(),
            words.Count > 0 ? words : null,
            await pictures.ForThemeAsync(theme, ct));
    }

    private static Task<Child?> FindChild(FocusFunDb db, Guid childId, HttpContext http, CancellationToken ct)
    {
        var parentId = http.User.ParentId();
        return db.Children.SingleOrDefaultAsync(c => c.Id == childId && c.ParentId == parentId, ct);
    }

    private static Task<Sheet?> FindSheet(FocusFunDb db, Guid childId, Guid id, HttpContext http, CancellationToken ct)
    {
        var parentId = http.User.ParentId();
        return db.Sheets.SingleOrDefaultAsync(s => s.Id == id && s.ChildId == childId && s.Child!.ParentId == parentId, ct);
    }

    private static async Task<SheetDto> ToDto(Sheet s, IFileStorage storage, CancellationToken ct) => new(
        s.Id,
        s.ToRequest(),
        s.Title,
        s.Instruction,
        s.IsFavourite,
        s.CreatedAt,
        await storage.GetDownloadUrlAsync(s.SvgKey, ct),
        s.AnswerKeySvgKey is null ? null : await storage.GetDownloadUrlAsync(s.AnswerKeySvgKey, ct));

    public sealed record CreateSheetInput(SheetType Type, Level Level, string? Seed, string? Theme, PaperSize? PaperSize, string? Letters, DateOnly? Date);

    public sealed record FavouriteInput(bool IsFavourite);

    public sealed record SheetDto(Guid Id, SheetRequest Spec, string Title, string Instruction, bool IsFavourite, DateTime CreatedAt, string SvgUrl, string? AnswerKeySvgUrl);

    public sealed record GeneratedSheet(SheetRequest Spec, string Title, string Instruction, string Svg, string? AnswerKeySvg);
}
