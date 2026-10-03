using System.Globalization;
using System.Security;
using System.Text;

namespace FocusFun.Core.Svg;

/// <summary>Small helper for writing SVG markup with invariant-culture numbers.</summary>
public sealed class SvgWriter
{
    public const string FontFamily = "Andika, Lexend, 'Comic Sans MS', sans-serif";

    private readonly StringBuilder _sb = new();

    public static string N(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    public static string Escape(string text) => SecurityElement.Escape(text) ?? string.Empty;

    public SvgWriter Raw(string markup)
    {
        _sb.Append(markup);
        return this;
    }

    public SvgWriter Line(double x1, double y1, double x2, double y2, double strokeWidth, string? dash = null)
    {
        _sb.Append($"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" stroke=\"#000\" stroke-width=\"{N(strokeWidth)}\" stroke-linecap=\"round\"");
        if (dash is not null) _sb.Append($" stroke-dasharray=\"{dash}\"");
        _sb.Append("/>");
        return this;
    }

    public SvgWriter Rect(double x, double y, double w, double h, double strokeWidth, double radius = 0, string fill = "none")
    {
        _sb.Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(radius)}\" fill=\"{fill}\" stroke=\"#000\" stroke-width=\"{N(strokeWidth)}\"/>");
        return this;
    }

    public SvgWriter Circle(double cx, double cy, double r, double strokeWidth, string fill = "none")
    {
        _sb.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\" fill=\"{fill}\" stroke=\"#000\" stroke-width=\"{N(strokeWidth)}\"/>");
        return this;
    }

    public SvgWriter Path(string d, double strokeWidth, string fill = "none", string? dash = null)
    {
        _sb.Append($"<path d=\"{d}\" fill=\"{fill}\" stroke=\"#000\" stroke-width=\"{N(strokeWidth)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"");
        if (dash is not null) _sb.Append($" stroke-dasharray=\"{dash}\"");
        _sb.Append("/>");
        return this;
    }

    // TODO(phase 2): replace <text> with HarfBuzz-shaped glyph paths so Hindi/Telugu render identically on web, mobile and PDF.
    public SvgWriter Text(double x, double y, string text, double fontSize, string anchor = "start", bool bold = false)
    {
        string weight = bold ? " font-weight=\"700\"" : string.Empty;
        _sb.Append($"<text x=\"{N(x)}\" y=\"{N(y)}\" font-family=\"{FontFamily}\" font-size=\"{N(fontSize)}\" text-anchor=\"{anchor}\"{weight} fill=\"#000\">{Escape(text)}</text>");
        return this;
    }

    public override string ToString() => _sb.ToString();
}
