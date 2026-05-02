using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Spectre.Console;

namespace Agelos.Cli.Services;

public class OpenWebUiService
{
    private const int    DefaultPort    = 3000;
    private const int    LlamaPort      = 8033;
    private const int    HealthChecks   = 60;
    private const string ContainerName      = "agelos-open-webui";
    private const string ContainerImage     = "ghcr.io/open-webui/open-webui:main";
    private const string ContainerImageCuda = "ghcr.io/open-webui/open-webui:cuda";

    private readonly string? _containerBinary;
    private readonly bool    _isPodman;

    public OpenWebUiService(string? containerBinary = null)
    {
        _containerBinary = containerBinary;
        _isPodman        = string.Equals(containerBinary, "podman", StringComparison.OrdinalIgnoreCase);
    }

    // ── Public factory ───────────────────────────────────────────────────────

    public static OpenWebUiService Create()
    {
        foreach (string bin in new[] { "podman", "docker" })
        {
            if (IsCommandAvailable(bin))
                return new OpenWebUiService(bin);
        }
        return new OpenWebUiService(containerBinary: null);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public bool HasRuntime => _containerBinary != null;

    public async Task<int> StartAsync(bool cuda = false)
    {
        int port = FindFreePortFrom(DefaultPort);
        if (port != DefaultPort)
            AnsiConsole.MarkupLine($"[dim]Port {DefaultPort} in use — using port {port}.[/]");
        else
            AnsiConsole.MarkupLine($"[dim]Using port {port}.[/]");

        StartContainer(port, cuda);
        await WaitForHealthAsync(port);
        return port;
    }

    public async Task StopAsync()
    {
        if (_containerBinary == null) return;

        try
        {
            var inspectPsi = new ProcessStartInfo(_containerBinary)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            inspectPsi.ArgumentList.Add("container");
            inspectPsi.ArgumentList.Add("inspect");
            inspectPsi.ArgumentList.Add(ContainerName);

            using var inspect = Process.Start(inspectPsi);
            if (inspect == null) return;
            await inspect.WaitForExitAsync();
            if (inspect.ExitCode != 0) return; // not running

            AnsiConsole.MarkupLine($"[dim]Stopping {ContainerName}...[/]");

            var stopPsi = new ProcessStartInfo(_containerBinary)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            stopPsi.ArgumentList.Add("stop");
            stopPsi.ArgumentList.Add(ContainerName);

            using var stop = Process.Start(stopPsi);
            if (stop != null) await stop.WaitForExitAsync();
        }
        catch { /* not running — that's fine */ }
    }

    public async Task<(bool Running, int Port)> GetStatusAsync()
    {
        if (_containerBinary == null) return (false, 0);

        try
        {
            var psi = new ProcessStartInfo(_containerBinary)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            psi.ArgumentList.Add("container");
            psi.ArgumentList.Add("inspect");
            psi.ArgumentList.Add(ContainerName);
            psi.ArgumentList.Add("--format");
            psi.ArgumentList.Add("{{json .HostConfig.PortBindings}}");

            using var proc = Process.Start(psi);
            if (proc == null) return (false, 0);

            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0) return (false, 0);

            // Parse: {"8080/tcp":[{"HostIp":"","HostPort":"3001"}]}
            int resolvedPort = ParseHostPort(output.Trim());
            return (resolvedPort > 0, resolvedPort);
        }
        catch
        {
            return (false, 0);
        }
    }

    public static async Task<bool> IsLlamaServerHealthyAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            HttpResponseMessage resp = await http.GetAsync($"http://127.0.0.1:{LlamaPort}/health");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Port helpers ─────────────────────────────────────────────────────────

    public static int FindFreePortFrom(int startPort)
    {
        int candidate = startPort;
        while (true)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, candidate);
                listener.Start();
                listener.Stop();
                return candidate;
            }
            catch (SocketException)
            {
                candidate++;
            }
        }
    }

    // ── Container ────────────────────────────────────────────────────────────

    private void StartContainer(int port, bool cuda)
    {
        if (_containerBinary == null) return;

        string image = cuda ? ContainerImageCuda : ContainerImage;
        AnsiConsole.MarkupLine($"[dim]Pulling {image} if needed (first run only)...[/]");

        string apiBaseUrl = _isPodman
            ? "http://host.containers.internal:8033/v1"
            : "http://host-gateway:8033/v1";

        var psi = new ProcessStartInfo(_containerBinary)
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };

        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add("--rm");
        psi.ArgumentList.Add("--name"); psi.ArgumentList.Add(ContainerName);
        psi.ArgumentList.Add("-p");     psi.ArgumentList.Add($"{port}:8080");

        if (cuda)
        {
            psi.ArgumentList.Add("--gpus");
            psi.ArgumentList.Add("all");
        }

        if (!_isPodman)
        {
            psi.ArgumentList.Add("--add-host");
            psi.ArgumentList.Add("host-gateway:host-gateway");
        }

        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "dummy";

        psi.ArgumentList.Add("-e"); psi.ArgumentList.Add($"OPENAI_API_BASE_URL={apiBaseUrl}");
        psi.ArgumentList.Add("-e"); psi.ArgumentList.Add($"OPENAI_API_KEY={apiKey}");
        psi.ArgumentList.Add("-v"); psi.ArgumentList.Add("open-webui:/app/backend/data");
        psi.ArgumentList.Add(image);

        try
        {
            AnsiConsole.MarkupLine(cuda
                ? "[dim]Starting Open WebUI container (CUDA)...[/]"
                : "[dim]Starting Open WebUI container...[/]");
            using var proc = Process.Start(psi);
            proc?.WaitForExit(10_000);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to start container: {ex.Message}[/]");
        }
    }

    // ── Health check ─────────────────────────────────────────────────────────

    private static async Task WaitForHealthAsync(int port)
    {
        AnsiConsole.Markup("[dim]Waiting for Open WebUI[/]");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (int i = 0; i < HealthChecks; i++)
        {
            try
            {
                HttpResponseMessage resp = await http.GetAsync($"http://localhost:{port}/");
                if (resp.IsSuccessStatusCode || (int)resp.StatusCode < 500)
                {
                    AnsiConsole.MarkupLine(" [green]ready[/]");
                    return;
                }
            }
            catch { }

            AnsiConsole.Markup(".");
            await Task.Delay(1_000);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Health check timed out — Open WebUI may still be loading.[/]");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int ParseHostPort(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            // Try "8080/tcp" key first, then any key
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement binding in prop.Value.EnumerateArray())
                    {
                        if (binding.TryGetProperty("HostPort", out JsonElement hp))
                        {
                            if (int.TryParse(hp.GetString(), out int p))
                                return p;
                        }
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo(command)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            psi.ArgumentList.Add("--version");
            using var p = Process.Start(psi);
            p?.WaitForExit(3_000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }
}

