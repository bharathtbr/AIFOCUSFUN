using System.Text;
using FocusFun.Core.Svg;

namespace FocusFun.Core.Art;

using static SvgWriter;

/// <summary>
/// Collects shapes for picture sets drawn on a 200 x 120 canvas (ground at y = 113): outlines use the main width,
/// inner details a thinner one, and white fills keep every shape colourable.
/// </summary>
internal sealed class LineArt(double mainWidth)
{
    public const double CanvasWidth = 200;
    public const double CanvasHeight = 120;
    private const double Ground = 113;
    private readonly StringBuilder _sb = new();

    /// <summary>Draws a sketch centred in <paramref name="box"/>, keeping its aspect ratio, optionally mirrored.</summary>
    /// <param name="lineWidth">Outline thickness in page points, independent of the drawing's size.</param>
    public static string Place(Box box, double lineWidth, bool flip, Action<LineArt> sketch)
    {
        double scale = Math.Min(box.Width / CanvasWidth, box.Height / CanvasHeight);
        double x = box.CenterX - CanvasWidth * scale / 2;
        double y = box.CenterY - CanvasHeight * scale / 2;
        var art = new LineArt(lineWidth / scale);
        sketch(art);

        string mirror = flip ? $" translate({N(CanvasWidth)} 0) scale(-1 1)" : string.Empty;
        return $"<g transform=\"translate({N(x)} {N(y)}) scale({N(scale)}){mirror}\" fill=\"#fff\" stroke=\"#000\" stroke-linejoin=\"round\" stroke-linecap=\"round\">{art}</g>";
    }

    private string Width(bool detail) => N(detail ? mainWidth * 0.65 : mainWidth);

    public void Path(string d, bool detail = false, bool open = false) =>
        _sb.Append($"<path d=\"{d}\"{(open ? " fill=\"none\"" : string.Empty)} stroke-width=\"{Width(detail)}\"/>");

    public void Rect(double x, double y, double w, double h, double r = 0, bool detail = false) =>
        _sb.Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(r)}\" stroke-width=\"{Width(detail)}\"/>");

    public void Circle(double cx, double cy, double r, bool detail = false) =>
        _sb.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\" stroke-width=\"{Width(detail)}\"/>");

    public void Ellipse(double cx, double cy, double rx, double ry, double rotate = 0, bool detail = false)
    {
        string turn = rotate == 0 ? string.Empty : $" transform=\"rotate({N(rotate)} {N(cx)} {N(cy)})\"";
        _sb.Append($"<ellipse cx=\"{N(cx)}\" cy=\"{N(cy)}\" rx=\"{N(rx)}\" ry=\"{N(ry)}\"{turn} stroke-width=\"{Width(detail)}\"/>");
    }

    public void Line(double x1, double y1, double x2, double y2, bool detail = false) =>
        _sb.Append($"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" stroke-width=\"{Width(detail)}\"/>");

    /// <summary>A leg from <paramref name="top"/> (hidden under the body) down to the ground.</summary>
    public void Leg(double x, double top, double w, bool rounded = true) =>
        Rect(x, top, w, Ground - top, rounded ? w / 2 : w * 0.2);

    /// <summary>A solid black pupil with a small white shine.</summary>
    public void Eye(double cx, double cy, double r = 4)
    {
        Dot(cx, cy, r, r);
        _sb.Append($"<circle cx=\"{N(cx + r * 0.3)}\" cy=\"{N(cy - r * 0.35)}\" r=\"{N(r * 0.32)}\" stroke=\"none\"/>");
    }

    /// <summary>A small solid black spot (nose, nostril, seed).</summary>
    public void Dot(double cx, double cy, double rx, double ry) =>
        _sb.Append($"<ellipse cx=\"{N(cx)}\" cy=\"{N(cy)}\" rx=\"{N(rx)}\" ry=\"{N(ry)}\" fill=\"#000\" stroke=\"none\"/>");

    /// <summary>A closed flower-like ring of <paramref name="bumps"/> bumps around a circle (a lion's mane).</summary>
    public void Scallop(double cx, double cy, double r, int bumps, double depth)
    {
        var d = new StringBuilder();
        for (int i = 0; i <= bumps; i++)
        {
            double t = 2 * Math.PI * i / bumps;
            double px = cx + r * Math.Cos(t), py = cy + r * Math.Sin(t);
            if (i == 0) { d.Append($"M{N(px)} {N(py)}"); continue; }
            double m = 2 * Math.PI * (i - 0.5) / bumps, reach = r + depth * 2;
            d.Append($" Q{N(cx + reach * Math.Cos(m))} {N(cy + reach * Math.Sin(m))} {N(px)} {N(py)}");
        }
        Path(d.Append(" Z").ToString());
    }

    /// <summary>A leaf from its stalk end (<paramref name="x"/>, <paramref name="y"/>) out to its tip, with a centre vein.</summary>
    public void Leaf(double x, double y, double tipX, double tipY)
    {
        double mx = (x + tipX) / 2, my = (y + tipY) / 2;
        double nx = -(tipY - y) * 0.35, ny = (tipX - x) * 0.35;
        Path($"M{N(x)} {N(y)} Q{N(mx + nx)} {N(my + ny)} {N(tipX)} {N(tipY)} Q{N(mx - nx)} {N(my - ny)} {N(x)} {N(y)} Z");
        Path($"M{N(x)} {N(y)} Q{N(mx + nx * 0.2)} {N(my + ny * 0.2)} {N(tipX - (tipX - x) * 0.15)} {N(tipY - (tipY - y) * 0.15)}", detail: true, open: true);
    }

    public override string ToString() => _sb.ToString();
}
