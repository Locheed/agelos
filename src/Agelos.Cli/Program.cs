using System.CommandLine;
using Agelos.Cli.Commands;

var rootCommand = new RootCommand("Agelos - Containerized AI coding agents with dynamic runtime detection");

rootCommand.AddCommand(new RunCommand());
rootCommand.AddCommand(new InitCommand());
rootCommand.AddCommand(new ListCommand());
rootCommand.AddCommand(new NewCommand());
rootCommand.AddCommand(new AddRuntimeCommand());
rootCommand.AddCommand(new PrebuildCommand());
rootCommand.AddCommand(new ModelCommand());
rootCommand.AddCommand(new WebUiCommand());

return await rootCommand.InvokeAsync(args);
