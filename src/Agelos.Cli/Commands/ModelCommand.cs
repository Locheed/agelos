using System.CommandLine;
using Agelos.Cli.Models;
using Agelos.Cli.Prompts;
using Agelos.Cli.Services;
using Spectre.Console;

namespace Agelos.Cli.Commands;

public class ModelCommand : Command
{
    public ModelCommand() : base("model", "Manage local AI models for llama-server")
    {
        AddCommand(new ModelAddCommand());
        AddCommand(new ModelListCommand());
        AddCommand(new ModelRemoveCommand());
    }
}

public class ModelAddCommand : Command
{
    public ModelAddCommand() : base("add", "Download a model and register it in the project config")
    {
        this.SetHandler(ExecuteAsync);
    }

    private static async Task ExecuteAsync()
    {
        var projectPath = Directory.GetCurrentDirectory();

        var pick = ModelPrompts.PromptForModelAndQuant();
        if (pick == null) return;

        var (model, quant) = pick.Value;
        var fileName    = quant.FileName(model.FilePrefix);
        var destPath    = ModelDownloadService.ModelPath(fileName);
        var displayName = $"{model.DisplayName} ({quant.Name})";

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Model:[/]  {model.DisplayName}");
        AnsiConsole.MarkupLine($"[bold]Quant:[/]  {quant.Name}  (~{quant.SizeGb:F1} GB)");
        AnsiConsole.MarkupLine($"[bold]File:[/]   {fileName}");
        AnsiConsole.MarkupLine($"[bold]Dest:[/]   {destPath}");
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Download and register?")) return;

        AnsiConsole.WriteLine();
        var modelPath = await ModelDownloadService.DownloadAsync(model.HuggingFaceRepo, fileName);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Updating project config...[/]");
        var svc = new OpenCodeConfigService();
        await svc.AddModelAsync(projectPath, fileName, displayName, model.ContextSize, model.OutputSize);
        AnsiConsole.MarkupLine("[green]Updated:[/] .agelos/opencode.json");

        AnsiConsole.WriteLine();
        await LlamaServerService.RestartAsync(modelPath, model.ContextSize);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Ready.[/] Active model: [cyan]{displayName}[/]  (port {8033})");
    }
}

public class ModelListCommand : Command
{
    public ModelListCommand() : base("list", "List downloaded models and project config status")
    {
        this.SetHandler(ExecuteAsync);
    }

    private static async Task ExecuteAsync()
    {
        var projectPath  = Directory.GetCurrentDirectory();
        var svc          = new OpenCodeConfigService();
        var configModels = await svc.ListModelsAsync(projectPath);
        var downloaded   = ModelDownloadService.ListDownloaded().ToList();

        if (downloaded.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No models downloaded. Run [cyan]agelos model add[/] to get started.[/]");
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]File[/]")
                .AddColumn("[bold]Size[/]")
                .AddColumn("[bold]In config[/]");

            foreach (var fi in downloaded)
            {
                var sizeGb   = fi.Length / (1024.0 * 1024.0 * 1024.0);
                var inConfig = configModels.ContainsKey(fi.Name) ? "[green]yes[/]" : "[dim]no[/]";
                table.AddRow(fi.Name, $"{sizeGb:F1} GB", inConfig);
            }

            AnsiConsole.Write(table);
        }

        if (configModels.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Project config (.agelos/opencode.json):[/]");
            foreach (var (id, name) in configModels)
                AnsiConsole.MarkupLine($"   [cyan]{id}[/]  {name}");
        }
    }
}

public class ModelRemoveCommand : Command
{
    public ModelRemoveCommand() : base("remove", "Delete a downloaded model and unregister from project config")
    {
        this.SetHandler(ExecuteAsync);
    }

    private static async Task ExecuteAsync()
    {
        var projectPath = Directory.GetCurrentDirectory();
        var files       = ModelDownloadService.ListDownloaded().Select(f => f.Name).ToList();

        var fileName = ModelPrompts.PromptForInstalledModel(files);
        if (fileName == null) return;

        var fullPath = ModelDownloadService.ModelPath(fileName);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[yellow]Will delete:[/] {fullPath}");

        if (!AnsiConsole.Confirm("Confirm removal?")) return;

        File.Delete(fullPath);
        AnsiConsole.MarkupLine($"[green]Deleted:[/] {fileName}");

        var svc = new OpenCodeConfigService();
        await svc.RemoveModelAsync(projectPath, fileName);
        AnsiConsole.MarkupLine("[dim]Removed from project config.[/]");
    }
}
