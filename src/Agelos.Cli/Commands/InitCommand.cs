using System.CommandLine;
using Spectre.Console;

namespace Agelos.Cli.Commands;

public class InitCommand : Command
{
    public InitCommand() : base("init", "Initialize Agelos in current project")
    {
        this.SetHandler(() =>
        {
            AnsiConsole.MarkupLine("[yellow]init: not yet implemented[/]");
        });
    }
}
