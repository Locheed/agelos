using System.CommandLine;
using System.Diagnostics;
using Agelos.Cli.Services;
using Spectre.Console;

namespace Agelos.Cli.Commands;

public class WebUiCommand : Command
{
    public WebUiCommand() : base("webui", "Manage the Open WebUI browser chat interface")
    {
        AddCommand(new WebUiStartCommand());
        AddCommand(new WebUiStopCommand());
        AddCommand(new WebUiStatusCommand());
    }
}

public class WebUiStartCommand : Command
{
    public WebUiStartCommand() : base("start", "Start Open WebUI backed by llama-server")
    {
        var cudaOpt = new Option<bool>("--cuda", "Use the CUDA-accelerated image (requires NVIDIA GPU + container toolkit)");
        AddOption(cudaOpt);
        this.SetHandler(ExecuteAsync, cudaOpt);
    }

    private static async Task ExecuteAsync(bool cuda)
    {
        OpenWebUiService service = OpenWebUiService.Create();

        if (!service.HasRuntime)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No container runtime found (Podman or Docker required).");
            AnsiConsole.MarkupLine("[dim]Install Podman or Docker and ensure it is in your PATH.[/]");
            return;
        }

        if (cuda)
        {
            AnsiConsole.MarkupLine("[dim]CUDA mode enabled — using [cyan]ghcr.io/open-webui/open-webui:cuda[/].[/]");
            AnsiConsole.MarkupLine("[dim]Requires: NVIDIA GPU, nvidia-container-toolkit, and Docker/Podman with GPU support.[/]");
            AnsiConsole.WriteLine();
        }

        bool llamaHealthy = await OpenWebUiService.IsLlamaServerHealthyAsync();
        if (!llamaHealthy)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] llama-server is not responding on port 8033.");
            AnsiConsole.MarkupLine("[dim]Open WebUI will start, but there will be no models available until llama-server is running.[/]");
            AnsiConsole.MarkupLine("[dim]Run [cyan]agelos model add[/] to download a model and start llama-server.[/]");
            AnsiConsole.WriteLine();
        }

        (bool running, int existingPort) = await service.GetStatusAsync();
        if (running)
        {
            AnsiConsole.MarkupLine($"[cyan]Open WebUI is already running[/] -> http://localhost:{existingPort}");
            AnsiConsole.WriteLine();
            if (!AnsiConsole.Confirm("Already running. Restart?")) return;
            AnsiConsole.WriteLine();
            await service.StopAsync();
            AnsiConsole.WriteLine();
        }

        int port = await service.StartAsync(cuda);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Open WebUI ready[/] -> [cyan]http://localhost:{port}[/]");
        AnsiConsole.WriteLine();

        try
        {
            Process.Start(new ProcessStartInfo($"http://localhost:{port}") { UseShellExecute = true });
        }
        catch
        {
            AnsiConsole.MarkupLine($"[dim]Open your browser and navigate to http://localhost:{port}[/]");
        }
    }
}

public class WebUiStopCommand : Command
{
    public WebUiStopCommand() : base("stop", "Stop the Open WebUI container")
    {
        this.SetHandler(ExecuteAsync);
    }

    private static async Task ExecuteAsync()
    {
        OpenWebUiService service = OpenWebUiService.Create();

        if (!service.HasRuntime)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No container runtime found.");
            return;
        }

        (bool running, int _) = await service.GetStatusAsync();
        if (!running)
        {
            AnsiConsole.MarkupLine("[dim]Open WebUI is not running.[/]");
            return;
        }

        await service.StopAsync();
        AnsiConsole.MarkupLine("[green]Open WebUI stopped.[/]");
    }
}

public class WebUiStatusCommand : Command
{
    public WebUiStatusCommand() : base("status", "Show Open WebUI container status")
    {
        this.SetHandler(ExecuteAsync);
    }

    private static async Task ExecuteAsync()
    {
        OpenWebUiService service = OpenWebUiService.Create();

        if (!service.HasRuntime)
        {
            AnsiConsole.MarkupLine("[dim]No container runtime found.[/]");
            return;
        }

        (bool running, int port) = await service.GetStatusAsync();

        AnsiConsole.WriteLine();
        if (running)
        {
            AnsiConsole.MarkupLine($"Open WebUI  [green]Running[/] -> [cyan]http://localhost:{port}[/]");

            bool llamaHealthy = await OpenWebUiService.IsLlamaServerHealthyAsync();
            string llamaStatus = llamaHealthy ? "[green]Running[/]" : "[yellow]Not running[/]";
            AnsiConsole.MarkupLine($"llama-server  {llamaStatus} (port 8033)");
        }
        else
        {
            AnsiConsole.MarkupLine("Open WebUI  [dim]Not running[/]");
            AnsiConsole.MarkupLine("[dim]Run [cyan]agelos webui start[/] to launch.[/]");
        }

        AnsiConsole.WriteLine();
    }
}

