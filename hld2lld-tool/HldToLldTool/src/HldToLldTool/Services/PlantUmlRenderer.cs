namespace HldToLldTool.Services;

/// <summary>
/// Renders PlantUML source to an image by calling the free, no-signup public
/// PlantUML server (plantuml.com), which also resolves the remote !include
/// URLs (C4-PlantUML, icon sprite libraries) server-side. No local Java or
/// Graphviz install required.
///
/// If you'd rather not send diagram text to a third-party server, run your own
/// PlantUML server (a single `docker run -p 8080:8080 plantuml/plantuml-server`)
/// and pass --plantuml-server http://localhost:8080.
/// </summary>
public sealed class PlantUmlRenderer
{
    private readonly HttpClient _http;
    private readonly string _serverBaseUrl;

    public PlantUmlRenderer(string serverBaseUrl = "https://www.plantuml.com/plantuml", HttpClient? httpClient = null)
    {
        _serverBaseUrl = serverBaseUrl.TrimEnd('/');
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    /// <param name="format">"png" or "svg"</param>
    public async Task<byte[]> RenderAsync(string plantUmlSource, string format = "png", CancellationToken ct = default)
    {
        var encoded = PlantUmlEncoder.Encode(plantUmlSource);
        var url = $"{_serverBaseUrl}/{format}/{encoded}";

        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"PlantUML render failed ({(int)response.StatusCode}) for URL {url}\n{errBody}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<string> RenderToFileAsync(string plantUmlSource, string outputPath, string format = "png", CancellationToken ct = default)
    {
        var bytes = await RenderAsync(plantUmlSource, format, ct);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllBytesAsync(outputPath, bytes, ct);
        return outputPath;
    }
}
