using FocusFun.Core.Art;
using FocusFun.Core.Generators;

namespace FocusFun.Core.Tests;

public class IndicTracingTests
{
    private readonly SheetRenderer _renderer = SheetRenderer.CreateDefault();

    public static TheoryData<string> Scripts() => ["Telugu", "Hindi"];

    private static IndicScript ScriptFor(string name) => name == "Telugu" ? IndicScript.Telugu : IndicScript.Hindi;

    [Theory]
    [MemberData(nameof(Scripts))]
    public void Font_draws_every_letter_and_combination(string name)
    {
        var script = ScriptFor(name);
        var all = script.Vowels.Concat(script.Consonants).Concat(script.Combinations).ToList();
        Assert.All(all, l => Assert.NotNull(script.Font.Outline(l.Text)));
    }

    [Theory]
    [InlineData(SheetType.TeluguTracing, "క్ష", "క్ష")]
    [InlineData(SheetType.TeluguTracing, "కి", "కి")]
    [InlineData(SheetType.HindiTracing, "क्ष", "क्ष")]
    [InlineData(SheetType.HindiTracing, "B క की", "की")] // other scripts' letters are skipped
    public void Tracing_uses_the_typed_letter(SheetType type, string letters, string expected)
    {
        var result = _renderer.Render(new SheetRequest(type, Level.Easy, "t", Letters: letters), new SheetHeader(null, null));
        Assert.Equal($"Let's Write {expected}", result.Title);
    }

    [Fact]
    public void Tracing_uses_the_topics_words_in_that_script()
    {
        string[] words = ["bus", "అ", "ఆ", "क"];
        for (int i = 0; i < 20; i++)
        {
            var result = _renderer.Render(new SheetRequest(SheetType.TeluguTracing, Level.Challenge, $"w{i}", Theme: "telugu-varnamala", Words: words), new SheetHeader(null, null));
            Assert.Contains(result.Title, new[] { "Let's Write అ", "Let's Write ఆ" });
        }
    }

    [Theory]
    [InlineData(Level.Easy)]
    [InlineData(Level.Medium)]
    [InlineData(Level.Challenge)]
    public void Level_picks_vowels_consonants_then_combinations(Level level)
    {
        var script = IndicScript.Telugu;
        var expected = (level switch { Level.Easy => script.Vowels, Level.Medium => script.Consonants, _ => script.Combinations.ToList() })
            .Select(l => l.Text).ToHashSet();
        var generator = new TeluguTracingGenerator();
        for (int i = 0; i < 20; i++)
            Assert.Contains(generator.PickLetter(new SheetRequest(SheetType.TeluguTracing, level, $"l{i}"), new SeededRandom($"l{i}")), expected);
    }

    [Fact]
    public void Split_keeps_conjuncts_and_vowel_signs_together()
    {
        Assert.Equal(["క్ష", "కా", "అం"], IndicScript.Telugu.Split("క్షకాఅం"));
        Assert.Equal(["क्ष", "कि"], IndicScript.Hindi.Split("क्ष, कि"));
    }

    [Fact]
    public void Combinations_have_sounds()
    {
        Assert.Equal("kā", IndicScript.Telugu.SoundOf("కా"));
        Assert.Equal("ki", IndicScript.Hindi.SoundOf("कि"));
        Assert.Null(IndicScript.Telugu.SoundOf("బస్సు"));
    }
}
