using Agelos.Cli.Models;
using Agelos.Cli.Services;
using Spectre.Console;

namespace Agelos.Cli.Core;

public interface IContainerRuntime
{
    string Name { get; }
    Task RunAsync(ContainerOptions options, CancellationToken cancellationToken = default);
    Task BuildAsync(BuildOptions options, CancellationToken cancellationToken = default);
    Task<bool> ImageExistsAsync(string image, CancellationToken cancellationToken = default);
}

public class PodmanRuntime : IContainerRuntime
{
    private readonly IProcessService _processService;

    public PodmanRuntime(IProcessService processService) => _processService = processService;

    public string Name => "podman";

    public async Task RunAsync(ContainerOptions options, CancellationToken cancellationToken = default)
    {
        var args = new List<string>
        {
            "run",
            "--userns=keep-id",
            options.Interactive ? "-it" : "",
            "--rm",
            "--workdir", options.WorkingDirectory
        };

        foreach (var volume in options.Volumes)
        {
            args.Add("-v");
            args.Add($"{volume.Host}:{volume.Container}:{volume.Mode}");
        }

        foreach (var (key, value) in options.Environment)
        {
            args.Add("-e");
            args.Add($"{key}={value}");
        }

        foreach (var port in options.Ports)
        {
            args.Add("-p");
            args.Add($"{port.Host}:{port.Container}");
        }

        args.Add(options.Image);
        args.AddRange(options.Command);

        await _processService.RunInteractiveAsync("podman", args.Where(a => !string.IsNullOrEmpty(a)).ToArray(), cancellationToken);
    }

    public async Task BuildAsync(BuildOptions options, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "build", "-f", options.Dockerfile, "-t", options.Tag };

        foreach (var (key, value) in options.BuildArgs)
        {
            args.Add("--build-arg");
            args.Add($"{key}={value}");
        }

        args.Add(options.Context);
        await _processService.RunInteractiveAsync("podman", args.ToArray(), cancellationToken);
    }

    public async Task<bool> ImageExistsAsync(string image, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processService.RunAsync("podman", new[] { "image", "inspect", image }, cancellationToken);
            return result.ExitCode == 0;
        }
        catch { return false; }
    }
}

public class DockerRuntime : IContainerRuntime
{
    private readonly IProcessService _processService;

    public DockerRuntime(IProcessService processService) => _processService = processService;

    public string Name => "docker";

    public async Task RunAsync(ContainerOptions options, CancellationToken cancellationToken = default)
    {
        var args = new List<string>
        {
            "run",
            options.Interactive ? "-it" : "",
            "--rm",
            "--workdir", options.WorkingDirectory
        };

        foreach (var volume in options.Volumes)
        {
            args.Add("-v");
            args.Add($"{volume.Host}:{volume.Container}:{volume.Mode}");
        }

        foreach (var (key, value) in options.Environment)
        {
            args.Add("-e");
            args.Add($"{key}={value}");
        }

        foreach (var port in options.Ports)
        {
            args.Add("-p");
            args.Add($"{port.Host}:{port.Container}");
        }

        args.Add(options.Image);
        args.AddRange(options.Command);

        await _processService.RunInteractiveAsync("docker", args.Where(a => !string.IsNullOrEmpty(a)).ToArray(), cancellationToken);
    }

    public async Task BuildAsync(BuildOptions options, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "build", "-f", options.Dockerfile, "-t", options.Tag };

        foreach (var (key, value) in options.BuildArgs)
        {
            args.Add("--build-arg");
            args.Add($"{key}={value}");
        }

        args.Add(options.Context);
        await _processService.RunInteractiveAsync("docker", args.ToArray(), cancellationToken);
    }

    public async Task<bool> ImageExistsAsync(string image, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processService.RunAsync("docker", new[] { "image", "inspect", image }, cancellationToken);
            return result.ExitCode == 0;
        }
        catch { return false; }
    }
}

public static class ContainerRuntimeDetector
{
    public static async Task<IContainerRuntime> DetectAsync(IProcessService processService)
    {
        try
        {
            var result = await processService.RunAsync("podman", new[] { "--version" });
            if (result.ExitCode == 0)
            {
                AnsiConsole.MarkupLine("[dim]Using Podman[/]");
                return new PodmanRuntime(processService);
            }
        }
        catch { }

        try
        {
            var result = await processService.RunAsync("docker", new[] { "--version" });
            if (result.ExitCode == 0)
            {
                AnsiConsole.MarkupLine("[dim]Using Docker[/]");
                return new DockerRuntime(processService);
            }
        }
        catch { }

        throw new InvalidOperationException(
            "Neither Podman nor Docker found. Please install one of them.\n" +
            "Podman: https://podman.io/getting-started/installation\n" +
            "Docker: https://docs.docker.com/get-docker/"
        );
    }
}
