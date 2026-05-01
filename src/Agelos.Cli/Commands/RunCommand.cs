using System.CommandLine;
using Agelos.Cli.Core;
using Agelos.Cli.Models;
using Agelos.Cli.Prompts;
using Agelos.Cli.Services;
using Spectre.Console;

namespace Agelos.Cli.Commands;

public class RunCommand : Command
{
    public RunCommand() : base("run", "Run an AI coding agent")
    {
        var agentArg    = new Argument<string>("agent", "Agent to run (e.g., opencode, aider)");
        var runtimesOpt = new Option<string?>("--runtimes", "Runtime specs (e.g., dotnet:10,node:20)");
        var addOnOpt    = new Option<string?>("--addon", "Add-ons to layer on agent (e.g., llama-cpp)");
        var minimalOpt  = new Option<bool>("--minimal", "Start with minimal image");
        var rebuildOpt  = new Option<bool>("--rebuild", "Force rebuild image");

        AddArgument(agentArg);
        AddOption(runtimesOpt);
        AddOption(addOnOpt);
        AddOption(minimalOpt);
        AddOption(rebuildOpt);

        this.SetHandler(async (agent, runtimesSpec, addOnSpec, minimal, rebuild) =>
        {
            await ExecuteAsync(agent, runtimesSpec, addOnSpec, minimal, rebuild);
        }, agentArg, runtimesOpt, addOnOpt, minimalOpt, rebuildOpt);
    }

    private static async Task ExecuteAsync(string agent, string? runtimesSpec, string? addOnSpec, bool minimal, bool rebuild)
    {
        var addOns = ParseAddOns(addOnSpec);
        var projectPath = Directory.GetCurrentDirectory();

        var fileService      = new FileService();
        var processService   = new ProcessService();
        var configService    = new ConfigService(fileService);
        var runtimeDetector  = new RuntimeDetector(fileService);
        var containerBuilder = new ContainerBuilder(fileService);

        var runtime = await ContainerRuntimeDetector.DetectAsync(processService);

        var requirements = await DetermineRuntimesAsync(
            projectPath, runtimesSpec, minimal, runtimeDetector, configService);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Runtime configuration:[/]");
        DisplayRuntimes(requirements);
        AnsiConsole.WriteLine();

        var imageTag = await containerBuilder.BuildCustomImageAsync(agent, requirements, addOns, runtime);

        var volumes = GetVolumes(projectPath, agent);

        var requestHandler = new RuntimeRequestHandler(projectPath, configService, containerBuilder);

        using var cts = new CancellationTokenSource();
        var watchTask = requestHandler.StartWatchingAsync(agent, runtime, async () =>
        {
            var config = await configService.LoadConfigAsync(projectPath);
            if (config != null)
            {
                await containerBuilder.BuildCustomImageAsync(agent, config.Runtimes, config.AddOns, runtime);
                AnsiConsole.MarkupLine("[green]Container updated. Please restart manually.[/]");
            }
        }, cts.Token);

        try
        {
            await runtime.RunAsync(new ContainerOptions
            {
                Image = imageTag,
                WorkingDirectory = "/workspace",
                Volumes = volumes,
                Interactive = true
            });
        }
        finally
        {
            requestHandler.StopWatching();
            await cts.CancelAsync();
        }
    }

    private static async Task<RuntimeRequirements> DetermineRuntimesAsync(
        string projectPath,
        string? runtimesSpec,
        bool minimal,
        IRuntimeDetector runtimeDetector,
        IConfigService configService)
    {
        if (!string.IsNullOrEmpty(runtimesSpec))
        {
            AnsiConsole.MarkupLine("[dim]Using --runtimes flag[/]");
            return RuntimeParser.ParseSpecs(runtimesSpec);
        }

        var config = await configService.LoadConfigAsync(projectPath);
        if (config != null)
        {
            AnsiConsole.MarkupLine("[dim]Using .agelos.yml[/]");
            return config.Runtimes;
        }

        var detected = await runtimeDetector.DetectAsync(projectPath);
        if (!detected.IsEmpty)
        {
            AnsiConsole.MarkupLine("[dim]Detected from project files[/]");
            return detected;
        }

        if (await runtimeDetector.IsEmptyProjectAsync(projectPath))
        {
            if (minimal)
            {
                AnsiConsole.MarkupLine("[dim]Using minimal image[/]");
                return new RuntimeRequirements();
            }

            var runtimes = await GreenfieldPrompts.PromptForRuntimesAsync();
            await configService.CreateProjectConfigAsync(projectPath, runtimes);
            return runtimes;
        }

        AnsiConsole.MarkupLine("[dim]Using minimal image[/]");
        return new RuntimeRequirements();
    }

    private static void DisplayRuntimes(RuntimeRequirements r)
    {
        if (r.DotNet?.Any() == true)
            AnsiConsole.MarkupLine($"   • .NET {string.Join(", ", r.DotNet)}");
        if (r.Node != null)
            AnsiConsole.MarkupLine($"   • Node.js {r.Node}");
        if (r.Python != null)
            AnsiConsole.MarkupLine($"   • Python {r.Python}");
        if (r.Go != null)
            AnsiConsole.MarkupLine($"   • Go {r.Go}");
        if (r.Rust)
            AnsiConsole.MarkupLine("   • Rust");
        if (r.Custom?.Any() == true)
            foreach (var c in r.Custom)
                AnsiConsole.MarkupLine($"   • {c.Name} {c.Version}");
        if (r.IsEmpty)
            AnsiConsole.MarkupLine("   • Minimal (no runtimes)");
    }

    private static VolumeMount[] GetOpenCodeVolumes(string projectPath, string home)
    {
        var configDir = Path.Combine(home, ".config", "opencode");
        var shareDir  = Path.Combine(home, ".local", "share", "opencode");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(shareDir);

        // Project-local opencode config takes precedence — sync to global dir before launch
        var projectConfig = OpenCodeConfigService.GetConfigPath(projectPath);
        if (File.Exists(projectConfig))
            File.Copy(projectConfig, Path.Combine(configDir, "config.json"), overwrite: true);

        return
        [
            new VolumeMount(configDir, "/home/codeuser/.config/opencode",      "rw"),
            new VolumeMount(shareDir,  "/home/codeuser/.local/share/opencode", "rw"),
        ];
    }

    private static IReadOnlyList<string>? ParseAddOns(string? spec) =>
        string.IsNullOrWhiteSpace(spec)
            ? null
            : spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static List<VolumeMount> GetVolumes(string projectPath, string agent)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var volumes = new List<VolumeMount> { new(projectPath, "/workspace", "rw") };

        var agentVolumes = agent switch
        {
            "opencode" => GetOpenCodeVolumes(projectPath, home),
            "aider" => new[]
            {
                new VolumeMount(Path.Combine(home, ".aider"),
                    "/home/codeuser/.aider", "rw")
            },
            _ => Array.Empty<VolumeMount>()
        };

        foreach (var v in agentVolumes)
            Directory.CreateDirectory(v.Host);

        volumes.AddRange(agentVolumes);
        return volumes;
    }
}
