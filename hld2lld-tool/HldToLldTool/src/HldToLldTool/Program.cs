using HldToLldTool.Models;
using HldToLldTool.Services;

namespace HldToLldTool;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = CliOptions.Parse(args);
        if (options is null)
        {
            CliOptions.PrintUsage();
            return 1;
        }

        try
        {
            await RunAsync(options);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    private static async Task RunAsync(CliOptions options)
    {
        if (!File.Exists(options.InputPath))
            throw new FileNotFoundException($"HLD input file not found: {options.InputPath}");

        var hld = await File.ReadAllTextAsync(options.InputPath);
        Directory.CreateDirectory(options.OutputDir);
        var diagramsDir = Path.Combine(options.OutputDir, "diagrams");
        Directory.CreateDirectory(diagramsDir);

        Console.WriteLine($"Provider     : resolving...");
        var provider = LlmProviderFactory.Create(options.Provider, options.Model, options.TimeoutSeconds);
        Console.WriteLine($"Provider     : {provider.Name}");
        Console.WriteLine($"HLD input    : {options.InputPath}");
        Console.WriteLine($"Output dir   : {options.OutputDir}");
        Console.WriteLine($"Timeout      : {options.TimeoutSeconds}s");
        Console.WriteLine();
        Console.WriteLine("Asking the model to generate the LLD + diagrams...");
        Console.WriteLine("(cloud providers: usually 5-30s. Local Ollama on CPU-only hardware " +
            "can take several minutes for a large model - this is normal, not frozen.)");

        var systemPrompt = PromptBuilder.BuildSystemPrompt();
        var userPrompt = PromptBuilder.BuildUserPrompt(hld, options.ExtraInstructions);

        using var heartbeat = StartHeartbeat();
        string rawResponse;
        try
        {
            rawResponse = await provider.CompleteAsync(systemPrompt, userPrompt);
        }
        finally
        {
            heartbeat.Cancel();
        }
        await File.WriteAllTextAsync(Path.Combine(options.OutputDir, "raw-llm-response.txt"), rawResponse);

        LldOutput lld;
        try
        {
            lld = DocumentAssembler.ParseLlmResponse(rawResponse);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{ex.Message} Raw response saved to " +
                $"{Path.Combine(options.OutputDir, "raw-llm-response.txt")}", ex);
        }

        Console.WriteLine($"Got LLD '{lld.Title}' with {lld.Diagrams.Count} diagram(s). Rendering...");

        var renderer = new PlantUmlRenderer(options.PlantUmlServer);
        var nameToRelPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var diagram in lld.Diagrams)
        {
            var safeName = SanitizeFileName(diagram.Name);
            var pumlPath = Path.Combine(diagramsDir, $"{safeName}.puml");
            await File.WriteAllTextAsync(pumlPath, diagram.PlantUml);

            var imageFile = $"{safeName}.{options.ImageFormat}";
            var imagePath = Path.Combine(diagramsDir, imageFile);

            try
            {
                await renderer.RenderToFileAsync(diagram.PlantUml, imagePath, options.ImageFormat);
                nameToRelPath[diagram.Name] = $"diagrams/{imageFile}";
                Console.WriteLine($"  [ok]   {diagram.Name} -> diagrams/{imageFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [FAIL] {diagram.Name}: {ex.Message}");
                Console.WriteLine($"         Source kept at diagrams/{safeName}.puml - fix and render manually at https://www.plantuml.com/plantuml/uml/");
            }
        }

        var finalMarkdown = DocumentAssembler.BuildFinalMarkdown(lld, nameToRelPath);
        var lldPath = Path.Combine(options.OutputDir, "LLD.md");
        await File.WriteAllTextAsync(lldPath, finalMarkdown);

        Console.WriteLine();
        Console.WriteLine($"Done. LLD written to: {lldPath}");
        Console.WriteLine($"Diagram sources/images in: {diagramsDir}");
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '-');
        return name;
    }

    /// <summary>Prints a "still waiting..." line every 20s so a slow local
    /// model doesn't look like a hung process.</summary>
    private static CancellationTokenSource StartHeartbeat()
    {
        var cts = new CancellationTokenSource();
        var started = DateTime.UtcNow;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), cts.Token);
                    var elapsed = (int)(DateTime.UtcNow - started).TotalSeconds;
                    Console.WriteLine($"  ...still waiting on the model ({elapsed}s elapsed)");
                }
            }
            catch (TaskCanceledException) { /* normal on completion */ }
        }, cts.Token);
        return cts;
    }
}

