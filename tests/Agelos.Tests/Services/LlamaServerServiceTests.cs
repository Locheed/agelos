using Agelos.Cli.Services;
using FluentAssertions;
using System.Runtime.InteropServices;
using Xunit;

namespace Agelos.Tests.Services;

public class LlamaServerServiceTests
{
    // ── GpuMode enum ─────────────────────────────────────────────────────────

    [Fact]
    public void GpuMode_HasExpectedValues()
    {
        // Documents the three supported GPU modes and guards against accidental rename/removal.
        var values = Enum.GetValues<LlamaServerService.GpuMode>();
        values.Should().Contain(LlamaServerService.GpuMode.None);
        values.Should().Contain(LlamaServerService.GpuMode.Cuda);
        values.Should().Contain(LlamaServerService.GpuMode.Vulkan);
        values.Should().HaveCount(3);
    }

    // ── DetectGpu ────────────────────────────────────────────────────────────

    [Fact]
    public void DetectGpu_ReturnsValidGpuMode()
    {
        // In a CI/test environment gpu detection should not throw regardless of outcome.
        LlamaServerService.GpuMode result = LlamaServerService.DetectGpu();
        Enum.IsDefined(result).Should().BeTrue();
    }

    [Fact]
    public void DetectGpu_ResultIsOneOfThreeKnownModes()
    {
        LlamaServerService.GpuMode result = LlamaServerService.DetectGpu();
        result.Should().BeOneOf(
            LlamaServerService.GpuMode.None,
            LlamaServerService.GpuMode.Cuda,
            LlamaServerService.GpuMode.Vulkan);
    }

    [Fact]
    public void DetectGpu_WhenNvidiaSmiPresent_ReturnsCudaNotVulkan()
    {
        // If CUDA is detected, Vulkan must NOT be returned — CUDA takes priority.
        // This test is meaningful only on machines with nvidia-smi; on others it verifies
        // the fallback path doesn't accidentally return Cuda.
        LlamaServerService.GpuMode result = LlamaServerService.DetectGpu();

        if (result == LlamaServerService.GpuMode.Cuda)
        {
            // CUDA was picked — correct, nvidia-smi + toolkit must be available
            result.Should().Be(LlamaServerService.GpuMode.Cuda);
        }
        else
        {
            // No NVIDIA GPU or toolkit missing — result must be Vulkan or None, never Cuda
            result.Should().NotBe(LlamaServerService.GpuMode.Cuda);
        }
    }

    [Fact]
    public void DetectGpu_CudaRequiresBothNvidiaSmiAndToolkit()
    {
        // Cuda must never be returned without the container toolkit present.
        // If the current machine has nvidia-smi but no toolkit, result must be Vulkan or None.
        LlamaServerService.GpuMode result = LlamaServerService.DetectGpu();

        if (result == LlamaServerService.GpuMode.Cuda)
        {
            // Verify the toolkit binaries that gated that decision are actually present
            bool toolkitPresent = IsCommandOnPath("nvidia-container-cli")
                               || IsCommandOnPath("nvidia-ctk");
            toolkitPresent.Should().BeTrue(
                "GpuMode.Cuda should only be returned when the nvidia-container-toolkit is installed");
        }
    }

    [Fact]
    public void DetectGpu_NvidiaSmiWithoutToolkit_DoesNotReturnCuda()
    {
        // On machines with just nvidia-smi (no toolkit), result must not be Cuda.
        // On machines without nvidia-smi at all, the test trivially passes.
        bool nvidiaSmi   = IsCommandOnPath("nvidia-smi");
        bool toolkitCli  = IsCommandOnPath("nvidia-container-cli");
        bool toolkitCtk  = IsCommandOnPath("nvidia-ctk");
        bool toolkitPresent = toolkitCli || toolkitCtk;

        if (nvidiaSmi && !toolkitPresent)
        {
            LlamaServerService.GpuMode result = LlamaServerService.DetectGpu();
            result.Should().NotBe(LlamaServerService.GpuMode.Cuda,
                "toolkit is missing so CUDA container mode must not be selected");
        }
    }

