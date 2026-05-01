# Architecture Overview

This document explains how Agelos is structured and how the main flows work end-to-end.

---

## Component map

```
CLI entry (Program.cs)
│
├── Commands/
│   ├── RunCommand          — orchestrates the main agent-run flow
│   ├── ModelCommand        — add / list / remove GGUF models
│   ├── ListCommand         — print agents, add-ons, runtimes
│   ├── InitCommand         — write .agelos.yml from detected runtimes  [stub]
│   ├── NewCommand          — scaffold new project from template         [stub]
│   ├── AddRuntimeCommand   — append runtime to existing .agelos.yml     [stub]
│   └── PrebuildCommand     — build image without launching agent        [stub]
│
├── Core/
│   ├── RuntimeDetector       — scans project files → RuntimeRequirements
│   ├── RuntimeParser         — parses "language:version" spec strings
│   ├── ContainerBuilder      — generates Dockerfiles, calls runtime.BuildAsync
│   ├── ContainerRunner       — IContainerRuntime (PodmanRuntime / DockerRuntime)
│   │                           ContainerRuntimeDetector (podman → docker fallback)
│   └── RuntimeRequestHandler — FileSystemWatcher; handles mid-session runtime requests
│
├── Services/
│   ├── FileService             — thin FS abstraction (testable)
│   ├── ProcessService          — start processes, capture output
│   ├── ConfigService           — read/write .agelos.yml (YamlDotNet)
│   ├── OpenCodeConfigService   — read/write .agelos/opencode.json (System.Text.Json)
│   ├── ModelDownloadService    — HuggingFace HTTP download with progress bar
│   └── LlamaServerService      — start/stop llama-server (native or containerised)
│
├── Models/
│   ├── AgentZConfig        — YAML config record
│   ├── ContainerOptions    — options passed to IContainerRuntime.RunAsync / BuildAsync
│   ├── RuntimeRequirements — typed set of required runtimes for a project
│   ├── ModelInfo / QuantOption — curated model catalogue entries
│   └── ModelCatalog        — static catalogue of all curated models
│
└── Prompts/
    ├── GreenfieldPrompts   — interactive TUI when no project files exist
    └── ModelPrompts        — interactive model / quant selection TUI
```

---

## Flow 1 — `agelos run opencode`

```
RunCommand.ExecuteAsync
│
├─ 1. ContainerRuntimeDetector.DetectAsync
│      → tries podman --version, then docker --version
│      → returns PodmanRuntime or DockerRuntime
│
├─ 2. DetermineRuntimesAsync  (priority order)
│      a. --runtimes flag     → RuntimeParser.ParseSpecs
│      b. .agelos.yml exists  → ConfigService.LoadConfigAsync
│      c. project files found → RuntimeDetector.DetectAsync
│            scans: .csproj, package.json, pyproject.toml, go.mod, Cargo.toml
│      d. empty project       → GreenfieldPrompts.PromptForRuntimesAsync  (TUI)
│      e. fallback            → minimal (no runtimes)
│
├─ 3. ContainerBuilder.BuildCustomImageAsync
│      → assembles Dockerfile in-memory from runtime stanzas
│      → writes Dockerfile to a temp dir
│      → calls runtime.BuildAsync  (podman/docker build)
│      → returns deterministic image tag  (e.g. agelos/opencode-dotnet10-node22)
│      → skips if tag already exists in local image store (unless --rebuild)
│
├─ 4. RuntimeRequestHandler.StartWatchingAsync  (background Task)
│      → FileSystemWatcher on <project>/.agelos/runtime-request
│      → see Flow 3 below
│
└─ 5. runtime.RunAsync  (podman/docker run -it --rm ...)
       → mounts /workspace           — project directory
       → mounts ~/.config/opencode   — after syncing .agelos/opencode.json
       → blocks until agent exits
       → finally: StopWatching + cancels watcher task
```

---

## Flow 2 — `agelos model add`

```
ModelAddCommand.ExecuteAsync
│
├─ 1. ModelPrompts.PromptForModelAndQuant   → (ModelInfo, QuantOption)
│
├─ 2. ModelDownloadService.DownloadAsync
│      → GET https://huggingface.co/{repo}/resolve/main/{fileName}
│      → streams to ~/.agelos/models/{fileName}.part
│      → renames to .gguf on completion  (atomic, resumable)
│
├─ 3. OpenCodeConfigService.AddModelAsync
│      → reads/creates .agelos/opencode.json
│      → upserts model entry under provider.llama-local.models
│      → writes atomically via .tmp rename
│
└─ 4. LlamaServerService.Create().RestartAsync
       ├─ llama-server in PATH?  → KillNative + StartNative
       └─ else podman/docker?    → StopContainerAsync + StartContainer
                                    image: ghcr.io/ggerganov/llama.cpp:server
                                    port:  8033 → 8080
                                    mount: ~/.agelos/models/ → /models  (ro)
       └─ WaitForHealthAsync
              polls GET http://127.0.0.1:8033/health  (30 × 1 s)
```

---

## Flow 3 — Agent runtime request (mid-session)

The agent running inside the container can request a new runtime without stopping the session.
The bridge is `assets/agents/opencode/entrypoint.sh`, which wraps all tool calls via `safe_exec`.

```
Container (entrypoint.sh)                   Host (RuntimeRequestHandler)
─────────────────────────────               ────────────────────────────────
safe_exec dotnet
  dotnet not found
  → request_runtime "dotnet:10"
  → writes RUNTIME_REQUEST:dotnet:10  ──►  FileSystemWatcher fires
    to /workspace/.agelos/runtime-request   HandleRequestAsync:
  → polls until file disappears               parse spec
                                              prompt user  (y/n)
                                              update .agelos.yml
                                              ContainerBuilder.BuildCustomImageAsync
                                 ◄──          File.Delete(requestFile)
  → resumes with dotnet available             onRestart() callback
```

---

## Image tagging

Tags are deterministic, derived from the agent name and runtime set:

```
agelos/{agent}-{runtime1}{ver}-{runtime2}{ver}-...
```

| Command | Tag |
|---------|-----|
| `agelos run opencode` (dotnet:10) | `agelos/opencode-dotnet10` |
| `agelos run opencode` (dotnet:10, node:22) | `agelos/opencode-dotnet10-node22` |
| `agelos run opencode --addon llama-cpp` | `agelos/opencode-dotnet10-node22-llama-cpp` |

Same tag → build step is skipped (layer cache). `--rebuild` forces a fresh build.

---

## Volumes mounted for `agelos run opencode`

| Host path | Container path | Mode |
|-----------|---------------|------|
| `<project>/` | `/workspace` | `rw` |
| `~/.config/opencode/` | `/home/codeuser/.config/opencode` | `rw` |
| `~/.local/share/opencode/` | `/home/codeuser/.local/share/opencode` | `rw` |

`.agelos/opencode.json` is copied → `~/.config/opencode/config.json` **before** the container starts, so each project carries its own model set.

---

## Native AOT considerations

- All JSON uses `System.Text.Json` with `JsonNode` — no reflection, AOT-safe.
- YAML uses `YamlDotNet` reflection builders → `IL3050` warnings at publish. Fix: migrate to `StaticSerializerBuilder` before shipping release binary.
- No `dynamic`, `Activator.CreateInstance`, or unbound generics anywhere in the codebase.

