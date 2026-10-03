using FocusFun.Core.Art;

namespace FocusFun.Core.Generators;

/// <summary>Theme-aware wording and lookups shared by the picture sheets.</summary>
internal static class ThemeText
{
    /// <summary>The theme's pictures, falling back to vehicles for a set too small to build a puzzle from.</summary>
    public static IPictureSet Pictures(SheetRequest request, int atLeast = 3)
    {
        var set = PictureSets.For(request);
        if (set.All.Count >= atLeast) return set;
        // Too few approved images: the drawn set for the theme, then vehicles.
        set = PictureSets.For(request.Theme);
        return set.All.Count >= atLeast ? set : PictureSets.For("vehicles");
    }

    /// <summary>
    /// The topic's own pictures (drawn animals/fruits or approved images) for sheets that otherwise draw vehicle scenes,
    /// or null for vehicles and unknown topics, which keep their vehicle scenes.
    /// </summary>
    public static IPictureSet? TopicPictures(SheetRequest request, int atLeast = 1)
    {
        var set = PictureSets.For(request);
        if (set.All.Count < atLeast) set = PictureSets.For(request.Theme);
        return IsVehicles(set) || set.All.Count < atLeast ? null : set;
    }

    /// <summary>The request's approved image pictures that have shape data, for dot-to-dot and colour-by-number.</summary>
    public static IReadOnlyList<PictureAsset> ShapedImages(SheetRequest request, Func<PictureShape, bool> usable) =>
        PictureSets.For(request) is ImagePictureSet set
            ? set.Assets.Where(a => a.Shape is { } s && usable(s)).ToList()
            : [];

    /// <summary>"vehicles" → "vehicle", "butterflies" → "butterfly"; anything else is returned as is.</summary>
    public static string Singular(IPictureSet set)
    {
        string plural = set.Plural.Trim();
        if (plural.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && plural.Length > 3) return plural[..^3] + "y";
        if (plural.EndsWith('s') && !plural.EndsWith("ss", StringComparison.OrdinalIgnoreCase)) return plural[..^1];
        return plural;
    }

    public static string Capitalize(string text) =>
        string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    /// <summary>The vehicle a picture from the vehicle set shows, or null for any other picture.</summary>
    public static Vehicle? AsVehicle(Picture picture)
    {
        var vehicles = PictureSets.For("vehicles").All;
        for (int i = 0; i < vehicles.Count; i++)
            if (ReferenceEquals(vehicles[i], picture)) return Vehicles.All[i];
        return null;
    }

    /// <summary>The drawn vehicle set, whose look-alike and mirroring rules are known; not image pictures of the vehicles topic.</summary>
    public static bool IsVehicles(IPictureSet set) => set is VehiclePictures;
}
