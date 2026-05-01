using Agelos.Cli.Models;
using Agelos.Cli.Services;
using Spectre.Console;

namespace Agelos.Cli.Core;

public class RuntimeRequestHandler
{
    private readonly string _projectPath;
    private readonly string _requestFile;
    private readonly IConfigService _configService;
    private readonly IContainerBuilder _containerBuilder;
    private volatile bool _watching;

    public RuntimeRequestHandler(
        string projectPath,
        IConfigService configService,
        IContainerBuilder containerBuilder)
    {
        _projectPath = projectPath;
        _requestFile = Path.Combine(projectPath, ".agelos", "runtime-request");
        _configService = configService;
        _containerBuilder = containerBuilder;
    }

    public async Task StartWatchingAsync(
        string agent,
        IContainerRuntime runtime,
        Func<Task> onRestart,
        CancellationToken cancellationToken = default)
    {
        _watching = true;

        var agentDir = Path.Combine(_projectPath, ".agelos");
        Directory.CreateDirectory(agentDir);

        using var watcher = new FileSystemWatcher(agentDir)
        {
            Filter = "runtime-request",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        watcher.Created += async (_, _) => await HandleRequestAsync(agent, runtime, onRestart, cancellationToken);
        watcher.Changed += async (_, _) => await HandleRequestAsync(agent, runtime, onRestart, cancellationToken);
        watcher.EnableRaisingEvents = true;

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (TaskCanceledException) { }
        finally
        {
            _watching = false;
        }
    }

    public void StopWatching() => _watching = false;

    private async Task HandleRequestAsync(
        string agent,
        IContainerRuntime runtime,
        Func<Task> onRestart,
        CancellationToken cancellationToken)
    {
        if (!_watching || !File.Exists(_requestFile))
            return;

        try
        {
            await Task.Delay(100, cancellationToken);

            var content = await File.ReadAllTextAsync(_requestFile, cancellationToken);
            var match = System.Text.RegularExpressions.Regex.Match(content, @"RUNTIME_REQUEST:(.+)");
            if (!match.Success) return;

            var requestedRuntime = match.Groups[1].Value.Trim();

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]Agent requests runtime: {requestedRuntime}[/]");

            var spec = RuntimeParser.ParseSpec(requestedRuntime);

            var approve = AnsiConsole.Confirm(
                $"Add [green]{spec.Language}:{spec.Version}[/] to container?",
                defaultValue: true);

            if (!approve)
            {
                AnsiConsole.MarkupLine("[red]Runtime request denied[/]");
                File.Delete(_requestFile);
                return;
            }

            var config = await _configService.LoadConfigAsync(_projectPath) ?? new AgelosConfig
            {
                Version = "1.0",
                Runtimes = new RuntimeRequirements(),
                Agent = agent
            };

            var updatedRuntimes = AddRuntime(config.Runtimes, spec);
            config = config with { Runtimes = updatedRuntimes };

            await _configService.SaveConfigAsync(_projectPath, config);
            AnsiConsole.MarkupLine("[green]Updated .agelos.yml[/]");
            AnsiConsole.MarkupLine("[yellow]Rebuilding container...[/]");

            await _containerBuilder.BuildCustomImageAsync(agent, config.Runtimes, config.AddOns, runtime, cancellationToken);

            File.Delete(_requestFile);

            AnsiConsole.MarkupLine("[green]Container rebuilt[/]");
            AnsiConsole.MarkupLine("[yellow]Restarting agent...[/]");
            AnsiConsole.WriteLine();

            await onRestart();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error handling runtime request: {ex.Message}[/]");
        }
    }

    private static RuntimeRequirements AddRuntime(RuntimeRequirements requirements, RuntimeSpec spec) =>
        spec.Language switch
        {
            "dotnet" => requirements with
            {
                DotNet = (requirements.DotNet ?? new List<string>())
                    .Append(spec.Version).Distinct().ToList()
            },
            "node" or "nodejs" => requirements with { Node = spec.Version },
            "python"           => requirements with { Python = spec.Version },
            "go" or "golang"   => requirements with { Go = spec.Version },
            "rust"             => requirements with { Rust = true },
            _ => requirements with
            {
                Custom = (requirements.Custom ?? new List<CustomRuntime>())
                    .Append(new CustomRuntime(spec.Language, spec.Version, spec.InstallScript))
                    .ToList()
            }
        };
}
