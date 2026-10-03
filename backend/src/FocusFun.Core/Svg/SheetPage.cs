namespace FocusFun.Core.Svg;

using static SvgWriter;

/// <summary>Page geometry (in points) and the shared header/instruction layout used by every sheet.</summary>
public sealed class SheetPage
{
    private const double MarginPt = 12 / 25.4 * 72; // 12 mm
    private const double HeaderHeight = 64;
    private const double InstructionHeight = 56;
    private const double GapBelowInstruction = 16;

    public double Width { get; }
    public double Height { get; }
    public Box Content { get; }

    private SheetPage(double width, double height)
    {
        Width = width;
        Height = height;
        double top = MarginPt + HeaderHeight + InstructionHeight + GapBelowInstruction;
        Content = new Box(MarginPt, top, width - 2 * MarginPt, height - top - MarginPt);
    }

    public static SheetPage For(PaperSize paper) => paper switch
    {
        PaperSize.A4 => new SheetPage(595.28, 841.89),
        _ => new SheetPage(612, 792),
    };

    public string Render(SheetContent content, SheetHeader header)
    {
        var w = new SvgWriter();
        double left = MarginPt, right = Width - MarginPt, top = MarginPt;

        // Header: title + done star on the first row, name + date on the second.
        w.Text(left, top + 26, content.Title, 24, bold: true);
        w.Rect(right - 52, top, 52, 52, 2.5, radius: 8);
        w.Raw(Icons.Star(right - 26, top + 27, 19, 2));

        string name = string.IsNullOrWhiteSpace(header.ChildName) ? "Name: ____________" : $"Name: {header.ChildName}";
        string date = header.Date is { } d ? $"Date: {d:dd MMM yyyy}" : "Date: ________";
        w.Text(left, top + 52, name, 14);
        w.Text(left + 250, top + 52, date, 14);
        w.Line(left, top + HeaderHeight - 2, right, top + HeaderHeight - 2, 1.5);

        // Instruction: one sentence, large text, with an icon.
        double iy = top + HeaderHeight + InstructionHeight / 2;
        w.Raw(Icons.Pencil(left + 20, iy, 36));
        w.Text(left + 48, iy + 8, content.Instruction, 22, bold: true);

        w.Raw(content.BodySvg);
        return Wrap(w.ToString());
    }

    public string RenderAnswerKey(SheetContent content)
    {
        if (content.AnswerKeyBodySvg is null)
            throw new InvalidOperationException("This sheet has no answer key.");

        var w = new SvgWriter();
        double left = MarginPt, right = Width - MarginPt, top = MarginPt;
        w.Text(left, top + 26, $"Parent page: {content.Title}", 24, bold: true);
        w.Text(left, top + 52, "Answer key", 14);
        w.Line(left, top + HeaderHeight - 2, right, top + HeaderHeight - 2, 1.5);
        w.Raw(content.AnswerKeyBodySvg);
        return Wrap(w.ToString());
    }

    private string Wrap(string inner) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(Width)}pt\" height=\"{N(Height)}pt\" viewBox=\"0 0 {N(Width)} {N(Height)}\">" +
        $"<rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>{inner}</svg>";
}
