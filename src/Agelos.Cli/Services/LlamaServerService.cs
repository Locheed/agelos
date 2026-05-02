using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace Agelos.Cli.Services;

public class LlamaServerService
{
    private const int    Port                 = 8033;
    private const string BinaryName           = "llama-server";
    private const string ContainerName        = "agelos-llama-server";
    private const string ContainerImage       = "ghcr.io/ggerganov/llama.cpp:server";
    private const string ContainerImageCuda   = "ghcr.io/ggerganov/llama.cpp:server-cuda";
    private const string ContainerImageVulkan = "ghcr.io/ggerganov/llama.cpp:server-vulkan";
    private const string ModelsMount          = "/models";
    private const int    HealthChecks         = 30;

    // The container binary to use when llama-server is not in PATH ("podman" or "docker").
    private readonly string? _containerBinary;

    public LlamaServerService(string? containerBinary = null)
    {
        _containerBinary = containerBinary;
    }

    // ── Public factory ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a LlamaServerService with automatic container-runtime detection.
    /// Checks Podman first, then Docker. If neither is found the service will
    /// still work as long as llama-server is present natively.
    /// </summary>
    public static LlamaServerService Create()
    {
        foreach (var bin in new[] { "podman", "docker" })
        {
            if (IsCommandAvailable(bin))
                return new LlamaServerService(bin);
        }
        return new LlamaServerService(containerBinary: null);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task RestartAsync(string modelPath, int contextSize)
    {
        if (IsNativeAvailable())
        {
            AnsiConsole.MarkupLine("[dim]llama-server found in PATH — using native binary.[/]");
            KillNative();
            StartNative(modelPath, contextSize);
        }
        else if (_containerBinary != null)
        {
            AnsiConsole.MarkupLine($"[dim]llama-server not in PATH — starting container via {_containerBinary}.[/]");
            await StopContainerAsync();
            StartContainer(modelPath, contextSize);
        }
        else
        {
            AnsiConsole.MarkupLine("[red]llama-server not found and no container runtime available.[/]");
            AnsiConsole.MarkupLine("[yellow]Install llama-server (https://github.com/ggml-org/llama.cpp) or ensure Podman/Docker is in PATH.[/]");
            AnsiConsole.MarkupLine($"[dim]Manual: llama-server -m \"{modelPath}\" --port {Port} --ctx-size {contextSize} -ngl 999[/]");
            return;
        }

        await WaitForHealthAsync();
    }

    // ── Native ───────────────────────────────────────────────────────────────

    public static bool IsNativeAvailable() => IsCommandAvailable(BinaryName);

    private static void KillNative()
    {
        var procs = Process.GetProcessesByName(BinaryName);
        if (procs.Length == 0) return;

        AnsiConsole.MarkupLine($"[dim]Stopping {procs.Length} existing llama-server process(es)...[/]");
        foreach (var p in procs)
        {
            try { p.Kill(entireProcessTree: true); p.WaitForExit(3_000); }
            catch { /* already dead */ }
            finally { p.Dispose(); }
        }
    }

    private static void StartNative(string modelPath, int contextSize)
    {
        AnsiConsole.MarkupLine($"[dim]Starting llama-server: {Path.GetFileName(modelPath)}...[/]");

        var psi = new ProcessStartInfo(BinaryName)
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = false,
            RedirectStandardError  = false,
        };
        psi.ArgumentList.Add("-m");         psi.ArgumentList.Add(modelPath);
        psi.ArgumentList.Add("--port");     psi.ArgumentList.Add(Port.ToString());
        psi.ArgumentList.Add("--ctx-size"); psi.ArgumentList.Add(contextSize.ToString());
        psi.ArgumentList.Add("-ngl");       psi.ArgumentList.Add("999");

        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to start llama-server: {ex.Message}[/]");
        }
    }

    // ── Containerised ────────────────────────────────────────────────────────

    private async Task StopContainerAsync()
    {
        try
        {
            var inspectPsi = new ProcessStartInfo(_containerBinary!)
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
            if (inspect.ExitCode != 0) return; // container doesn't exist

            AnsiConsole.MarkupLine($"[dim]Stopping existing {ContainerName} container...[/]");

            var stopPsi = new ProcessStartInfo(_containerBinary!)
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
        catch { /* container not running — that's fine */ }
    }

    private void StartContainer(string hostModelPath, int contextSize)
    {
        string modelsDir      = Path.GetDirectoryName(hostModelPath)!;
        string modelFile      = Path.GetFileName(hostModelPath);
        string containerModel = $"{ModelsMount}/{modelFile}";

        GpuMode gpu   = DetectGpu();
        string  image = gpu switch
        {
            GpuMode.Cuda   => ContainerImageCuda,
            GpuMode.Vulkan => ContainerImageVulkan,
            _              => ContainerImage,
        };

        AnsiConsole.MarkupLine($"[dim]Pulling {image} if needed (first run only)...[/]");

        // Run detached, auto-remove on stop, named so we can stop it later.
        var psi = new ProcessStartInfo(_containerBinary!)
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
        psi.ArgumentList.Add("-p");     psi.ArgumentList.Add($"{Port}:8080");
        psi.ArgumentList.Add("-v");     psi.ArgumentList.Add($"{modelsDir}:{ModelsMount}:ro");

        switch (gpu)
        {
            case GpuMode.Cuda:
                psi.ArgumentList.Add("--gpus");
                psi.ArgumentList.Add("all");
                break;
            case GpuMode.Vulkan:
                psi.ArgumentList.Add("--device");
                psi.ArgumentList.Add("/dev/dri");
                break;
        }

        psi.ArgumentList.Add(image);
        psi.ArgumentList.Add("--model");    psi.ArgumentList.Add(containerModel);
        psi.ArgumentList.Add("--port");     psi.ArgumentList.Add("8080");
        psi.ArgumentList.Add("--host");     psi.ArgumentList.Add("0.0.0.0");
        psi.ArgumentList.Add("--ctx-size"); psi.ArgumentList.Add(contextSize.ToString());
        psi.ArgumentList.Add("-ngl");       psi.ArgumentList.Add("999");

        try
        {
            string gpuLabel = gpu switch
            {
                GpuMode.Cuda   => " (CUDA)",
                GpuMode.Vulkan => " (Vulkan)",
                _              => " (CPU)",
            };
            AnsiConsole.MarkupLine($"[dim]Starting containerised llama-server: {modelFile}{gpuLabel}...[/]");
            using var proc = Process.Start(psi);
            proc?.WaitForExit(10_000);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to start container: {ex.Message}[/]");
        }
    }

    // ── GPU detection ─────────────────────────────────────────────────────────

    public enum GpuMode { None, Cuda, Vulkan }

    /// <summary>
    /// Probes the host for GPU capabilities in priority order:
    ///   1. NVIDIA CUDA  — nvidia-smi present and exits 0
    ///   2. Vulkan       — vulkaninfo present and exits 0 (AMD/Intel/NVIDIA on Linux/WSL)
    ///   3. None         — CPU fallback
    /// </summary>
    public static GpuMode DetectGpu()
    {
        if (IsCommandAvailable("nvidia-smi"))
        {
            bool toolkitAvailable = IsCommandAvailable("nvidia-container-cli")
                                 || IsCommandAvailable("nvidia-ctk");

            if (toolkitAvailable)
            {
                AnsiConsole.MarkupLine("[dim]NVIDIA GPU + container toolkit detected — using CUDA image.[/]");
                return GpuMode.Cuda;
            }

            AnsiConsole.MarkupLine("[yellow]Warning:[/] NVIDIA GPU detected but nvidia-container-toolkit is not installed.");
            AnsiConsole.MarkupLine("[dim]CUDA container acceleration is unavailable — falling back to Vulkan.[/]");
            AnsiConsole.MarkupLine("[dim]To enable CUDA: https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html[/]");
            AnsiConsole.MarkupLine("[dim]Tip: install llama-server natively for full GPU performance without any toolkit.[/]");
            // Fall through — NVIDIA GPUs also support Vulkan
        }

        if (IsCommandAvailable("vulkaninfo"))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Vulkan GPU detected but Windows container runtimes (Docker Desktop,");
                AnsiConsole.MarkupLine("[dim]Podman Desktop/Machine) do not support /dev/dri passthrough — Vulkan container[/]");
                AnsiConsole.MarkupLine("[dim]acceleration is unavailable on Windows. Falling back to CPU image.[/]");
                AnsiConsole.MarkupLine("[dim]For GPU acceleration, install llama-server natively (no container overhead, works on Windows).[/]");
                // Fall through to CPU
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Vulkan-capable GPU detected — using Vulkan image.[/]");
                return GpuMode.Vulkan;
            }
        }

        AnsiConsole.MarkupLine("[dim]No GPU detected — using CPU image (inference will be slower).[/]");
        return GpuMode.None;
    }

    // ── Health check ─────────────────────────────────────────────────────────

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
            await Task.Delay(1_000);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Health check timed out — server may still be loading.[/]");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
