namespace HldToLldTool.Services;

/// <summary>
/// Abstraction over "some free AI agent/API that can turn a prompt into text".
/// Implementations: Groq (free tier, cloud), Google Gemini (free tier, cloud),
/// Ollama (100% free, fully local, no signup, no API key).
/// </summary>
public interface ILlmProvider
{
    string Name { get; }

    /// <summary>Sends a system+user prompt and returns the raw text completion.</summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
