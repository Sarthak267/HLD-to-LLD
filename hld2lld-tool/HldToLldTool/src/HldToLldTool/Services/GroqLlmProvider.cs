using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HldToLldTool.Services;

/// <summary>
/// Groq (https://console.groq.com) offers a genuinely free API tier with fast
/// open-weight models and an OpenAI-compatible chat completions endpoint.
/// Sign up, create an API key, set GROQ_API_KEY.
/// Default model is openai/gpt-oss-120b - Groq retired its old Llama chat
/// models (llama-3.3-70b-versatile, llama-3.1-8b-instant) in August 2026 in
/// favor of the GPT-OSS models, which they report as faster and stronger.
/// If model names change again, list what's currently available with:
///   curl https://api.groq.com/openai/v1/models -H "Authorization: Bearer $GROQ_API_KEY"
/// </summary>
public sealed class GroqLlmProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public string Name => $"Groq ({_model})";

    public GroqLlmProvider(string apiKey, string model = "openai/gpt-oss-120b", HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("GROQ_API_KEY is not set. Get a free key at https://console.groq.com/keys", nameof(apiKey));

        _apiKey = apiKey;
        _model = model;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            temperature = 0.2,
            max_tokens = 8000,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            if (body.Contains("model_not_found"))
                throw new InvalidOperationException(
                    $"Groq model '{_model}' is not available on your account (it may have been " +
                    "deprecated/renamed - Groq's free-tier catalog changes over time). " +
                    "List currently available models with:\n" +
                    "  curl https://api.groq.com/openai/v1/models -H \"Authorization: Bearer $GROQ_API_KEY\"\n" +
                    $"then rerun with --model <one_of_those_ids>. Raw error: {body}");

            throw new InvalidOperationException($"Groq API error {(int)response.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
