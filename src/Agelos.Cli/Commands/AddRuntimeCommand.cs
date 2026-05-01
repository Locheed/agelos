using System.CommandLine;
using Spectre.Console;

namespace Agelos.Cli.Commands;

public class AddRuntimeCommand : Command
{
    public AddRuntimeCommand() : base("add-runtime", "Add a runtime to the current project")
    {
        var runtimeArg = new Argument<string>("runtime", "Runtime spec (e.g., dotnet:10)");
        AddArgument(runtimeArg);

        this.SetHandler((runtime) =>
        {
            AnsiConsole.MarkupLine($"[yellow]add-runtime {runtime}: not yet implemented[/]");
        }, runtimeArg);
    }
}
