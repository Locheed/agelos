using System.Diagnostics;

namespace Agelos.Cli.Services;

public interface IProcessService
{
    Task<ProcessResult> RunAsync(string command, string[] args, CancellationToken cancellationToken = default);
    Task RunInteractiveAsync(string command, string[] args, CancellationToken cancellationToken = default);
}

public record ProcessResult(int ExitCode, string Output, string Error);

public class ProcessService : IProcessService
{
    public async Task<ProcessResult> RunAsync(string command, string[] args, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {command}");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
    }

    public async Task RunInteractiveAsync(string command, string[] args, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {command}");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Process exited with code {process.ExitCode}");
    }
}
