# AgentZ Implementation Plan

## Status: COMPLETE

---

## Phase 1: Foundation
- [x] `AgentZ.slnx` — solution file (.slnx format)
- [x] `src/AgentZ.Cli/AgentZ.Cli.csproj` — Native AOT, ImplicitUsings, System.CommandLine, Spectre.Console, YamlDotNet
- [x] `src/AgentZ.Cli/Program.cs` — entry point, register all commands
- [x] `src/AgentZ.Cli/Models/ContainerOptions.cs` — ContainerOptions, VolumeMount, PortMapping, BuildOptions records
- [x] `src/AgentZ.Cli/Services/ProcessService.cs` — IProcessService, ProcessService, ProcessResult
- [x] `src/AgentZ.Cli/Core/ContainerRunner.cs` — IContainerRuntime, PodmanRuntime, DockerRuntime, ContainerRuntimeDetector

## Phase 2: Runtime Detection
- [x] `src/AgentZ.Cli/Models/RuntimeRequirements.cs` — RuntimeRequirements, CustomRuntime, RuntimeSpec records
- [x] `src/AgentZ.Cli/Services/FileService.cs` — IFileService, FileService
- [x] `src/AgentZ.Cli/Core/RuntimeDetector.cs` — IRuntimeDetector, RuntimeDetector
- [x] `src/AgentZ.Cli/Core/RuntimeParser.cs` — RuntimeParser.ParseSpec, ParseSpecs

## Phase 3: Dynamic Container Building
- [x] `src/AgentZ.Cli/Core/ContainerBuilder.cs` — IContainerBuilder, ContainerBuilder

## Phase 4: Config & Interactive Greenfield Setup
- [x] `src/AgentZ.Cli/Models/AgentZConfig.cs` — AgentZConfig record
- [x] `src/AgentZ.Cli/Services/ConfigService.cs` — IConfigService, ConfigService
- [x] `src/AgentZ.Cli/Prompts/GreenfieldPrompts.cs` — PromptForRuntimesAsync, all sub-prompts

## Phase 5: Agent Runtime Requests
- [x] `containers/agents/opencode/entrypoint.sh` — request_runtime helper, safe_exec wrapper
- [x] `src/AgentZ.Cli/Core/RuntimeRequestHandler.cs` — FileSystemWatcher on .agentz/runtime-request

## Phase 6: Commands
- [x] `src/AgentZ.Cli/Commands/RunCommand.cs` — full implementation
- [x] `src/AgentZ.Cli/Commands/ListCommand.cs` — list agents and runtimes
- [x] `src/AgentZ.Cli/Commands/InitCommand.cs` — stub
- [x] `src/AgentZ.Cli/Commands/NewCommand.cs` — stub
- [x] `src/AgentZ.Cli/Commands/AddRuntimeCommand.cs` — stub
- [x] `src/AgentZ.Cli/Commands/PrebuildCommand.cs` — stub

## Phase 7: Tests
- [x] `tests/AgentZ.Tests/AgentZ.Tests.csproj` — xUnit, Moq, FluentAssertions
- [x] `tests/AgentZ.Tests/Core/RuntimeParserTests.cs` — 10 tests, all pass

## Phase 8: Build & Distribution
- [x] `.github/workflows/release.yml` — matrix build (5 platforms)
- [x] `scripts/install.sh` — curl-pipe installer

---

## Fixes Applied (vs plan)
1. Added `<ImplicitUsings>enable</ImplicitUsings>` to both csproj files (plan omitted it)
2. Fixed `using var cts = new CancellationToken()` → `CancellationTokenSource` in RunCommand.cs
3. Removed async from prompt helpers that don't await; used Task.FromResult pattern

## Known Issues
- YamlDotNet 16.x uses reflection — IL3050 warnings during AOT publish. Fix: switch to `StaticSerializerBuilder`/`StaticDeserializerBuilder` when ready to ship native binary.

---

## Phase 9: Model Management (llama-server)
- [x] `src/AgentZ.Cli/Models/ModelInfo.cs` — ModelInfo, QuantOption records
- [x] `src/AgentZ.Cli/Models/ModelCatalog.cs` — 14 curated models (Qwen3, Llama 3, Mistral, Phi-4, Gemma 3)
- [x] `src/AgentZ.Cli/Services/OpenCodeConfigService.cs` — read/write .agentz/opencode.json via JsonNode (AOT-safe)
- [x] `src/AgentZ.Cli/Services/ModelDownloadService.cs` — HuggingFace download + Spectre progress bar; models to ~/.agentz/models/
- [x] `src/AgentZ.Cli/Services/LlamaServerService.cs` — kill/start llama-server, health check poll
- [x] `src/AgentZ.Cli/Prompts/ModelPrompts.cs` — TUI model/quant selection, remove selection
- [x] `src/AgentZ.Cli/Commands/ModelCommand.cs` — model add / list / remove subcommands
- [x] `Program.cs` — registered ModelCommand
- [x] `RunCommand.cs` — GetOpenCodeVolumes: syncs .agentz/opencode.json to ~/.config/opencode/ before launch

---

## Review
Build: ✅ 0 errors, 2 warnings (YamlDotNet AOT — known, non-blocking)
Tests: ✅ 10/10 pass
