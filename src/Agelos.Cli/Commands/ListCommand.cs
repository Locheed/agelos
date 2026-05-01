using System.CommandLine;
using Spectre.Console;

namespace Agelos.Cli.Commands;

public class ListCommand : Command
{
    public ListCommand() : base("list", "List available agents and runtimes")
    {
        var agentsOpt   = new Option<bool>("--agents", "List agents only");
        var runtimesOpt = new Option<bool>("--runtimes", "List runtimes only");

        AddOption(agentsOpt);
        AddOption(runtimesOpt);

        this.SetHandler((agents, runtimes) =>
        {
            var showAll = !agents && !runtimes;

            if (agents || showAll) { ListAgents(); Console.WriteLine(); ListAddOns(); }
            if (runtimes || showAll) { Console.WriteLine(); ListRuntimes(); }
        }, agentsOpt, runtimesOpt);
    }

    private static void ListAgents()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Available Agents:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Description");
        table.AddRow("opencode", "OpenCode - AI coding agent");
        table.AddRow("aider",    "Aider - AI pair programming");
        table.AddRow("gemini",   "Gemini CLI - Google Gemini AI coding agent");

        AnsiConsole.Write(table);
    }

    private static void ListAddOns()
    {
        AnsiConsole.MarkupLine("[yellow]Available Add-ons:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Description");
        table.AddColumn("Usage");
        table.AddRow("llama-cpp", "llama.cpp - Local LLM inference", "--addon llama-cpp");

        AnsiConsole.Write(table);
    }

    private static void ListRuntimes()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Supported Runtimes:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Runtime");
        table.AddColumn("Description");
        table.AddRow("dotnet:9",    ".NET 9");
        table.AddRow("dotnet:10",   ".NET 10 (LTS)");
        table.AddRow("dotnet:11",   ".NET 11 (latest)");
        table.AddRow("node:20",     "Node.js 20 (LTS)");
        table.AddRow("node:22",     "Node.js 22 (LTS)");
        table.AddRow("node:24",     "Node.js 24");
        table.AddRow("node:25",     "Node.js 25 (latest)");
        table.AddRow("python:3.12", "Python 3.12");
        table.AddRow("go:1.22",     "Go 1.22");
        table.AddRow("rust:latest", "Rust (latest)");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]+ Custom runtimes via language:version format[/]");
        AnsiConsole.MarkupLine("[dim]  Example: agelos run opencode --runtimes kotlin:1.9[/]");
    }
}
