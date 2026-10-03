using System.Text;
using System.Text.Json;
using FocusFun.Api.Ai;
using FocusFun.Api.Auth;
using FocusFun.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusFun.Api.Endpoints;

/// <summary>
/// Training data for our own model: every reviewed AI call, exported as chat-format JSONL
/// (system, user, the web searches as tool calls with their results, then the admin-corrected answer).
/// This is the format Hugging Face TRL, Unsloth and Axolotl read for supervised fine-tuning.
/// </summary>
public static class TrainingEndpoints
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapTrainingEndpoints(this IEndpointRouteBuilder api)
    {
        var ai = api.MapGroup("/admin/ai").RequireAuthorization(AuthEndpoints.AdminPolicy);

        ai.MapGet("/stats", async (FocusFunDb db) =>
        {
            var byTask = await db.AiCalls.GroupBy(c => c.Task)
                .Select(g => new
                {
                    Task = g.Key,
                    Calls = g.Count(),
                    Reviewed = g.Count(c => c.Accepted != null),
                    InputTokens = g.Sum(c => c.InputTokens),
                    OutputTokens = g.Sum(c => c.OutputTokens),
                })
                .ToListAsync();
            return Results.Ok(byTask);
        });

        ai.MapGet("/training-data.jsonl", async (string? task, FocusFunDb db, CancellationToken ct) =>
        {
            var calls = await db.AiCalls
                .Where(c => c.Accepted != null && (task == null || c.Task == task))
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);

            var jsonl = new StringBuilder();
            foreach (var call in calls)
                jsonl.AppendLine(JsonSerializer.Serialize(ToExample(call), Json));
            return Results.File(Encoding.UTF8.GetBytes(jsonl.ToString()), "application/x-ndjson", $"focusfun-training-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        });
    }

    /// <summary>
    /// One training example. Search results carry titles and URLs only: the page text Claude read isn't returned by the API,
    /// so a self-hosted model is trained on which searches to run and what answer to give.
    /// </summary>
    private static object ToExample(AiCall call)
    {
        var messages = new List<object>
        {
            new { role = "system", content = call.SystemPrompt },
            new { role = "user", content = call.Prompt },
        };

        var searches = call.SearchTrace is null ? [] : JsonSerializer.Deserialize<List<SearchStep>>(call.SearchTrace, Json) ?? [];
        for (int i = 0; i < searches.Count; i++)
        {
            string id = $"search_{i + 1}";
            messages.Add(new
            {
                role = "assistant",
                content = string.Empty,
                tool_calls = new[] { new { id, type = "function", function = new { name = "web_search", arguments = JsonSerializer.Serialize(new { query = searches[i].Query }) } } },
            });
            messages.Add(new { role = "tool", tool_call_id = id, content = JsonSerializer.Serialize(searches[i].Results, Json) });
        }

        var suggested = JsonSerializer.Deserialize<Suggested>(call.Suggested, Json);
        var accepted = JsonSerializer.Deserialize<List<CategoryEndpoints.WordInput>>(call.Accepted!, Json) ?? [];
        var keptWords = accepted.Select(w => w.Word).ToHashSet(StringComparer.OrdinalIgnoreCase);
        messages.Add(new
        {
            role = "assistant",
            content = JsonSerializer.Serialize(new { words = accepted, sources = suggested?.Sources ?? [] }, Json),
        });

        return new
        {
            messages,
            metadata = new
            {
                id = call.Id,
                task = call.Task,
                provider = call.Provider,
                model = call.Model,
                createdAt = call.CreatedAt,
                // Words the model offered but the admin didn't keep: useful as negatives (e.g. for preference training).
                rejected = (suggested?.Words ?? []).Where(w => !keptWords.Contains(w.Word)).Select(w => w.Word).ToList(),
            },
        };
    }

    private sealed record Suggested(List<WordSuggestion> Words, List<string> Sources);
}