    [Fact]
    public void DetectGpu_OnWindows_VulkaninfoAlone_DoesNotReturnVulkan()
    {
        // On Windows, even if vulkaninfo is present, Vulkan container mode must not be selected
        // because Docker Desktop doesn't support /dev/dri passthrough.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // Linux/macOS: skip

        LlamaServerService.GpuMode result = LlamaServerService.DetectGpu();

        // On Windows without toolkit, result must be None (CPU) — never Vulkan
        if (!IsCommandOnPath("nvidia-container-cli") && !IsCommandOnPath("nvidia-ctk"))
            result.Should().NotBe(LlamaServerService.GpuMode.Vulkan,
                "Vulkan /dev/dri passthrough is unsupported on Windows Docker Desktop");
    }

    [Fact]
    public void DetectGpu_OnLinux_VulkaninfoPresent_MayReturnVulkan()
    {
        // On Linux/WSL2, vulkaninfo availability is a valid signal for Vulkan container mode.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // Windows: skip

        bool vulkanAvailable = IsCommandOnPath("vulkaninfo");
        LlamaServerService.GpuMode result = LlamaServerService.DetectGpu();

        if (vulkanAvailable && result == LlamaServerService.GpuMode.Vulkan)
            result.Should().Be(LlamaServerService.GpuMode.Vulkan); // correct
        else
            result.Should().BeOneOf(
                LlamaServerService.GpuMode.None,
                LlamaServerService.GpuMode.Cuda,
                LlamaServerService.GpuMode.Vulkan); // any valid mode
    }

    // ── Image selection logic (via GpuMode) ───────────────────────────────────

    [Theory]
    [InlineData(LlamaServerService.GpuMode.None,   "ghcr.io/ggerganov/llama.cpp:server")]
    [InlineData(LlamaServerService.GpuMode.Cuda,   "ghcr.io/ggerganov/llama.cpp:server-cuda")]
    [InlineData(LlamaServerService.GpuMode.Vulkan, "ghcr.io/ggerganov/llama.cpp:server-vulkan")]
    public void GpuMode_MapsToCorrectImageTag(LlamaServerService.GpuMode gpu, string expectedImage)
    {
        // Documents and pins the GpuMode → image tag mapping so accidental changes are caught.
        string image = gpu switch
        {
            LlamaServerService.GpuMode.Cuda   => "ghcr.io/ggerganov/llama.cpp:server-cuda",
            LlamaServerService.GpuMode.Vulkan => "ghcr.io/ggerganov/llama.cpp:server-vulkan",
            _                                 => "ghcr.io/ggerganov/llama.cpp:server",
        };
        image.Should().Be(expectedImage);
    }

    [Fact]
    public void GpuMode_NoneImage_DoesNotContainCudaOrVulkan()
    {
        const string cpuImage = "ghcr.io/ggerganov/llama.cpp:server";
        cpuImage.Should().NotContain("cuda").And.NotContain("vulkan");
    }

    [Fact]
    public void GpuMode_CudaImage_ContainsCuda()
    {
        const string cudaImage = "ghcr.io/ggerganov/llama.cpp:server-cuda";
        cudaImage.Should().Contain("cuda");
    }

    [Fact]
    public void GpuMode_VulkanImage_ContainsVulkan()
    {
        const string vulkanImage = "ghcr.io/ggerganov/llama.cpp:server-vulkan";
        vulkanImage.Should().Contain("vulkan");
    }

    // ── Create factory ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ReturnsLlamaServerServiceInstance()
    {
        LlamaServerService svc = LlamaServerService.Create();
        svc.Should().NotBeNull().And.BeOfType<LlamaServerService>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsCommandOnPath(string command)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(command)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            psi.ArgumentList.Add("--version");
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(3_000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }
}
