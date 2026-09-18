using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HldToLldTool.Models;

namespace HldToLldTool.Services;

public static class DocumentAssembler
{
    /// <summary>
    /// LLMs occasionally wrap JSON in ```json fences or add stray prose despite
    /// instructions. Pull out the first top-level {...} object defensively.
    /// </summary>
    public static LldOutput ParseLlmResponse(string raw)
    {
        var text = raw.Trim();

        // Strip ```json ... ``` or ``` ... ``` fences if present.
        var fenceMatch = Regex.Match(text, @"```(?:json)?\s*(\{.*\})\s*```", RegexOptions.Singleline);
        if (fenceMatch.Success)
        {
            text = fenceMatch.Groups[1].Value;
        }
        else
        {
            // Fall back to the span between the first '{' and the last '}'.
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                text = text.Substring(start, end - start + 1);
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<LldOutput>(text, options);

        if (result is null || string.IsNullOrWhiteSpace(result.MarkdownBody))
            throw new InvalidOperationException(
                "Could not parse a valid LLD JSON object from the model's response. " +
                "Raw response has been saved for inspection.");

        return result;
    }

    /// <summary>
    /// Replaces {{DIAGRAM:key}} placeholders in the markdown with Markdown image
    /// links pointing at the locally rendered diagram files, and appends any
    /// diagrams whose placeholder was missing from the body at the end.
    /// </summary>
    public static string BuildFinalMarkdown(LldOutput lld, IReadOnlyDictionary<string, string> diagramNameToRelativePath)
    {
        var body = new StringBuilder();
        body.AppendLine($"# {lld.Title}");
        body.AppendLine();
        body.AppendLine(lld.Overview);
        body.AppendLine();

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var withDiagrams = Regex.Replace(lld.MarkdownBody, @"\{\{DIAGRAM:([a-zA-Z0-9_\-]+)\}\}", match =>
        {
            var key = match.Groups[1].Value;
            used.Add(key);
            var diagram = lld.Diagrams.FirstOrDefault(d => string.Equals(d.Name, key, StringComparison.OrdinalIgnoreCase));
            if (diagram is null || !diagramNameToRelativePath.TryGetValue(key, out var relPath))
                return $"> _(diagram '{key}' could not be rendered - see console output / raw diagrams folder)_";

            return $"**{diagram.Title}**\n\n![{diagram.Title}]({relPath})";
        });

        body.Append(withDiagrams);

        var missing = lld.Diagrams.Where(d => !used.Contains(d.Name)).ToList();
        if (missing.Count > 0)
        {
            body.AppendLine();
            body.AppendLine("## Additional Diagrams");
            foreach (var d in missing)
            {
                body.AppendLine();
                body.AppendLine($"**{d.Title}**");
                body.AppendLine();
                if (diagramNameToRelativePath.TryGetValue(d.Name, out var relPath))
                    body.AppendLine($"![{d.Title}]({relPath})");
                else
                    body.AppendLine($"_(diagram '{d.Name}' could not be rendered)_");
            }
        }

        return body.ToString();
    }
}
