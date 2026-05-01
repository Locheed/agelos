# How to Add a New Agent

This guide covers every file to change when adding support for a new AI coding agent
(e.g. Cursor, Continue, Claude Code).

---

## Files to change

| # | File | What to add |
|---|------|-------------|
| 1 | `src/Agelos.Cli/Core/ContainerBuilder.cs` | `GetAgentSetup()` case + `GetAgentCmd()` case |
| 2 | `src/Agelos.Cli/Commands/RunCommand.cs` | `GetVolumes()` agent switch arm |
| 3 | `src/Agelos.Cli/Commands/ListCommand.cs` | `table.AddRow` in `ListAgents()` |
| 4 | `assets/agents/<agent>/entrypoint.sh` | Runtime-request bridge script (if needed) |
| 5 | `README.md` | Add to `agelos run` examples and any per-project config notes |

---

## 1. `src/Agelos.Cli/Core/ContainerBuilder.cs`

Add a case to `GetAgentSetup` with the steps to install the agent inside the Alpine container:

```csharp
"cursor" =>
    "# Cursor\n" +
    "RUN apk add --no-cache nodejs npm\n" +
    "RUN npm install -g @cursor/cli\n",
```

Add a case to `GetAgentCmd` for the default container command:

```csharp
"cursor" => "CMD [\"cursor\", \"--no-sandbox\"]",
```

> If the container uses a custom entrypoint script (see step 4), change `GetAgentCmd` to
> `ENTRYPOINT` instead of `CMD` and `COPY` the script in `GetAgentSetup`.

---

## 2. `src/Agelos.Cli/Commands/RunCommand.cs`

Add an arm to the `GetVolumes` switch for any host directories the agent needs bind-mounted
(config, state, credentials, SSH keys, etc.):

```csharp
"cursor" => new[]
{
    new VolumeMount(Path.Combine(home, ".cursor"), "/home/codeuser/.cursor", "rw"),
},
```

If the agent has a **project-local config file** that should be synced before launch (like
`opencode.json` for OpenCode), add a helper method similar to `GetOpenCodeVolumes`:

```csharp
private static VolumeMount[] GetCursorVolumes(string projectPath, string home)
{
    var configDir = Path.Combine(home, ".cursor");
    Directory.CreateDirectory(configDir);

    var projectConfig = Path.Combine(projectPath, ".agelos", "cursor.json");
    if (File.Exists(projectConfig))
        File.Copy(projectConfig, Path.Combine(configDir, "config.json"), overwrite: true);

    return [ new VolumeMount(configDir, "/home/codeuser/.cursor", "rw") ];
}
```

Then wire it in `GetVolumes`:

```csharp
"cursor" => GetCursorVolumes(projectPath, home),
```

---

## 3. `src/Agelos.Cli/Commands/ListCommand.cs`

```csharp
table.AddRow("cursor", "Cursor - AI code editor");
```

---

## 4. `assets/agents/<agent>/entrypoint.sh`

Create `assets/agents/cursor/entrypoint.sh` **only** if the agent needs the runtime-request
bridge — i.e. it will invoke tools (`dotnet`, `node`, `go`, …) that might be missing from the
container and should trigger an on-demand rebuild.

Copy the structure from `assets/agents/opencode/entrypoint.sh` and change only the final `exec` line:

```bash
exec cursor "$@"
```

If the agent doesn't need the bridge, skip this file entirely — the simple `CMD` in the
Dockerfile is sufficient.

---

## 5. `README.md`

- Add the agent name to the `agelos run` examples block.
- If the agent has a per-project config file (e.g. `.agelos/cursor.json`), document it under
  `## Configuration` in the same style as `.agelos/opencode.json`.

---

## Checklist summary

```
[ ] ContainerBuilder.GetAgentSetup  — install steps
[ ] ContainerBuilder.GetAgentCmd    — CMD / ENTRYPOINT
[ ] RunCommand.GetVolumes           — bind mounts
[ ] ListCommand.ListAgents          — display row
[ ] assets/agents/<name>/entrypoint.sh  (only if bridge needed)
[ ] README.md
```

