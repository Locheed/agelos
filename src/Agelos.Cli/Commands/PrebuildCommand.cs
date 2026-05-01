using System.CommandLine;
using Spectre.Console;

namespace Agelos.Cli.Commands;

public class PrebuildCommand : Command
{
    public PrebuildCommand() : base("prebuild", "Pre-build container images for configured runtimes")
    {
        this.SetHandler(() =>
        {
            AnsiConsole.MarkupLine("[yellow]prebuild: not yet implemented[/]");
        });
    }
}
