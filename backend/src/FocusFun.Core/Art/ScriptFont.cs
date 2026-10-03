using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using HarfBuzzSharp;
using static FocusFun.Core.Svg.SvgWriter;
using Buffer = HarfBuzzSharp.Buffer;

namespace FocusFun.Core.Art;

/// <summary>Shaped text as outline commands in font units: y grows downwards, (MinX, MinY) is the top-left of the ink.</summary>
public sealed class TextOutline
{
    internal List<(char Command, double[] Points)> Commands { get; } = [];

    public double MinX { get; private set; } = double.MaxValue;
    public double MinY { get; private set; } = double.MaxValue;
    public double MaxX { get; private set; } = double.MinValue;
    public double MaxY { get; private set; } = double.MinValue;
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;

    internal void Add(char command, params double[] points)
    {
        for (int i = 0; i < points.Length; i += 2)
        {
            MinX = Math.Min(MinX, points[i]);
            MaxX = Math.Max(MaxX, points[i]);
            MinY = Math.Min(MinY, points[i + 1]);
            MaxY = Math.Max(MaxY, points[i + 1]);
        }
        Commands.Add((command, points));
    }

    /// <summary>SVG path data with the ink's top-left at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public string ToSvgPath(double x, double y, double scale)
    {
        var d = new StringBuilder();
        foreach (var (command, points) in Commands)
        {
            d.Append(command);
            for (int i = 0; i < points.Length; i += 2)
            {
                if (i > 0) d.Append(' ');
                d.Append(N(x + (points[i] - MinX) * scale)).Append(' ').Append(N(y + (points[i + 1] - MinY) * scale));
            }
        }
        return d.ToString();
    }
}

/// <summary>
/// An embedded Noto font that turns text (including conjuncts and vowel signs, shaped by HarfBuzz) into outlines,
/// so Hindi and Telugu letters print the same everywhere without relying on the viewer's fonts.
/// </summary>
public sealed class ScriptFont
{
    public static readonly ScriptFont Telugu = new("NotoSansTelugu-Bold.ttf");
    public static readonly ScriptFont Devanagari = new("NotoSansDevanagari-Bold.ttf");

    private readonly Blob _blob;
    private readonly Face _face;
    private readonly Font _font;
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, TextOutline?> _cache = new();

    private ScriptFont(string file)
    {
        using var stream = typeof(ScriptFont).Assembly.GetManifestResourceStream("FocusFun.Fonts." + file)
            ?? throw new InvalidOperationException($"Font {file} is not embedded.");
        _blob = Blob.FromStream(stream);
        _face = new Face(_blob, 0);
        _font = new Font(_face); // scale defaults to units per em
    }

    /// <summary>The outline of <paramref name="text"/>, or null when the font can't draw every character of it.</summary>
    public TextOutline? Outline(string text) => _cache.GetOrAdd(text, Shape);

    private TextOutline? Shape(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        lock (_lock)
        {
            using var buffer = new Buffer();
            buffer.AddUtf16(text);
            buffer.GuessSegmentProperties();
            _font.Shape(buffer);

            var outline = new TextOutline();
            var pen = new Pen(outline);
            var handle = GCHandle.Alloc(pen);
            try
            {
                var infos = buffer.GlyphInfos;
                var positions = buffer.GlyphPositions;
                double penX = 0, penY = 0;
                for (int i = 0; i < infos.Length; i++)
                {
                    if (infos[i].Codepoint == 0) return null; // .notdef: a character this font doesn't have
                    pen.X = penX + positions[i].XOffset;
                    pen.Y = penY + positions[i].YOffset;
                    Native.hb_font_draw_glyph(_font.Handle, infos[i].Codepoint, Native.DrawFuncs, GCHandle.ToIntPtr(handle));
                    penX += positions[i].XAdvance;
                    penY += positions[i].YAdvance;
                }
            }
            finally
            {
                handle.Free();
            }
            return outline.Commands.Count == 0 ? null : outline;
        }
    }

    /// <summary>Where the current glyph sits; flips font y (up) to SVG y (down).</summary>
    private sealed class Pen(TextOutline outline)
    {
        public double X, Y;

