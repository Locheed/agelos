# How to Add a New Runtime

This guide covers every file that must be changed when adding support for a new runtime — either a **brand-new language** (e.g. Kotlin) or a **new version of an existing language** (e.g. Node.js 26).

---

## Full checklist — new language

| # | File | What to change |
|---|------|----------------|
| 1 | `src/Agelos.Cli/Models/RuntimeRequirements.cs` | Add a typed property |
| 2 | `src/Agelos.Cli/Core/RuntimeParser.cs` | Add install-script factory + `ParseSpecs` branch |
| 3 | `src/Agelos.Cli/Core/ContainerBuilder.cs` | Add Dockerfile stanza + image-tag segment |
| 4 | `src/Agelos.Cli/Core/RuntimeDetector.cs` | Add auto-detection from project files |
| 5 | `src/Agelos.Cli/Commands/ListCommand.cs` | Add `table.AddRow` entry |
| 6 | `src/Agelos.Cli/Prompts/GreenfieldPrompts.cs` | Add to multi-select group + spec mapping |
| 7 | `assets/agents/opencode/entrypoint.sh` | Add `safe_exec` fallback case |
| 8 | `README.md` | Update Supported runtimes table |

---

## File-by-file instructions

### 1. `src/Agelos.Cli/Models/RuntimeRequirements.cs`

Add a property for the new language if it deserves first-class handling (auto-detection,
dedicated Dockerfile stanza, named image tag segment). Skip this for fully custom/generic runtimes —
they fall through to the `Custom` list automatically.

```csharp
public string? Kotlin { get; init; }
```

---

### 2. `src/Agelos.Cli/Core/RuntimeParser.cs`

**`InstallScripts` dictionary** — add an entry that generates the shell install command:

```csharp
["kotlin"] = v =>
    $"curl -fsSL https://github.com/JetBrains/kotlin/releases/download/v{v}/kotlin-compiler-{v}.zip " +
    $"-o kotlin.zip && unzip kotlin.zip -d /usr/local && rm kotlin.zip",
```

**`ParseSpecs` switch** — add a case only if you added a typed property in step 1:

```csharp
"kotlin" => requirements with { Kotlin = parsed.Version },
```

If you're relying on the `Custom` fallback you can omit the switch case entirely.

---

### 3. `src/Agelos.Cli/Core/ContainerBuilder.cs`

Add a Dockerfile stanza inside `GenerateDockerfile`:

```csharp
if (runtimes.Kotlin != null)
{
    sb.AppendLine($"# Kotlin {runtimes.Kotlin}");
    sb.AppendLine("RUN apk add --no-cache openjdk21-jdk curl unzip");
    sb.AppendLine($"RUN curl -fsSL https://github.com/JetBrains/kotlin/releases/download/" +
                  $"v{runtimes.Kotlin}/kotlin-compiler-{runtimes.Kotlin}.zip -o kotlin.zip \\");
    sb.AppendLine("    && unzip kotlin.zip -d /usr/local && rm kotlin.zip");
    sb.AppendLine("ENV PATH=\"/usr/local/kotlinc/bin:$PATH\"");
    sb.AppendLine();
}
```

Add a segment in `GenerateImageTag` so caching works correctly:

```csharp
if (runtimes.Kotlin != null)
    parts.Add($"kotlin{runtimes.Kotlin.Replace(".", "")}");
```

---

### 4. `src/Agelos.Cli/Core/RuntimeDetector.cs`

Add a call in `DetectAsync`:

```csharp
var kotlinVersion = await DetectKotlinAsync(projectPath);
if (kotlinVersion != null)
    requirements = requirements with { Kotlin = kotlinVersion };
```

Add the detector method (detect from `build.gradle.kts` or `build.gradle`):

```csharp
private async Task<string?> DetectKotlinAsync(string projectPath)
{
    var candidates = new[] { "build.gradle.kts", "build.gradle" };
    foreach (var name in candidates)
    {
        var path = Path.Combine(projectPath, name);
        if (await _fileService.FileExistsAsync(path))
            return "2.1"; // default; parse version from file if needed
    }
    return null;
}
```

---

### 5. `src/Agelos.Cli/Commands/ListCommand.cs`

Add a row to the `ListRuntimes()` table:

```csharp
table.AddRow("kotlin:2.1", "Kotlin 2.1");
```

---

### 6. `src/Agelos.Cli/Prompts/GreenfieldPrompts.cs`

Three places:

**`PromptCustomRuntimesAsync` — multi-select choice group:**
```csharp
// in AddChoiceGroup("— Backend Languages —", ...):
"Kotlin 2.1",
```

**`PromptCustomRuntimesAsync` — spec-mapping switch:**
```csharp
"Kotlin 2.1" => "kotlin:2.1",
```

**`PromptFullStackSetup`** — add if the language is a viable standalone backend choice.

---

### 7. `assets/agents/opencode/entrypoint.sh`

Add a case to `safe_exec` so the agent can request the runtime mid-session:

```bash
kotlinc)
  request_runtime "kotlin:2.1"
  ;;
```

---

### 8. `README.md`

Update the **Supported runtimes** table under `## Configuration`:

```markdown
| Kotlin | `kotlin:2.1` |
```

---

## New-version-only checklist (e.g. Node.js 26, .NET 12)

When adding a version of an **existing** language, `RuntimeParser`, `ContainerBuilder`,
`RuntimeDetector`, and `RuntimeRequirements` are all **version-agnostic** — no changes needed there.

| File | Change |
|------|--------|
| `ListCommand.cs` | Add `table.AddRow` |
| `GreenfieldPrompts.cs` | Add choice string + spec mapping in all relevant prompts |
| `entrypoint.sh` | Bump default version if the new version becomes the LTS |
| `README.md` | Add the new spec to the Supported runtimes table |

