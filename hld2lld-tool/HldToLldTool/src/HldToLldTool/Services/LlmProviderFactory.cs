namespace HldToLldTool.Services;

public static class LlmProviderFactory
{
    /// <summary>
    /// providerName: "groq" | "gemini" | "ollama" (default "groq" if a GROQ_API_KEY
    /// is present, otherwise falls back to "ollama" since that needs no key at all).
    /// </summary>
    public static ILlmProvider Create(string? providerName, string? modelOverride, int ollamaTimeoutSeconds = 600)
    {
        var groqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        var name = (providerName ?? Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? Infer(groqKey, geminiKey))
            .Trim().ToLowerInvariant();

        return name switch
        {
            "groq" => new GroqLlmProvider(
                groqKey ?? throw new InvalidOperationException("Set GROQ_API_KEY (free key: https://console.groq.com/keys)"),
                modelOverride ?? "openai/gpt-oss-120b"),

            "gemini" => new GeminiLlmProvider(
                geminiKey ?? throw new InvalidOperationException("Set GEMINI_API_KEY (free key: https://aistudio.google.com/apikey)"),
                modelOverride ?? "gemini-2.0-flash"),

            "ollama" => new OllamaLlmProvider(
                modelOverride ?? "llama3.1",
                timeoutSeconds: ollamaTimeoutSeconds),

            _ => throw new InvalidOperationException($"Unknown provider '{name}'. Use groq | gemini | ollama.")
        };
    }

    private static string Infer(string? groqKey, string? geminiKey)
    {
        if (!string.IsNullOrWhiteSpace(groqKey)) return "groq";
        if (!string.IsNullOrWhiteSpace(geminiKey)) return "gemini";
        return "ollama"; // no key anywhere -> assume local Ollama
    }
}
