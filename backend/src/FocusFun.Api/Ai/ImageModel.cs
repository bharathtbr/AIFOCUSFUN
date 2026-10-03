using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FocusFun.Api.Ai;

/// <summary>Image generation (optional): off until a provider key is set. Pictures are drawn only when an admin asks.</summary>
public sealed class ImageOptions
{
    public const string Section = "Images";

    /// <summary>Which service draws pictures: "google" today.</summary>
    public string Provider { get; set; } = "google";
    public GoogleImageOptions Google { get; set; } = new();

    /// <summary>How many versions to draw per item, so the admin can pick the best.</summary>
    public int Versions { get; set; } = 3;

    /// <summary>Shape requested from the model; pictures are trimmed to their drawing afterwards anyway.</summary>
    public string AspectRatio { get; set; } = "4:3";

    /// <summary>A topic's picture sheets switch to its approved images once it has at least this many.</summary>
    public int MinApprovedForSheets { get; set; } = 4;

    public bool IsConfigured => Provider == "google" && Google.IsConfigured;
}

public sealed class GoogleImageOptions
{
    /// <summary>Gemini API key (Google AI Studio, billing on). Server-side only.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model for first drafts.</summary>
    public string DefaultModel { get; set; } = "gemini-3.1-flash-lite-image";

    /// <summary>Model for "fix with words" and matching the style of approved pictures.</summary>
    public string EditModel { get; set; } = "gemini-3.1-flash-image";

    public bool IsConfigured => ApiKey.Length > 0 && !ApiKey.StartsWith('<');
}

/// <param name="PricePerImage">US$ per 1K image (standard, not batch), from ai.google.dev/gemini-api/docs/pricing (Sep 2026).</param>
public sealed record ImageModelInfo(string Id, string Label, decimal PricePerImage, string Note);

public sealed record ReferenceImage(byte[] Bytes, string MimeType);

public sealed record GeneratedImage(string Provider, string Model, byte[] Bytes, string MimeType);

/// <summary>The one seam image jobs go through, like <see cref="ITextModel"/> for text, so another provider can be added by config.</summary>
public interface IImageModel
{
    string Provider { get; }
    bool IsConfigured { get; }
    IReadOnlyList<ImageModelInfo> Models { get; }
    string DefaultModel { get; }
    string EditModel { get; }

    /// <summary>Draws one picture from <paramref name="prompt"/>; <paramref name="references"/> are images to edit or match.</summary>
    Task<GeneratedImage> DrawAsync(string model, string prompt, IReadOnlyList<ReferenceImage> references, CancellationToken ct);
}

/// <summary>Google's Nano Banana (Gemini image) models through the Gemini API's Interactions endpoint. One image per request.</summary>
public sealed class GoogleImageModel(HttpClient http, IOptions<ImageOptions> options, ILogger<GoogleImageModel> logger) : IImageModel
{
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";

    public static readonly IReadOnlyList<ImageModelInfo> Catalog =
    [
        new("gemini-3.1-flash-lite-image", "Nano Banana 2 Lite", 0.0336m, "Cheapest; good for first drafts"),
        new("gemini-3.1-flash-image", "Nano Banana 2", 0.067m, "Better detail; best for fixes and matching a style"),
        new("gemini-3-pro-image", "Nano Banana Pro", 0.134m, "Highest quality"),
    ];

    public string Provider => "google";
    public bool IsConfigured => options.Value.IsConfigured;
    public IReadOnlyList<ImageModelInfo> Models => Catalog;
    public string DefaultModel => options.Value.Google.DefaultModel;
    public string EditModel => options.Value.Google.EditModel;

    public async Task<GeneratedImage> DrawAsync(string model, string prompt, IReadOnlyList<ReferenceImage> references, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new InvalidOperationException("Image generation is off: add Images:Google:ApiKey to appsettings.json.");

        var input = new List<object> { new { type = "text", text = prompt } };
        input.AddRange(references.Select(r => new { type = "image", mime_type = r.MimeType, data = Convert.ToBase64String(r.Bytes) }));
        var body = new
        {
            model,
            input,
            response_format = new { type = "image", mime_type = "image/png", aspect_ratio = settings.AspectRatio },
            store = false,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
        request.Headers.Add("x-goog-api-key", settings.Google.ApiKey);
        using var response = await http.SendAsync(request, ct);
        string json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google image request failed ({Status}): {Body}", (int)response.StatusCode, json);
            throw new InvalidOperationException($"Google couldn't draw the picture: {ErrorMessage(json) ?? response.ReasonPhrase}");
        }
        return Parse(json, model);
    }

    /// <summary>The last image in the model's output steps; its text (e.g. a refusal) becomes the error when there is none.</summary>
    internal GeneratedImage Parse(string json, string model)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        (string Data, string Mime)? image = null;
        var text = new StringBuilder();
        if (root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            foreach (var step in steps.EnumerateArray())
            {
                if (Str(step, "type") != "model_output" || !step.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                foreach (var block in content.EnumerateArray())
                {
                    if (Str(block, "type") == "image" && Str(block, "data") is { Length: > 0 } data)
                        image = (data, Str(block, "mime_type") ?? "image/png");
                    else if (Str(block, "type") == "text")
                        text.Append(Str(block, "text")).Append(' ');
                }
            }

        if (image is null)
        {
            string reason = ErrorMessage(json) ?? (text.Length > 0 ? text.ToString().Trim() : "no picture in the reply");
            logger.LogWarning("Google image reply had no image: {Body}", json.Length > 2000 ? json[..2000] : json);
            throw new InvalidOperationException($"Google didn't return a picture: {reason}");
        }
        return new GeneratedImage(Provider, model, Convert.FromBase64String(image.Value.Data), image.Value.Mime);
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Google's error envelope ({"error":{"message"}}) or the interaction's "errors" list.</summary>
    private static string? ErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error) && Str(error, "message") is { } message) return message;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                return string.Join("; ", errors.EnumerateArray().Select(e => Str(e, "message")).OfType<string>()) is { Length: > 0 } all ? all : null;
        }
        catch (JsonException)
        {
        }
        return null;
    }
}