        public void Add(char command, params float[] xy)
        {
            var points = new double[xy.Length];
            for (int i = 0; i < xy.Length; i += 2)
            {
                points[i] = X + xy[i];
                points[i + 1] = -(Y + xy[i + 1]);
            }
            outline.Add(command, points);
        }

        public static Pen From(IntPtr data) => (Pen)GCHandle.FromIntPtr(data).Target!;
    }

    /// <summary>HarfBuzz's glyph drawing API, which HarfBuzzSharp doesn't wrap; the native library exports it.</summary>
    private static class Native
    {
        private const string Lib = "libHarfBuzzSharp";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PointFunc(IntPtr funcs, IntPtr data, IntPtr state, float x, float y, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QuadFunc(IntPtr funcs, IntPtr data, IntPtr state, float cx, float cy, float x, float y, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CubicFunc(IntPtr funcs, IntPtr data, IntPtr state, float c1x, float c1y, float c2x, float c2y, float x, float y, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CloseFunc(IntPtr funcs, IntPtr data, IntPtr state, IntPtr user);

        // Kept in fields so the delegates outlive the native function table.
        private static readonly PointFunc MoveTo = (_, data, _, x, y, _) => Pen.From(data).Add('M', x, y);
        private static readonly PointFunc LineTo = (_, data, _, x, y, _) => Pen.From(data).Add('L', x, y);
        private static readonly QuadFunc QuadTo = (_, data, _, cx, cy, x, y, _) => Pen.From(data).Add('Q', cx, cy, x, y);
        private static readonly CubicFunc CubicTo = (_, data, _, c1x, c1y, c2x, c2y, x, y, _) => Pen.From(data).Add('C', c1x, c1y, c2x, c2y, x, y);
        private static readonly CloseFunc Close = (_, data, _, _) => Pen.From(data).Add('Z');

        public static readonly IntPtr DrawFuncs = Create();

        private static IntPtr Create()
        {
            IntPtr funcs = hb_draw_funcs_create();
            hb_draw_funcs_set_move_to_func(funcs, Marshal.GetFunctionPointerForDelegate(MoveTo), IntPtr.Zero, IntPtr.Zero);
            hb_draw_funcs_set_line_to_func(funcs, Marshal.GetFunctionPointerForDelegate(LineTo), IntPtr.Zero, IntPtr.Zero);
            hb_draw_funcs_set_quadratic_to_func(funcs, Marshal.GetFunctionPointerForDelegate(QuadTo), IntPtr.Zero, IntPtr.Zero);
            hb_draw_funcs_set_cubic_to_func(funcs, Marshal.GetFunctionPointerForDelegate(CubicTo), IntPtr.Zero, IntPtr.Zero);
            hb_draw_funcs_set_close_path_func(funcs, Marshal.GetFunctionPointerForDelegate(Close), IntPtr.Zero, IntPtr.Zero);
            hb_draw_funcs_make_immutable(funcs);
            return funcs;
        }

        [DllImport(Lib)] private static extern IntPtr hb_draw_funcs_create();
        [DllImport(Lib)] private static extern void hb_draw_funcs_set_move_to_func(IntPtr funcs, IntPtr func, IntPtr user, IntPtr destroy);
        [DllImport(Lib)] private static extern void hb_draw_funcs_set_line_to_func(IntPtr funcs, IntPtr func, IntPtr user, IntPtr destroy);
        [DllImport(Lib)] private static extern void hb_draw_funcs_set_quadratic_to_func(IntPtr funcs, IntPtr func, IntPtr user, IntPtr destroy);
        [DllImport(Lib)] private static extern void hb_draw_funcs_set_cubic_to_func(IntPtr funcs, IntPtr func, IntPtr user, IntPtr destroy);
        [DllImport(Lib)] private static extern void hb_draw_funcs_set_close_path_func(IntPtr funcs, IntPtr func, IntPtr user, IntPtr destroy);
        [DllImport(Lib)] private static extern void hb_draw_funcs_make_immutable(IntPtr funcs);
        [DllImport(Lib)] public static extern void hb_font_draw_glyph(IntPtr font, uint glyph, IntPtr funcs, IntPtr data);
    }
}
