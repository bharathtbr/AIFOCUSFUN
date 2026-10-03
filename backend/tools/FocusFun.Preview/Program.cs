// Renders sheets to SVG (and PNG via headless Edge when available) without the API or database.
//
//   dotnet run --project tools/FocusFun.Preview -- <outDir> [type] [level] [seed] [letters] [theme] [words]
//
// With no type, renders every sheet type at every level. Pass "-" to skip letters; words are comma-separated
// (e.g. "apple,mango,banana"). A theme other than vehicles is added to the file names.
using FocusFun.Core;

if (args.Length == 0)
{
    Console.WriteLine("usage: FocusFun.Preview <outDir> [type] [level] [seed] [letters|-] [theme] [word,word,...]");
    return 1;
}

string outDir = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outDir);
var renderer = SheetRenderer.CreateDefault();

var types = args.Length > 1 ? [Enum.Parse<SheetType>(args[1], ignoreCase: true)] : renderer.SupportedTypes.ToArray();
var levels = args.Length > 2 ? [Enum.Parse<Level>(args[2], ignoreCase: true)] : Enum.GetValues<Level>();
string seed = args.Length > 3 ? args[3] : "preview";
string? letters = args.Length > 4 && args[4] != "-" ? args[4] : null;
string theme = args.Length > 5 ? args[5] : "vehicles";
string[]? words = args.Length > 6 ? args[6].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : null;
string suffix = theme.Equals("vehicles", StringComparison.OrdinalIgnoreCase) ? string.Empty : "-" + theme;

string? edge = new[]
{
    @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
}.FirstOrDefault(File.Exists);

foreach (var type in types)
    foreach (var level in levels)
    {
        var request = new SheetRequest(type, level, seed, Theme: theme, Letters: letters, Words: words);
        var result = renderer.Render(request, new SheetHeader("Aarav", DateOnly.FromDateTime(DateTime.Today)));
        Write($"{type}-{level}{suffix}", result.Svg);
        if (result.AnswerKeySvg is not null) Write($"{type}-{level}{suffix}-key", result.AnswerKeySvg);
    }

return 0;

void Write(string name, string svg)
{
    string svgPath = Path.Combine(outDir, name + ".svg");
    File.WriteAllText(svgPath, svg);
    Console.WriteLine(svgPath);
    if (edge is null) return;

    string pngPath = Path.Combine(outDir, name + ".png");
    // A private profile per render, so parallel runs don't hand off to one Edge instance and drop screenshots.
    string profile = Path.Combine(Path.GetTempPath(), "focusfun-preview-" + Guid.NewGuid().ToString("N"));
    using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(edge,
        $"--headless=new --disable-gpu --hide-scrollbars --user-data-dir=\"{profile}\" --window-size=816,1056 --screenshot=\"{pngPath}\" \"file:///{svgPath.Replace('\\', '/')}\"")
    {
        RedirectStandardError = true,
        RedirectStandardOutput = true,
    });
    process?.WaitForExit(30_000);
    try { Directory.Delete(profile, recursive: true); } catch (IOException) { }
    Console.WriteLine(pngPath);
}
