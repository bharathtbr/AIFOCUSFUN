using FocusFun.Core;
using FocusFun.Core.Art;
using FocusFun.Core.Generators;

namespace FocusFun.Api.Endpoints;

/// <summary>Public, personal-data-free pages: the vehicle art and the stroke-order chart, plus a dev-only sheet preview.</summary>
public static class ReferenceEndpoints
{
    private const string SvgType = "image/svg+xml";

    public static void MapReferenceEndpoints(this IEndpointRouteBuilder api, bool isDevelopment)
    {
        api.MapGet("/sheet-types", (SheetRenderer renderer) => renderer.SupportedTypes);

        api.MapGet("/vehicles", () => Vehicles.All);

        api.MapGet("/vehicles/{vehicle}.svg", (Vehicle vehicle) =>
            Results.Content(Page(Vehicles.CanvasWidth, Vehicles.CanvasHeight,
                Vehicles.Draw(vehicle, new Box(0, 0, Vehicles.CanvasWidth, Vehicles.CanvasHeight), 5)), SvgType));

        // Every vehicle on one page, for checking the art.
        api.MapGet("/vehicles.svg", () =>
        {
            const double cellW = 300, cellH = 210;
            var cells = Vehicles.All.Select((v, i) =>
            {
                var box = new Box(i % 2 * cellW + 20, i / 2 * cellH + 10, cellW - 40, cellH - 50);
                return Vehicles.Draw(v, box, 3) +
                       $"<text x=\"{box.CenterX}\" y=\"{box.Bottom + 30}\" font-family=\"sans-serif\" font-size=\"18\" text-anchor=\"middle\">{Vehicles.Name(v)}</text>";
            });
            return Results.Content(Page(2 * cellW, Math.Ceiling(Vehicles.All.Count / 2.0) * cellH, string.Concat(cells)), SvgType);
        });

        // Stroke order for every letter and digit, for checking against the school's handwriting book.
        api.MapGet("/letters/stroke-order.svg", () =>
            Results.Content(Page(900, 1080, EnglishTracingGenerator.StrokeOrderChart(new Box(20, 20, 860, 1040))), SvgType));

        if (!isDevelopment) return;

        // Dev only: render any sheet from a URL without logging in, for checking generators in a browser.
        api.MapGet("/dev/sheets/{type}.svg", (SheetType type, Level level, string seed, PaperSize? paper, string? letters, bool? answerKey, SheetRenderer renderer) =>
        {
            var result = renderer.Render(new SheetRequest(type, level, seed, PaperSize: paper ?? PaperSize.Letter, Letters: letters), new SheetHeader(null, null));
            string? svg = answerKey == true ? result.AnswerKeySvg : result.Svg;
            return svg is null ? Results.NotFound() : Results.Content(svg, SvgType);
        });
    }

    private static string Page(double width, double height, string inner) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\"><rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>{inner}</svg>";
}
