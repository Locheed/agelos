# How Local Models Work

This document explains the full flow from downloading a GGUF model to making it available to
OpenCode running inside a container.

---

## Directory layout

```
~/.agelos/
  models/                        ← shared across all projects; NOT in repo
    Qwen_Qwen3-8B-Q4_K_M.gguf
    Llama-3.2-3B-Instruct-Q4_K_M.gguf

<project>/
  .agelos/
    opencode.json                ← committed to git; per-project model/provider config

~/.config/opencode/
  config.json                    ← OpenCode's global config
                                    Agelos syncs the project file here before each run
```

---

## Step-by-step: `agelos model add`

### 1. Pick model + quantization

`ModelPrompts.PromptForModelAndQuant` presents the curated catalogue from `ModelCatalog.cs`.
Each record carries:

| Field | Description |
|-------|-------------|
| `HuggingFaceRepo` | `bartowski/<repo>-GGUF` |
| `FilePrefix` | Filename without `-{Quant}.gguf` |
| `ContextSize` | Max context tokens |
| `OutputSize` | Max output tokens |
| `Q(q4Gb)` | Q4_K_M size in GB; other quants derived by fixed ratios |

### 2. Download

`ModelDownloadService.DownloadAsync` streams the file from HuggingFace:

```
GET https://huggingface.co/{repo}/resolve/main/{fileName}
  → ~/.agelos/models/{fileName}.part   (streaming)
  → rename to .gguf on completion
```

Downloads are effectively resumable — delete the `.part` file to restart from scratch.
All models are stored in `~/.agelos/models/` and shared across all projects.

### 3. Register in project config

`OpenCodeConfigService.AddModelAsync` writes (or updates) `.agelos/opencode.json`:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "provider": {
    "llama-local": {
      "npm": "@ai-sdk/openai-compatible",
      "name": "llama-server (local)",
      "options": { "baseURL": "http://127.0.0.1:8033/v1" },
      "models": {
        "Qwen_Qwen3-8B-Q4_K_M.gguf": {
          "name": "Qwen3 8B (Q4_K_M)",
          "limit": { "context": 131072, "output": 32768 }
        }
      }
    }
  }
}
```

The file is written atomically: `write → .tmp` then `rename → opencode.json` so a crash
mid-write cannot corrupt existing config.

This file is intended to be **committed to source control** — different projects can point at
different models.

### 4. Start llama-server

`LlamaServerService.Create().RestartAsync` resolves the server in this order:

1. **Native** — `llama-server` found in PATH  
   → `KillNative` (terminates any existing process by name)  
   → `StartNative` (spawns detached background process)

2. **Containerised** — `llama-server` not in PATH, but `podman` or `docker` is available  
   → `StopContainerAsync` (stops existing `agelos-llama-server` container if running)  
   → `StartContainer`  
   ```
   podman run -d --rm \
     --name agelos-llama-server \
     -p 8033:8080 \
     -v ~/.agelos/models:/models:ro \
     ghcr.io/ggerganov/llama.cpp:server \
     --model /models/{fileName} \
     --port 8080 --host 0.0.0.0 \
     --ctx-size {contextSize} -ngl 999
   ```

3. **Neither available** — prints a manual command and exits gracefully.

After starting, `WaitForHealthAsync` polls `http://127.0.0.1:8033/health` every second for
up to 30 seconds.

---

## How the container reaches llama-server

llama-server runs on the **host** at port `8033`.
The OpenCode container connects to it via the host network.

| Container runtime | Host address inside container |
|-------------------|-------------------------------|
| Podman (rootless, `--userns=keep-id`) | `127.0.0.1` — host network is shared by default |
| Docker with bridge networking | `host.docker.internal` |
| Docker with `--network=host` | `127.0.0.1` |

The `baseURL` in `opencode.json` is `http://127.0.0.1:8033/v1` which works with Podman's
default behaviour. If you switch to Docker bridge mode, change it to
`http://host.docker.internal:8033/v1`.

---

## Config sync before `agelos run opencode`

`RunCommand.GetOpenCodeVolumes` runs this before every container start:

```csharp
File.Copy(".agelos/opencode.json", "~/.config/opencode/config.json", overwrite: true);
```

Then `~/.config/opencode/` is bind-mounted into the container at
`/home/codeuser/.config/opencode/`.

This means:
- Each project carries its own model set in `.agelos/opencode.json` (committed to git).
- The last project you ran `agelos run opencode` in becomes the active OpenCode session config.
- OpenCode inside the container sees exactly the models registered for that project.

---

## Adding a model to the curated catalogue

Edit `src/Agelos.Cli/Models/ModelCatalog.cs` and add a `new ModelInfo(...)` entry:

```csharp
new("my-model-7b", "My Model 7B",
    "bartowski/MyOrg-MyModel-7B-GGUF",   // HuggingFace repo
    "MyOrg-MyModel-7B",                   // file prefix (before -Q4_K_M.gguf)
    131072,                               // context size (tokens)
    32768,                                // output size (tokens)
    Q(4.9)),                              // Q4_K_M size in GB
```

The `Q(gb)` helper derives Q2_K, Q3_K_M, Q5_K_M, Q6_K, Q8_0 sizes automatically using
fixed ratios. Verify the actual sizes against the HuggingFace repo before committing.

All catalogue models are sourced from [bartowski](https://huggingface.co/bartowski).

