using System.Diagnostics;
using Spectre.Console;

namespace Agelos.Cli.Services;

public static class LlamaServerService
{
    private const int    Port         = 8033;
    private const string BinaryName   = "llama-server";
    private const int    HealthChecks = 30;

    public static async Task RestartAsync(string modelPath, int contextSize)
    {
        Kill();
        Start(modelPath, contextSize);
        await WaitForHealthAsync();
    }

    private static void Kill()
    {
        var procs = Process.GetProcessesByName(BinaryName);
        if (procs.Length == 0) return;

        AnsiConsole.MarkupLine($"[dim]Stopping {procs.Length} existing llama-server process(es)...[/]");
        foreach (var p in procs)
        {
            try { p.Kill(entireProcessTree: true); p.WaitForExit(3000); }
            catch { /* already dead */ }
            finally { p.Dispose(); }
        }
    }

    private static void Start(string modelPath, int contextSize)
    {
        AnsiConsole.MarkupLine($"[dim]Starting llama-server: {Path.GetFileName(modelPath)}...[/]");

        var psi = new ProcessStartInfo(BinaryName)
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = false,
            RedirectStandardError  = false,
        };
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add(modelPath);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(Port.ToString());
        psi.ArgumentList.Add("--ctx-size");
        psi.ArgumentList.Add(contextSize.ToString());
        psi.ArgumentList.Add("-ngl");
        psi.ArgumentList.Add("999");

        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to start llama-server: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[yellow]Ensure llama-server is in your PATH.[/]");
            AnsiConsole.MarkupLine($"[dim]Manual: llama-server -m \"{modelPath}\" --port {Port} --ctx-size {contextSize} -ngl 999[/]");
        }
    }

    private static async Task WaitForHealthAsync()
    {
        AnsiConsole.Markup("[dim]Waiting for llama-server[/]");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var i = 0; i < HealthChecks; i++)
        {
            try
            {
                var resp = await http.GetAsync($"http://127.0.0.1:{Port}/health");
                if (resp.IsSuccessStatusCode)
                {
                    AnsiConsole.MarkupLine(" [green]ready[/]");
                    return;
                }
            }
            catch { }

            AnsiConsole.Markup(".");
            await Task.Delay(1000);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Health check timed out — server may still be loading.[/]");
    }
}
