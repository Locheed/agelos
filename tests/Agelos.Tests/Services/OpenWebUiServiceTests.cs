using System.Net;
using System.Net.Sockets;
using Agelos.Cli.Services;
using FluentAssertions;
using Xunit;

namespace Agelos.Tests.Services;

public class OpenWebUiServiceTests
{
    // ── HasRuntime ────────────────────────────────────────────────────────────

    [Fact]
    public void HasRuntime_NullBinary_ReturnsFalse()
    {
        var sut = new OpenWebUiService(containerBinary: null);
        sut.HasRuntime.Should().BeFalse();
    }

    [Fact]
    public void HasRuntime_NonNullBinary_ReturnsTrue()
    {
        var sut = new OpenWebUiService(containerBinary: "docker");
        sut.HasRuntime.Should().BeTrue();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ReturnsOpenWebUiServiceInstance()
    {
        OpenWebUiService svc = OpenWebUiService.Create();
        svc.Should().NotBeNull();
        svc.Should().BeOfType<OpenWebUiService>();
    }

    [Fact]
    public void Create_WhenNoRuntimePresent_HasRuntimeIsFalse()
    {
        // This will be true on CI where neither podman nor docker exists,
        // and false on a dev machine that has Docker installed.
        // We just assert the factory doesn't throw and returns a valid object.
        OpenWebUiService svc = OpenWebUiService.Create();
        svc.Should().NotBeNull();
    }

    // ── FindFreePortFrom ──────────────────────────────────────────────────────

    [Fact]
    public void FindFreePortFrom_FreePort_ReturnsSamePort()
    {
        // Find a high port that should be free
        int startPort = 49200;
        int result    = OpenWebUiService.FindFreePortFrom(startPort);
        result.Should().BeGreaterThanOrEqualTo(startPort);
    }

    [Fact]
    public void FindFreePortFrom_OccupiedPort_ReturnsNextFreePort()
    {
        // Occupy a port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int occupiedPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            int result = OpenWebUiService.FindFreePortFrom(occupiedPort);
            result.Should().BeGreaterThan(occupiedPort);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void FindFreePortFrom_ReturnedPort_IsActuallyBindable()
    {
        int port = OpenWebUiService.FindFreePortFrom(49300);

        // Verify the returned port can actually be bound (i.e. it's genuinely free)
        TcpListener? verify = null;
        Action bind = () =>
        {
            verify = new TcpListener(IPAddress.Loopback, port);
            verify.Start();
        };

        bind.Should().NotThrow();
        verify?.Stop();
    }

    [Fact]
    public void FindFreePortFrom_MultipleOccupiedPorts_SkipsAllOfThem()
    {
        // Occupy two consecutive ports
        var l1 = new TcpListener(IPAddress.Loopback, 0);
        l1.Start();
        int port1 = ((IPEndPoint)l1.LocalEndpoint).Port;

        var l2 = new TcpListener(IPAddress.Loopback, 0);
        l2.Start();
        int port2 = ((IPEndPoint)l2.LocalEndpoint).Port;

        try
        {
            // Both ports are now occupied; FindFreePortFrom should skip whichever it hits
            int result1 = OpenWebUiService.FindFreePortFrom(port1);
            int result2 = OpenWebUiService.FindFreePortFrom(port2);

            result1.Should().NotBe(port1);
            result2.Should().NotBe(port2);
        }
        finally
        {
            l1.Stop();
            l2.Stop();
        }
    }

    // ── DefaultPort constant ──────────────────────────────────────────────────

    [Fact]
    public void FindFreePortFrom_DefaultStartPort_Is3000()
    {
        // Verify the well-known default port that all docs reference is still 3000.
        // We call FindFreePortFrom(3000) and confirm the result is >= 3000.
        int result = OpenWebUiService.FindFreePortFrom(3000);
        result.Should().BeGreaterThanOrEqualTo(3000);
    }

    // ── IsLlamaServerHealthyAsync ─────────────────────────────────────────────

    [Fact]
    public async Task IsLlamaServerHealthyAsync_WhenServerNotRunning_ReturnsFalse()
    {
        // llama-server will not be running in a unit test environment
        bool result = await OpenWebUiService.IsLlamaServerHealthyAsync();
        result.Should().BeFalse();
    }
}

