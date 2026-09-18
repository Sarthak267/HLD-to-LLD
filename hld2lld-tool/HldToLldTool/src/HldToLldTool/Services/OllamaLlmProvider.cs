using System.Text;
using System.Text.Json;

namespace HldToLldTool.Services;

/// <summary>
/// Ollama (https://ollama.com) runs open-weight models entirely on your own
/// machine - no signup, no API key, no per-call cost, no rate limits, no data
/// leaving your laptop. Install Ollama, then "ollama pull llama3.1" (or any
/// model you like) and it serves an API on http://localhost:11434.
/// This is the most "free" of the three providers, at the cost of needing a
/// reasonably capable machine to get good quality output.
/// </summary>
public sealed class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _baseUrl;

    public string Name => $"Ollama local ({_model})";

    public OllamaLlmProvider(string model = "llama3.1", string baseUrl = "http://localhost:11434",
        int timeoutSeconds = 600, HttpClient? httpClient = null)
    {
        _model = model;
        _baseUrl = baseUrl.TrimEnd('/');
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        // Fail fast (a few seconds, not the full timeout) if Ollama itself
        // isn't reachable at all, rather than hanging until the long chat
        // timeout expires with a confusing generic message.
        await PreflightCheckAsync(ct);

        var payload = new
        {
            model = _model,
            stream = false,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            options = new { temperature = 0.2 }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync($"{_baseUrl}/api/chat", content, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama at {_baseUrl}. Is it installed and running? " +
                $"Install from https://ollama.com then run: ollama pull {_model}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Ollama did not respond within {_http.Timeout.TotalSeconds:0}s for model '{_model}'. " +
                "This almost always means the model is too large/slow for this machine's hardware " +
                "(CPU-only inference on a big model can take many minutes). Try: " +
                "(1) a smaller model, e.g. 'ollama pull llama3.1:8b' and --model llama3.1:8b, " +
                "(2) raise --timeout-seconds, or " +
                "(3) switch to --provider groq for a fast free cloud option.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama error {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private async Task PreflightCheckAsync(CancellationToken ct)
    {
        using var preflight = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var resp = await preflight.GetAsync($"{_baseUrl}/api/tags", ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Ollama responded with {(int)resp.StatusCode} at {_baseUrl}/api/tags.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama at {_baseUrl} (checked in 5s). Is it running? " +
                "Start it with 'ollama serve' or open the Ollama app, then retry. " +
                $"Install from https://ollama.com if you haven't.", ex);
        }
    }
}
