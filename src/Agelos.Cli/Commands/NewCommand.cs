using System.CommandLine;
using Spectre.Console;

namespace Agelos.Cli.Commands;

public class NewCommand : Command
{
    public NewCommand() : base("new", "Create a new project from template")
    {
        var templateArg = new Argument<string>("template", "Template name (e.g., dotnet-api)");
        var nameArg     = new Argument<string>("name", "Project name");

        AddArgument(templateArg);
        AddArgument(nameArg);

        this.SetHandler((template, name) =>
        {
            AnsiConsole.MarkupLine($"[yellow]new {template} {name}: not yet implemented[/]");
        }, templateArg, nameArg);
    }
}
