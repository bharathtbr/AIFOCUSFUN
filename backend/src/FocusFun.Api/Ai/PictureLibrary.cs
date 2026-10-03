using System.Text.Json;
using FocusFun.Api.Data;
using FocusFun.Api.Storage;
using FocusFun.Core.Art;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FocusFun.Api.Ai;

/// <summary>
/// Hands sheets a topic's approved image pictures. Works whether or not image generation is switched on (pictures
/// already approved keep working), and never breaks a sheet: if storage fails the drawn pictures are used instead.
/// </summary>
public sealed class PictureLibrary(FocusFunDb db, IFileStorage storage, IMemoryCache cache, IOptions<ImageOptions> options, ILogger<PictureLibrary> logger)
{
    public int MinApproved => options.Value.MinApprovedForSheets;

    /// <summary>The topic's approved pictures, or null when it has too few or has image pictures switched off.</summary>
    public async Task<SheetPictures?> ForThemeAsync(string theme, CancellationToken ct)
    {
        var category = await db.Categories.Where(c => c.Key == theme).Select(c => new { c.Name, c.UseImagePictures }).SingleOrDefaultAsync(ct);
        if (category is not { UseImagePictures: true }) return null;

        var approved = await db.WordPictures
            .Where(p => p.Word!.Category!.Key == theme && p.Status == PictureStatus.Approved)
            .OrderBy(p => p.Word!.Word)
            .Select(p => new { p.Id, p.Word!.Word, p.ImageKey, p.Width, p.Height, p.ShapeJson, p.MirrorSafe })
            .ToListAsync(ct);
        if (approved.Count < MinApproved) return null;

        try
        {
            var items = new List<PictureAsset>(approved.Count);
            foreach (var p in approved)
            {
                // Approved images never change (a fix makes a new picture), so they can be cached by id.
                string base64 = (await cache.GetOrCreateAsync($"picture:{p.Id}", async entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromHours(6);
                    return Convert.ToBase64String(await storage.GetBytesAsync(p.ImageKey, ct));
                }))!;
                var shape = p.ShapeJson is null ? null : JsonSerializer.Deserialize<PictureShape>(p.ShapeJson, PictureMaker.ShapeJson);
                items.Add(new PictureAsset(p.Id.ToString("N"), p.Word, base64, p.Width, p.Height, shape, p.MirrorSafe));
            }
            return new SheetPictures(category.Name.Trim().ToLowerInvariant(), items);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Couldn't load approved pictures for {Theme}; using drawn pictures", theme);
            return null;
        }
    }
}
