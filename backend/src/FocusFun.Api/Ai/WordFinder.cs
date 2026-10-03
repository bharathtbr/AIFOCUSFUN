using System.Globalization;
using System.Text.Json;
using FocusFun.Api.Data;

namespace FocusFun.Api.Ai;

/// <param name="Picture">How to draw it, saved as the word's picture hint for the image model.</param>
public sealed record WordSuggestion(string Word, string? Hindi, string? Telugu, string? Note, string? Picture = null);

/// <param name="CallId">The logged AI call; pass it back when saving so the admin's choices become training data.</param>
public sealed record WordSuggestions(Guid CallId, IReadOnlyList<WordSuggestion> Words, IReadOnlyList<string> Sources);

/// <summary>
/// Asks the configured model (with web search) for age-appropriate words for a category, and logs the call as training data.
/// Suggestions are never saved as words here: the admin reviews them first, as the spec requires
/// (AI spelling in Hindi and Telugu can be wrong). Only the category and existing words are sent, never anything about a child.
/// </summary>
public sealed class WordFinder(ITextModel model, FocusFunDb db, ILogger<WordFinder> logger)
{
    public const string TaskName = "word-suggestions";

    public const string SystemPrompt = """
        You help build Focus Fun, printable activity sheets for children aged 6–8, many with ADHD.
        Sheets cover many categories, not just vehicles: animals, alphabets, maths, word magic (spelling games),
        fruits, colours, shapes, and any topic a parent adds. You research word lists for one category at a time.

        Use web search to check real early-years vocabulary lists (school word lists, phonics and picture-dictionary
        resources for ages 5–8) so the words are ones children this age actually know.

        Rules for every word:
        - A common, concrete, picturable noun (or the most natural word for the category), in English, lowercase.
        - Prefer short words (3–8 letters); allow up to 12 when the category needs it. Single words where possible.
        - Friendly and age-appropriate: no brands, characters, weapons, scary or violent things.
        - Not already in the existing list you're given, and no duplicates.
        - Add the Hindi word in Devanagari and the Telugu word in Telugu script, as a child would say it; leave
          either empty if unsure rather than guessing.
        - Give a short note only when useful (e.g. "easy to draw").
        - Add "picture": one short sentence on how to draw it as a simple colouring-book picture for a child, naming
          what makes it recognisable (shape, key parts, or for a pose or action, exactly what the body does), e.g.
          "child standing on one leg, other foot against the inner knee, palms together above the head".
          Only include things that are safe for a 6–8 year old to copy. Leave it empty for letters.

        Exception: when the category (its name, description or the parent's notes) is about another language's letters
        or words — e.g. "Telugu Varnamala", "Hindi Varnamala", "Telugu words" — follow that instead of the English rules:
        - Put each item in "word" in that language's own script (Telugu script, Devanagari), exactly as written,
          e.g. "అ", "క", "क्ष". For an alphabet/varnamala, list the letters in their traditional order, one letter
          (vowel or consonant, including conjuncts like క్ష / क्ष if asked) per item.
        - Also copy the item into the matching "telugu" or "hindi" field; leave the other language empty.
        - Use "note" for the English romanisation, e.g. "a", "ka", "ksha".
        - Check the letter list against a reliable source with web search.

        Reply with only a JSON object, no other text:
        {"words":[{"word":"","hindi":"","telugu":"","note":"","picture":""}],"sources":["https://..."]}
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<WordSuggestions> SuggestAsync(string category, string? description, IReadOnlyCollection<string> existing, int count, string? notes, CancellationToken ct)
    {
        string prompt = BuildPrompt(category, description, existing, count, notes);
        var reply = await model.AskAsync(SystemPrompt, prompt, allowWebSearch: true, ct);
        var (words, sources) = Parse(reply.Text, existing, count);

        var call = new AiCall
        {
            Task = TaskName,
            Provider = reply.Provider,
            Model = reply.Model,
            SystemPrompt = SystemPrompt,
            Prompt = prompt,
            SearchTrace = reply.Searches.Count == 0 ? null : JsonSerializer.Serialize(reply.Searches, Json),
            RawOutput = reply.Text,
            Suggested = JsonSerializer.Serialize(new { words, sources }, Json),
            InputTokens = reply.InputTokens,
            OutputTokens = reply.OutputTokens,
        };
        db.AiCalls.Add(call);
        await db.SaveChangesAsync(ct);

        return new WordSuggestions(call.Id, words, sources);
    }

    public static string BuildPrompt(string category, string? description, IReadOnlyCollection<string> existing, int count, string? notes) => $"""
        Category: {category}
        {(string.IsNullOrWhiteSpace(description) ? string.Empty : $"About the category: {description}")}
        Find {count} new words.
        {(string.IsNullOrWhiteSpace(notes) ? string.Empty : $"Parent's notes: {notes}")}
        Existing words (don't repeat): {(existing.Count == 0 ? "none" : string.Join(", ", existing))}
        """;

    /// <summary>Pulls the JSON object out of the reply and drops anything invalid, duplicated or already listed.</summary>
    private (List<WordSuggestion> Words, List<string> Sources) Parse(string text, IReadOnlyCollection<string> existing, int count)
    {
        int start = text.IndexOf('{'), end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            logger.LogWarning("Word finder reply had no JSON: {Reply}", text);
            throw new InvalidOperationException("The AI reply could not be read. Please try again.");
        }

        Reply? reply;
        try
        {
            reply = JsonSerializer.Deserialize<Reply>(text[start..(end + 1)], Json);
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "Word finder reply was not valid JSON: {Reply}", text);
            throw new InvalidOperationException("The AI reply could not be read. Please try again.");
        }

        var seen = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var words = (reply?.Words ?? [])
            .Select(w => w with { Word = Endpoints.CategoryEndpoints.NormaliseWord(w.Word), Hindi = Clean(w.Hindi), Telugu = Clean(w.Telugu), Note = Clean(w.Note), Picture = Clean(w.Picture) })
            .Where(w => w.Word.Length is > 0 and <= 40 && w.Word.All(IsWordChar) && seen.Add(w.Word))
            .Take(count)
            .ToList();
        var sources = (reply?.Sources ?? []).Where(s => Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme is "https" or "http").Distinct().Take(10).ToList();
        return (words, sources);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Letters plus the combining marks Telugu and Devanagari need (vowel signs, virama, anusvara, nukta),
    /// and the zero-width joiners some conjuncts use; without the marks, words like "కా" or "कि" would be dropped.
    /// </summary>
    private static bool IsWordChar(char c) =>
        char.IsLetter(c) || c is ' ' or '-' or '‌' or '‍'
        || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    private sealed record Reply(List<WordSuggestion>? Words, List<string>? Sources);
}
