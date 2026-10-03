using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;

namespace FocusFun.Api.Ai;

public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Which model answers AI jobs: "claude" today; our own fine-tuned model later.</summary>
    public string Provider { get; set; } = "claude";

    /// <summary>Anthropic API key. Keep it server-side only (appsettings on the API host).</summary>
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-opus-5";
    public int MaxSearches { get; set; } = 5;

    public bool IsConfigured => ApiKey.Length > 0 && !ApiKey.StartsWith('<');
}

public sealed record SearchHit(string Title, string Url);

/// <summary>One web search the model ran and what came back — kept so our own model can learn when and what to search.</summary>
public sealed record SearchStep(string Query, IReadOnlyList<SearchHit> Results);

public sealed record ModelReply(string Provider, string Model, string Text, IReadOnlyList<SearchStep> Searches, long InputTokens, long OutputTokens);

/// <summary>
/// The one seam every AI job goes through, so the provider can be swapped by config (Ai:Provider) without touching callers.
/// A self-hosted model plugs in here with its own search tool, since Claude's web search runs on Anthropic's servers.
/// </summary>
public interface ITextModel
{
    string Provider { get; }

    Task<ModelReply> AskAsync(string systemPrompt, string prompt, bool allowWebSearch, CancellationToken ct);
}

public sealed class ClaudeTextModel(IOptions<AiOptions> options) : ITextModel
{
    public string Provider => "claude";

    public async Task<ModelReply> AskAsync(string systemPrompt, string prompt, bool allowWebSearch, CancellationToken ct)
    {
        var ai = options.Value;
        if (!ai.IsConfigured)
            throw new InvalidOperationException("AI is not set up: add Ai:ApiKey to appsettings.json.");

        var client = new AnthropicClient { ApiKey = ai.ApiKey };
        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = ai.Model,
            MaxTokens = 16000,
            System = systemPrompt,
            Thinking = new BetaThinkingConfigAdaptive(),
            OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
            // A declined request is re-served by a fallback model instead of just stopping.
            Betas = ["server-side-fallback-2026-06-01"],
            Fallbacks = new List<BetaFallbackParam> { new() { Model = "claude-opus-4-8" } },
            Tools = allowWebSearch ? [new BetaToolUnion(new BetaWebSearchTool20260209 { MaxUses = ai.MaxSearches })] : [],
            Messages = [new() { Role = Role.User, Content = prompt }],
        }, ct);

        if (response.StopReason == "refusal")
            throw new InvalidOperationException("The AI declined this request. Try describing it differently.");

        // Pair each search query with its results, in the order the model ran them.
        var queries = new Dictionary<string, string>();
        var searches = new List<SearchStep>();
        var text = new System.Text.StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var t))
                text.Append(t.Text);
            else if (block.TryPickServerToolUse(out var use) && use.Input.TryGetValue("query", out var q) && q.ValueKind == JsonValueKind.String)
                queries[use.ID] = q.GetString()!;
            else if (block.TryPickWebSearchToolResult(out var result))
                searches.Add(new SearchStep(queries.GetValueOrDefault(result.ToolUseID, string.Empty), Hits(result)));
        }

        return new ModelReply(Provider, response.Model, text.ToString(), searches, response.Usage.InputTokens, response.Usage.OutputTokens);
    }

    /// <summary>Titles and URLs only; a failed search (error object instead of a list) yields no hits.</summary>
    private static List<SearchHit> Hits(BetaWebSearchToolResultBlock result) =>
        result.Content.TryPickBetaWebSearchResultBlocks(out var list)
            ? list.Select(r => new SearchHit(r.Title, r.Url)).ToList()
            : [];
}
