using Agelos.Cli.Models;
using Spectre.Console;

namespace Agelos.Cli.Prompts;

public static class ModelPrompts
{
    private static readonly (string Label, string Prefix)[] Families =
    [
        ("Qwen3",   "qwen"),
        ("Llama 3", "llama"),
        ("Other",   ""),
    ];

    public static (ModelInfo Model, QuantOption Quant)? PromptForModelAndQuant()
    {
        AnsiConsole.WriteLine();

        // Step 1: pick family
        var family = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select model family:")
                .PageSize(10)
                .AddChoices(Families.Select(f => f.Label))
        );

        var prefix  = Families.First(f => f.Label == family).Prefix;
        var inFamily = prefix.Length > 0
            ? ModelCatalog.All.Where(m => m.Id.StartsWith(prefix)).ToArray()
            : ModelCatalog.All.Where(m => !m.Id.StartsWith("qwen") && !m.Id.StartsWith("llama")).ToArray();

        AnsiConsole.WriteLine();

        // Step 2: pick model
        var model = AnsiConsole.Prompt(
            new SelectionPrompt<ModelInfo>()
                .Title($"Select {family} model:")
                .PageSize(15)
                .UseConverter(m => $"{m.DisplayName,-28}  {m.ContextSize / 1024}k ctx")
                .AddChoices(inFamily)
        );

        AnsiConsole.WriteLine();

        // Step 3: pick quant
        var quant = AnsiConsole.Prompt(
            new SelectionPrompt<QuantOption>()
                .Title($"Select quantization for {model.DisplayName}:")
                .PageSize(10)
                .UseConverter(q => q.Display)
                .AddChoices(model.Quants)
        );

        return (model, quant);
    }

    public static string? PromptForInstalledModel(IReadOnlyList<string> modelFiles)
    {
        if (modelFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No models downloaded yet.[/]");
            return null;
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select model to remove:")
                .PageSize(15)
                .AddChoices(modelFiles)
        );
    }
}
