namespace FocusFun.Core.Art;

/// <summary>Something a sheet can draw and name: a vehicle, an animal, …</summary>
/// <param name="Word">Child-friendly name, lowercase (e.g. "fire truck", "elephant").</param>
/// <param name="Draw">Draws the picture centred in a box with the given outline width (points), optionally mirrored.</param>
/// <param name="Mirrorable">
/// True when a mirrored copy is clearly different (a vehicle facing the other way). Generated images can't promise that,
/// so odd one out never uses "same picture, mirrored" for them.
/// </param>
public sealed record Picture(string Word, Func<Box, double, bool, string> Draw, bool Mirrorable = true)
{
    public string Render(Box box, double lineWidth = 2.5, bool flip = false) => Draw(box, lineWidth, flip);

    public override string ToString() => Word;
}

/// <summary>A theme's pictures. Every set's art shares the vehicle style: thick black outlines, white fills, side view, 200 x 120 canvas.</summary>
public interface IPictureSet
{
    /// <summary>Theme key used in <see cref="SheetRequest.Theme"/>, e.g. "vehicles".</summary>
    string Theme { get; }

    /// <summary>Plural label for instructions, e.g. "vehicles", "animals".</summary>
    string Plural { get; }

    IReadOnlyList<Picture> All { get; }
}

public sealed class VehiclePictures : IPictureSet
{
    public string Theme => "vehicles";
    public string Plural => "vehicles";

    public IReadOnlyList<Picture> All { get; } = Vehicles.All
        .Select(v => new Picture(Vehicles.Name(v), (box, width, flip) => Vehicles.Draw(v, box, width, flip)))
        .ToList();
}

/// <summary>Finds a theme's pictures. New sets are discovered automatically; unknown themes fall back to vehicles.</summary>
public static class PictureSets
{
    private static readonly Dictionary<string, IPictureSet> Sets = typeof(PictureSets).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPictureSet).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null)
        .Select(t => (IPictureSet)Activator.CreateInstance(t)!)
        .ToDictionary(s => s.Theme, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Themes => Sets.Keys;

    public static IPictureSet For(string? theme) =>
        theme is not null && Sets.TryGetValue(theme, out var set) ? set : Sets["vehicles"];

    /// <summary>The request's approved generated pictures when it has them, otherwise the theme's drawn set.</summary>
    public static IPictureSet For(SheetRequest request) =>
        request.Pictures is { Items.Count: > 0 } pictures ? new ImagePictureSet(request.Theme, pictures) : For(request.Theme);

    /// <summary>The picture whose word matches (case-insensitive), from any set.</summary>
    public static Picture? Find(string word) =>
        Sets.Values.SelectMany(s => s.All).FirstOrDefault(p => string.Equals(p.Word, word.Trim(), StringComparison.OrdinalIgnoreCase));
}
