using Agelos.Cli.Core;
using Agelos.Cli.Models;
using Spectre.Console;

namespace Agelos.Cli.Prompts;

public static class GreenfieldPrompts
{
    public static async Task<RuntimeRequirements> PromptForRuntimesAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Empty project detected[/]");
        AnsiConsole.WriteLine();

        var projectType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What type of project are you starting?")
                .PageSize(10)
                .AddChoices(
                    ".NET API/Backend",
                    "Node.js/TypeScript",
                    "Python",
                    "Full-stack (Backend + Frontend)",
                    "─────────────────",
                    "→ Choose runtimes manually",
                    "→ Start minimal (add later)"
                )
        );

        return projectType switch
        {
            ".NET API/Backend"             => PromptDotNetSetup(),
            "Node.js/TypeScript"           => PromptNodeSetup(),
            "Python"                       => PromptPythonSetup(),
            "Full-stack (Backend + Frontend)" => PromptFullStackSetup(),
            "→ Choose runtimes manually"   => await PromptCustomRuntimesAsync(),
            _                              => new RuntimeRequirements()
        };
    }

    private static RuntimeRequirements PromptDotNetSetup()
    {
        var version = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Which .NET version?")
                .AddChoices(".NET 11 (latest)", ".NET 10 (LTS)", ".NET 9", "Both 10+11 (preview)")
        );

        return version switch
        {
            ".NET 11 (latest)"          => new RuntimeRequirements { DotNet = new List<string> { "11" } },
            ".NET 10 (LTS)"             => new RuntimeRequirements { DotNet = new List<string> { "10" } },
            ".NET 9"                    => new RuntimeRequirements { DotNet = new List<string> { "9" } },
            "Both 10+11 (preview)"      => new RuntimeRequirements { DotNet = new List<string> { "10", "11" } },
            _                           => new RuntimeRequirements()
        };
    }

    private static RuntimeRequirements PromptNodeSetup()
    {
        var version = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Which Node.js version?")
                .AddChoices("Node.js 22 (LTS)", "Node.js 24", "Node.js 25 (latest)", "Node.js 20 (LTS)")
        );

        return version switch
        {
            "Node.js 22 (LTS)"      => new RuntimeRequirements { Node = "22" },
            "Node.js 24"            => new RuntimeRequirements { Node = "24" },
            "Node.js 25 (latest)"   => new RuntimeRequirements { Node = "25" },
            "Node.js 20 (LTS)"      => new RuntimeRequirements { Node = "20" },
            _                       => new RuntimeRequirements()
        };
    }

    private static RuntimeRequirements PromptPythonSetup()
    {
        var version = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Which Python version?")
                .AddChoices("Python 3.12", "Python 3.11", "Python 3.10")
        );

        return version switch
        {
            "Python 3.12" => new RuntimeRequirements { Python = "3.12" },
            "Python 3.11" => new RuntimeRequirements { Python = "3.11" },
            "Python 3.10" => new RuntimeRequirements { Python = "3.10" },
            _             => new RuntimeRequirements()
        };
    }

    private static RuntimeRequirements PromptFullStackSetup()
    {
        var backend = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Backend runtime?")
                .AddChoices(".NET 11", ".NET 10 (LTS)", ".NET 9", "Node.js 22 (LTS)", "Python 3.12")
        );

        var backendRequirements = backend switch
        {
            ".NET 11"           => RuntimeParser.ParseSpecs("dotnet:11"),
            ".NET 10 (LTS)"     => RuntimeParser.ParseSpecs("dotnet:10"),
            ".NET 9"            => RuntimeParser.ParseSpecs("dotnet:9"),
            "Node.js 22 (LTS)"  => RuntimeParser.ParseSpecs("node:22"),
            "Python 3.12"       => RuntimeParser.ParseSpecs("python:3.12"),
            _                   => new RuntimeRequirements()
        };

        return backendRequirements with { Node = backendRequirements.Node ?? "22" };
    }

    private static async Task<RuntimeRequirements> PromptCustomRuntimesAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Custom runtime selection[/]");
        AnsiConsole.WriteLine();

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select runtimes (space to toggle):")
                .PageSize(20)
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                .AddChoiceGroup("— Backend Languages —", new[]
                {
                    ".NET 11 (latest)", ".NET 10 (LTS)", ".NET 9",
                    "Node.js 22 (LTS)", "Node.js 24", "Node.js 25 (latest)", "Node.js 20 (LTS)",
                    "Python 3.12", "Python 3.11", "Go 1.22", "Rust (latest)",
                    "Java 21", "PHP 8.3", "Ruby 3.3"
                })
                .AddChoiceGroup("— Mobile/Cross-platform —", new[] { "Flutter" })
                .AddChoiceGroup("— Other —", new[]
                {
                    "Elixir", "Haskell", "Zig", "+ Type custom runtime manually"
                })
        );

        var runtimeSpecs = new List<string>();

        foreach (var item in selected)
        {
            var spec = item switch
            {
                ".NET 11 (latest)"               => "dotnet:11",
                ".NET 10 (LTS)"                  => "dotnet:10",
                ".NET 9"                         => "dotnet:9",
                "Node.js 22 (LTS)"               => "node:22",
                "Node.js 24"                     => "node:24",
                "Node.js 25 (latest)"            => "node:25",
                "Node.js 20 (LTS)"               => "node:20",
                "Python 3.12"                    => "python:3.12",
                "Python 3.11"                    => "python:3.11",
                "Go 1.22"                        => "go:1.22",
                "Rust (latest)"                  => "rust:latest",
                "Java 21"                        => "java:21",
                "PHP 8.3"                        => "php:8.3",
                "Ruby 3.3"                       => "ruby:3.3",
                "Flutter"                        => "flutter:latest",
                "Elixir"                         => "elixir:latest",
                "Haskell"                        => "haskell:latest",
                "Zig"                            => "zig:latest",
                "+ Type custom runtime manually" => null,
                _                               => null
            };

            if (spec != null) runtimeSpecs.Add(spec);
        }

        if (selected.Contains("+ Type custom runtime manually"))
            runtimeSpecs.AddRange(await PromptManualRuntimeAsync());

        return RuntimeParser.ParseSpecs(string.Join(",", runtimeSpecs));
    }

    private static Task<List<string>> PromptManualRuntimeAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Enter runtime in format: language:version[/]");
        AnsiConsole.MarkupLine("[dim]Examples: kotlin:1.9, scala:3, nim:2.0[/]");
        AnsiConsole.WriteLine();

        var manual = AnsiConsole.Prompt(
            new TextPrompt<string>("Runtime (comma-separated for multiple):")
                .Validate(input =>
                {
                    if (string.IsNullOrWhiteSpace(input))
                        return ValidationResult.Error("Please enter at least one runtime");

                    var runtimes = input.Split(',', StringSplitOptions.TrimEntries);
                    return runtimes.All(r => r.Contains(':'))
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Format: language:version (e.g., kotlin:1.9)");
                })
        );

        return Task.FromResult(manual.Split(',', StringSplitOptions.TrimEntries).ToList());
    }
}
