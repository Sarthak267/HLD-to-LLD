using System.Text.Json.Serialization;

namespace HldToLldTool.Models;

/// <summary>
/// The structured result we ask the LLM to produce: a full LLD document body
/// plus a set of named diagrams expressed as PlantUML source (so we can render
/// them ourselves deterministically instead of trusting the model to draw pixels).
/// </summary>
public sealed class LldOutput
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "Low-Level Design";

    [JsonPropertyName("overview")]
    public string Overview { get; set; } = string.Empty;

    /// <summary>Markdown body for the LLD narrative sections (components, APIs,
    /// data model, error handling, NFRs, etc). Diagrams are injected into this
    /// markdown at {{DIAGRAM:name}} placeholders by the DocumentAssembler.</summary>
    [JsonPropertyName("markdownBody")]
    public string MarkdownBody { get; set; } = string.Empty;

    [JsonPropertyName("diagrams")]
    public List<DiagramSpec> Diagrams { get; set; } = new();
}

public sealed class DiagramSpec
{
    /// <summary>Short unique key, e.g. "component-diagram". Referenced from the
    /// markdown body via a {{DIAGRAM:key}} placeholder.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>e.g. "component", "sequence", "class", "deployment"</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Full PlantUML source, starting with @startuml and ending with @enduml.</summary>
    [JsonPropertyName("plantuml")]
    public string PlantUml { get; set; } = string.Empty;
}
