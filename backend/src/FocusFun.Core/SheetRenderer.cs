using FocusFun.Core.Art;
using FocusFun.Core.Svg;

namespace FocusFun.Core;

/// <summary>Picks the generator for a sheet type and lays its output onto a printable page.</summary>
public sealed class SheetRenderer
{
    private readonly Dictionary<SheetType, ISheetGenerator> _generators;

    public SheetRenderer(IEnumerable<ISheetGenerator> generators)
    {
        _generators = generators.ToDictionary(g => g.Type);
    }

    /// <summary>A renderer with every generator in FocusFun.Core, so a new sheet type only needs its generator class.</summary>
    public static SheetRenderer CreateDefault() => new(
        typeof(SheetRenderer).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ISheetGenerator).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (ISheetGenerator)Activator.CreateInstance(t)!));

    public IReadOnlyCollection<SheetType> SupportedTypes => _generators.Keys;

    /// <summary>The personal-data-free part of a sheet: fully determined by the request, so safe to cache and share.</summary>
    public SheetContent Generate(SheetRequest request)
    {
        if (!_generators.TryGetValue(request.Type, out var generator))
            throw new NotSupportedException($"No generator registered for {request.Type}.");

        var page = SheetPage.For(request.PaperSize);
        return generator.Generate(request, page.Content, new SeededRandom(request.Seed));
    }

    /// <summary>Adds the header (child's name, date, done star) and wraps the content into printable pages.</summary>
    public SheetResult Compose(SheetRequest request, SheetContent content, SheetHeader header)
    {
        var page = SheetPage.For(request.PaperSize);
        string? answerKey = content.AnswerKeyBodySvg is null ? null : ImagePictureSet.Embed(page.RenderAnswerKey(content), request.Pictures);
        return new SheetResult(content.Title, content.Instruction, ImagePictureSet.Embed(page.Render(content, header), request.Pictures), answerKey);
    }

    public SheetResult Render(SheetRequest request, SheetHeader header) => Compose(request, Generate(request), header);
}
