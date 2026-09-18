using System.Text;
using System.Text.Json;

namespace HldToLldTool.Services;

/// <summary>
/// Google AI Studio (https://aistudio.google.com/apikey) issues free API keys
/// for Gemini models with a generous free-tier request quota. No credit card
/// required for the free tier as of this writing.
/// </summary>
public sealed class GeminiLlmProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public string Name => $"Gemini ({_model})";

    public GeminiLlmProvider(string apiKey, string model = "gemini-2.0-flash", HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("GEMINI_API_KEY is not set. Get a free key at https://aistudio.google.com/apikey", nameof(apiKey));

        _apiKey = apiKey;
        _model = model;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 8000 }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini API error {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }
}
