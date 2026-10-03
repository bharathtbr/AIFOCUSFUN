namespace FocusFun.Core;

public enum SheetType
{
    RoadMaze,
    DotToDot,
    Pattern,
    EnglishTracing,
    Coloring,
    ColorByNumber,
    SpotTheDifference,
    WordSearch,
    OddOneOut,
    CountVehicles,
    RoadTracing,
    Maths,
    WordMagic,
    TeluguTracing,
    HindiTracing,
    PictureCards,
}

public enum Level
{
    Easy,
    Medium,
    Challenge,
}

public enum PaperSize
{
    Letter,
    A4,
}

/// <summary>Everything needed to (re)generate a sheet. Same request => same sheet.</summary>
/// <param name="Theme">Category the sheet is about, e.g. "vehicles" or "animals"; picture sheets use <see cref="Art.PictureSets.For"/>.</param>
/// <param name="Letters">Letters or digits to practise on tracing sheets (e.g. "B" or "ABC"); the seed picks one when empty or several.</param>
/// <param name="Words">The category's approved word list (from the admin section), for word sheets; generators fall back to built-in words when null.</param>
/// <param name="Pictures">
/// The category's approved image pictures (e.g. AI-drawn, admin-checked); picture sheets use them instead of the drawn set.
/// Loaded fresh from the topic on every render, so they're never sent to clients or saved with a sheet's spec.
/// </param>
public sealed record SheetRequest(
    SheetType Type,
    Level Level,
    string Seed,
    string Theme = "vehicles",
    PaperSize PaperSize = PaperSize.Letter,
    string? Letters = null,
    IReadOnlyList<string>? Words = null,
    [property: System.Text.Json.Serialization.JsonIgnore] Art.SheetPictures? Pictures = null);

/// <summary>Personal details printed in the page header; kept out of SheetRequest so sheet bodies can be cached and reused.</summary>
public sealed record SheetHeader(string? ChildName, DateOnly? Date);

/// <summary>What a generator draws: the task inside the content box, plus an optional answer key.</summary>
public sealed record SheetContent(string Title, string Instruction, string BodySvg, string? AnswerKeyBodySvg);

/// <summary>A finished, printable sheet.</summary>
public sealed record SheetResult(string Title, string Instruction, string Svg, string? AnswerKeySvg);

public interface ISheetGenerator
{
    SheetType Type { get; }

    /// <summary>Draws the sheet body into <paramref name="box"/>, using only <paramref name="random"/> for randomness.</summary>
    SheetContent Generate(SheetRequest request, Box box, SeededRandom random);
}

public readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}
