using System.Globalization;

namespace FocusFun.Core.Art;

/// <param name="Common">False for letters rarely taught with vowel signs (ఙ, ఞ, ఱ, ङ, ञ), left out of guninthalu/barakhadi.</param>
public sealed record IndicLetter(string Text, string Sound, bool Common = true);

/// <summary>
/// Letter data for a Brahmic script: vowels, consonants and vowel signs in traditional order, each with its sound
/// (romanised, ā = long a, ṭ/ḍ/ṇ = retroflex). Check the sets and order against the school's book; counts taught vary.
/// </summary>
public sealed class IndicScript
{
    public required string Name { get; init; }
    public required ScriptFont Font { get; init; }
    public required char BlockStart { get; init; }
    public required char BlockEnd { get; init; }
    public required char Virama { get; init; }
    public required IReadOnlyList<IndicLetter> Vowels { get; init; }
    public required IReadOnlyList<IndicLetter> Consonants { get; init; }
    public required IReadOnlyList<IndicLetter> VowelSigns { get; init; }

    /// <summary>Every common consonant with every vowel sign (క కా కి … / क का कि …).</summary>
    public IEnumerable<IndicLetter> Combinations =>
        from c in Consonants.Where(c => c.Common)
        from s in VowelSigns
        select new IndicLetter(c.Text + s.Text, Stem(c.Sound) + s.Sound);

    /// <summary>True when every character is in this script (joiners allowed), e.g. "క", "క్ష" or "బస్సు".</summary>
    public bool Covers(string text) =>
        text.Length > 0 && text.All(c => (c >= BlockStart && c <= BlockEnd) || c is '‌' or '‍');

    /// <summary>The romanised sound of a letter or consonant + vowel sign, or null for anything else (e.g. a word).</summary>
    public string? SoundOf(string text) =>
        Vowels.Concat(Consonants).Concat(Combinations).FirstOrDefault(l => l.Text == text)?.Sound;

    /// <summary>
    /// Splits typed text into letters: spaces and commas separate, otherwise each letter with its vowel signs,
    /// and a consonant ending in a virama joins the next one (క్ష, क्ष stay whole).
    /// </summary>
    public IEnumerable<string> Split(string? text)
    {
        foreach (string part in (text ?? string.Empty).Split([' ', ',', '،', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string current = string.Empty;
            var elements = StringInfo.GetTextElementEnumerator(part.Normalize(System.Text.NormalizationForm.FormC));
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                if (current.Length > 0 && !current.EndsWith(Virama) && !current.EndsWith('‍'))
                {
                    yield return current;
                    current = string.Empty;
                }
                current += element;
            }
            if (current.Length > 0) yield return current;
        }
    }

    private static string Stem(string sound) => sound.EndsWith('a') ? sound[..^1] : sound;

    private static IReadOnlyList<IndicLetter> Letters(string pairs, params string[] rare) =>
        pairs.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('='))
            .Select(p => new IndicLetter(p[0], p[1], !rare.Contains(p[0])))
            .ToList();

    public static readonly IndicScript Telugu = new()
    {
        Name = "Telugu",
        Font = ScriptFont.Telugu,
        BlockStart = 'ఀ',
        BlockEnd = '౿',
        Virama = '్',
        // అచ్చులు
        Vowels = Letters("అ=a ఆ=ā ఇ=i ఈ=ī ఉ=u ఊ=ū ఋ=ru ౠ=rū ఎ=e ఏ=ē ఐ=ai ఒ=o ఓ=ō ఔ=au అం=aṁ అః=aḥ"),
        // హల్లులు
        Consonants = Letters(
            "క=ka ఖ=kha గ=ga ఘ=gha ఙ=ṅa చ=cha ఛ=chha జ=ja ఝ=jha ఞ=ña ట=ṭa ఠ=ṭha డ=ḍa ఢ=ḍha ణ=ṇa " +
            "త=ta థ=tha ద=da ధ=dha న=na ప=pa ఫ=pha బ=ba భ=bha మ=ma య=ya ర=ra ల=la వ=va శ=śa ష=ṣa స=sa హ=ha " +
            "ళ=ḷa క్ష=kṣa ఱ=ṟa",
            "ఙ", "ఞ", "ఱ", "క్ష"),
        // గుణింతం signs, in the order of క కా కి కీ కు కూ కృ కె కే కై కొ కో కౌ కం కః
        VowelSigns = Letters("ా=ā ి=i ీ=ī ు=u ూ=ū ృ=ru ె=e ే=ē ై=ai ొ=o ో=ō ౌ=au ం=aṁ ః=aḥ"),
    };

    public static readonly IndicScript Hindi = new()
    {
        Name = "Hindi",
        Font = ScriptFont.Devanagari,
        BlockStart = 'ऀ',
        BlockEnd = 'ॿ',
        Virama = '्',
        // स्वर
        Vowels = Letters("अ=a आ=ā इ=i ई=ī उ=u ऊ=ū ऋ=ri ए=e ऐ=ai ओ=o औ=au अं=aṁ अः=aḥ"),
        // व्यंजन
        Consonants = Letters(
            "क=ka ख=kha ग=ga घ=gha ङ=ṅa च=cha छ=chha ज=ja झ=jha ञ=ña ट=ṭa ठ=ṭha ड=ḍa ढ=ḍha ण=ṇa " +
            "त=ta थ=tha द=da ध=dha न=na प=pa फ=pha ब=ba भ=bha म=ma य=ya र=ra ल=la व=va श=śa ष=ṣa स=sa ह=ha " +
            "क्ष=kṣa त्र=tra ज्ञ=gya",
            "ङ", "ञ", "क्ष", "त्र", "ज्ञ"),
        // बारहखड़ी signs, in the order of क का कि की कु कू कृ के कै को कौ कं कः
        VowelSigns = Letters("ा=ā ि=i ी=ī ु=u ू=ū ृ=ri े=e ै=ai ो=o ौ=au ं=aṁ ः=aḥ"),
    };
}
