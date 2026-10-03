namespace FocusFun.Core.Svg;

using static SvgWriter;

/// <summary>
/// Simple thick-outline icons. Vehicles live in Art/Vehicles.cs.
/// Each icon is drawn to fit a square of <c>size</c> centred on (cx, cy).
/// </summary>
public static class Icons
{
    public static string Garage(double cx, double cy, double size)
    {
        double s = size / 100;
        double x = cx - 50 * s, y = cy - 50 * s;
        var w = new SvgWriter();
        w.Path($"M{N(x + 10 * s)} {N(y + 90 * s)} L{N(x + 10 * s)} {N(y + 40 * s)} L{N(x + 50 * s)} {N(y + 10 * s)} L{N(x + 90 * s)} {N(y + 40 * s)} L{N(x + 90 * s)} {N(y + 90 * s)} Z", 3 * s + 1);
        w.Rect(x + 28 * s, y + 52 * s, 44 * s, 38 * s, 2.5 * s + 0.5);
        for (int i = 1; i <= 3; i++)
        {
            double ly = y + 52 * s + i * 9.5 * s;
            w.Line(x + 28 * s, ly, x + 72 * s, ly, 2 * s + 0.5);
        }
        return w.ToString();
    }

    public static string Coin(double cx, double cy, double r)
    {
        var w = new SvgWriter();
        w.Circle(cx, cy, r, 2.5, "#fff");
        w.Circle(cx, cy, r * 0.62, 1.5);
        return w.ToString();
    }

    public static string Star(double cx, double cy, double outerRadius, double strokeWidth)
    {
        double inner = outerRadius * 0.45;
        var d = new System.Text.StringBuilder();
        for (int i = 0; i < 10; i++)
        {
            double r = i % 2 == 0 ? outerRadius : inner;
            double a = -Math.PI / 2 + i * Math.PI / 5;
            d.Append(i == 0 ? 'M' : 'L').Append(N(cx + r * Math.Cos(a))).Append(' ').Append(N(cy + r * Math.Sin(a))).Append(' ');
        }
        d.Append('Z');
        return new SvgWriter().Path(d.ToString(), strokeWidth).ToString();
    }

    /// <summary>A pencil, used as the instruction icon.</summary>
    public static string Pencil(double cx, double cy, double size)
    {
        double s = size / 100;
        double x = cx - 50 * s, y = cy - 50 * s;
        var w = new SvgWriter();
        w.Path($"M{N(x + 20 * s)} {N(y + 80 * s)} L{N(x + 28 * s)} {N(y + 58 * s)} L{N(x + 70 * s)} {N(y + 16 * s)} L{N(x + 84 * s)} {N(y + 30 * s)} L{N(x + 42 * s)} {N(y + 72 * s)} Z", 3 * s + 1);
        w.Line(x + 28 * s, y + 58 * s, x + 42 * s, y + 72 * s, 2.5 * s + 0.5);
        w.Line(x + 62 * s, y + 24 * s, x + 76 * s, y + 38 * s, 2.5 * s + 0.5);
        return w.ToString();
    }
}
