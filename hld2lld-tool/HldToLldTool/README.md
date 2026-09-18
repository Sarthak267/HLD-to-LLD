# hld2lld

A C# console tool that takes a **High-Level Design (HLD)** document and produces
a **Low-Level Design (LLD)** — component breakdowns, API contracts, data
models, sequence flows, deployment topology — plus **rendered diagrams that
use real tool/service icons** (AWS, Azure, Kubernetes, etc. via C4-PlantUML),
using a **free AI provider** of your choice.

No paid API required, and no external NuGet packages — it only uses the
.NET base class library (`HttpClient`, `System.Text.Json`,
`System.IO.Compression`), so `dotnet build` works even offline/behind
restrictive proxies (only outbound internet is needed at *run* time, to call
the LLM and the diagram renderer).

## How it works

1. You give it an HLD file (markdown or plain text).
2. It sends a carefully engineered prompt to a free LLM provider, asking for
   a structured JSON response: an LLD written in Markdown, plus one or more
   diagrams expressed as **PlantUML source** (not raw pixels — this makes the
   diagrams deterministic and editable).
3. It renders each PlantUML diagram to PNG/SVG using the free public
   PlantUML server (or your own self-hosted one), which also resolves the
   `!include` references to icon libraries (AWS/Azure/K8s/office icons via
   C4-PlantUML + plantuml-stdlib) so components render with recognizable
   tool icons instead of generic boxes.
4. It stitches the Markdown and diagram images together into `LLD.md`.

## Free AI providers supported

| Provider | Cost | Setup |
|---|---|---|
| **Groq** (recommended) | Free tier, fast `openai/gpt-oss-120b` | Sign up at https://console.groq.com/keys, `set GROQ_API_KEY=...` |
| **Google Gemini** | Free tier | Get a key at https://aistudio.google.com/apikey, `set GEMINI_API_KEY=...` |
| **Ollama** | 100% free, fully local, no signup, no key | Install https://ollama.com, then `ollama pull llama3.1` |

Note: Groq's free-tier model catalog changes over time (they retired the old
`llama-3.3-70b-versatile`/`llama-3.1-8b-instant` models in August 2026 in
favor of `openai/gpt-oss-120b`/`openai/gpt-oss-20b`). If you ever get a
`model_not_found` error, list what's currently available and pass one via
`--model`:
```bash
curl https://api.groq.com/openai/v1/models -H "Authorization: Bearer $GROQ_API_KEY"
```

The tool auto-detects which one to use: if `GROQ_API_KEY` is set it uses
Groq; else if `GEMINI_API_KEY` is set it uses Gemini; else it assumes a
local Ollama instance. You can also force it with `--provider`.

Diagram rendering uses the free public PlantUML server by default
(`https://www.plantuml.com/plantuml`) — no signup needed. If you don't want
diagram text leaving your machine, run
`docker run -d -p 8080:8080 plantuml/plantuml-server` and pass
`--plantuml-server http://localhost:8080`.

## Build & run

```bash
cd src/HldToLldTool
dotnet build

# Option A: Groq (fast, good quality, free tier)
export GROQ_API_KEY=gsk_your_key_here      # Windows: set GROQ_API_KEY=...
dotnet run -- --input ../../sample/sample-hld.md --output ../../output

# Option B: Google Gemini
export GEMINI_API_KEY=your_key_here
dotnet run -- --input ../../sample/sample-hld.md --provider gemini

# Option C: fully local, zero signup, via Ollama
ollama pull llama3.1
dotnet run -- --input ../../sample/sample-hld.md --provider ollama --model llama3.1
```

Output:
```
output/
  LLD.md                    <- the generated Low-Level Design, with embedded diagram images
  raw-llm-response.txt      <- raw model output, kept for debugging/auditing
  diagrams/
    component-diagram.puml  <- editable PlantUML source
    component-diagram.png   <- rendered image
    deployment-diagram.puml / .png
    order-flow-sequence.puml / .png
    ...
```

Open `output/LLD.md` in any Markdown viewer (VS Code preview, Obsidian,
GitHub, etc.) to see the document with diagrams inline. The `.puml` files
are kept alongside the images so you can tweak a diagram by hand and
re-render it yourself at https://www.plantuml.com/plantuml/uml/ if needed.

## CLI reference

```
hld2lld --input <path> [options]

REQUIRED:
  --input, -i <path>          Path to the HLD file (markdown or plain text)

OPTIONS:
  --output, -o <dir>          Output directory (default: ./output)
  --provider, -p <name>       groq | gemini | ollama
  --model, -m <name>          Override the default model for the provider
  --instructions <text>       Extra guidance appended to the prompt,
                               e.g. "focus on the payments service only"
  --format <png|svg>          Diagram image format (default: png)
  --plantuml-server <url>     PlantUML render server (default: public instance)
```

## Notes & known limitations

- **Quality depends on the model.** Groq's Llama 3.3 70B and Gemini 2.0
  Flash both produce solid, detailed LLDs. Small local Ollama models (e.g.
  under 8B params) may produce thinner content or occasionally malformed
  JSON/PlantUML — the tool saves `raw-llm-response.txt` and each `.puml`
  file so you can inspect/fix by hand if a diagram fails to render.
- **Icon coverage**: icons come from the `plantuml-stdlib` C4-PlantUML
  library (AWS, Azure, GCP-via-generic, Kubernetes, Office/generic server
  icons). If the HLD doesn't name a cloud vendor, the model is instructed to
  fall back to plain C4 boxes rather than guessing a vendor.
- **Rate limits**: free tiers are rate-limited (Groq/Gemini) — if you hit a
  429, wait a bit or lower `max_tokens`/diagram count via `--instructions`.
- **Privacy**: HLD text is sent to whichever provider you choose. Use Ollama
  if the HLD is sensitive and must stay local. Diagram *source* (PlantUML
  text, not your prose) is sent to the PlantUML render server — self-host it
  if that's a concern too.