internal sealed class CliOptions
{
    public string InputPath { get; init; } = string.Empty;
    public string OutputDir { get; init; } = "./output";
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public string? ExtraInstructions { get; init; }
    public string ImageFormat { get; init; } = "png";
    public string PlantUmlServer { get; init; } = "https://www.plantuml.com/plantuml";
    public int TimeoutSeconds { get; init; } = 600;

    public static CliOptions? Parse(string[] args)
    {
        string? input = null;
        string output = "./output";
        string? provider = null;
        string? model = null;
        string? extra = null;
        string imageFormat = "png";
        string plantUmlServer = "https://www.plantuml.com/plantuml";
        int timeoutSeconds = 600;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input" or "-i":
                    input = args[++i];
                    break;
                case "--output" or "-o":
                    output = args[++i];
                    break;
                case "--provider" or "-p":
                    provider = args[++i];
                    break;
                case "--model" or "-m":
                    model = args[++i];
                    break;
                case "--instructions":
                    extra = args[++i];
                    break;
                case "--format":
                    imageFormat = args[++i];
                    break;
                case "--plantuml-server":
                    plantUmlServer = args[++i];
                    break;
                case "--timeout-seconds":
                    timeoutSeconds = int.Parse(args[++i]);
                    break;
                case "--help" or "-h":
                    return null;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
            return null;

        return new CliOptions
        {
            InputPath = input,
            OutputDir = output,
            Provider = provider,
            Model = model,
            ExtraInstructions = extra,
            ImageFormat = imageFormat,
            PlantUmlServer = plantUmlServer,
            TimeoutSeconds = timeoutSeconds
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            hld2lld - generate a Low-Level Design (with diagrams) from a High-Level Design

            USAGE:
              dotnet run -- --input hld.md [options]

            REQUIRED:
              --input, -i <path>          Path to the HLD file (markdown or plain text)

            OPTIONS:
              --output, -o <dir>          Output directory (default: ./output)
              --provider, -p <name>       groq | gemini | ollama
                                           (default: auto-detect from GROQ_API_KEY /
                                            GEMINI_API_KEY env vars, else ollama)
              --model, -m <name>          Override the default model for the provider
              --instructions <text>       Extra guidance appended to the prompt
                                           e.g. "focus on the payments service only"
              --format <png|svg>          Diagram image format (default: png)
              --plantuml-server <url>     PlantUML render server
                                           (default: https://www.plantuml.com/plantuml,
                                            the free public instance; point this at your
                                            own `docker run -p 8080:8080
                                            plantuml/plantuml-server` if you'd rather not
                                            send diagram text to a third party)
              --timeout-seconds <n>       LLM request timeout, mainly for slow local
                                           Ollama models on CPU-only hardware
                                           (default: 600 = 10 minutes)

            ENV VARS (set whichever matches your chosen provider):
              GROQ_API_KEY     Free key: https://console.groq.com/keys
              GEMINI_API_KEY   Free key: https://aistudio.google.com/apikey
              (Ollama needs no key - just have it running locally: https://ollama.com)

            EXAMPLES:
              set GROQ_API_KEY=gsk_...
              dotnet run -- --input sample/sample-hld.md --output ./output

              dotnet run -- -i hld.md -p ollama -m llama3.1
            """);
    }
}
