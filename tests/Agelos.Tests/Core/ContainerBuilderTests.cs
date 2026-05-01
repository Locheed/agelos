using Agelos.Cli.Core;
using Agelos.Cli.Models;
using Agelos.Cli.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Agelos.Tests.Core;

public class ContainerBuilderTests
{
    private readonly ContainerBuilder _sut;

    public ContainerBuilderTests()
    {
        _sut = new ContainerBuilder(new Mock<IFileService>().Object);
    }

    // ── GenerateImageTag ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateImageTag_AgentOnly_ReturnsAgentTag()
    {
        var tag = _sut.GenerateImageTag("opencode", new RuntimeRequirements());
        tag.Should().Be("agelos/opencode");
    }

    [Fact]
    public void GenerateImageTag_WithDotNet_IncludesDotNetVersion()
    {
        var tag = _sut.GenerateImageTag("opencode", new RuntimeRequirements { DotNet = ["10"] });
        tag.Should().Be("agelos/opencode-dotnet10");
    }

    [Fact]
    public void GenerateImageTag_WithMultipleDotNetVersions_JoinedWithDash()
    {
        var tag = _sut.GenerateImageTag("opencode", new RuntimeRequirements { DotNet = ["8", "10"] });
        tag.Should().Be("agelos/opencode-dotnet8-10");
    }

    [Fact]
    public void GenerateImageTag_WithNode_IncludesNodeVersion()
    {
        var tag = _sut.GenerateImageTag("aider", new RuntimeRequirements { Node = "20" });
        tag.Should().Be("agelos/aider-node20");
    }

    [Fact]
    public void GenerateImageTag_WithPython_DotsRemovedFromVersion()
    {
        var tag = _sut.GenerateImageTag("aider", new RuntimeRequirements { Python = "3.12" });
        tag.Should().Be("agelos/aider-py312");
    }

    [Fact]
    public void GenerateImageTag_WithGo_DotsRemovedFromVersion()
    {
        var tag = _sut.GenerateImageTag("opencode", new RuntimeRequirements { Go = "1.22" });
        tag.Should().Be("agelos/opencode-go122");
    }

    [Fact]
    public void GenerateImageTag_WithRust_IncludesRust()
    {
        var tag = _sut.GenerateImageTag("opencode", new RuntimeRequirements { Rust = true });
        tag.Should().Be("agelos/opencode-rust");
    }

    [Fact]
    public void GenerateImageTag_WithCustomRuntime_IncludesNameAndVersion()
    {
        var req = new RuntimeRequirements
        {
            Custom = [new CustomRuntime("kotlin", "1.9")]
        };
        var tag = _sut.GenerateImageTag("opencode", req);
        tag.Should().Be("agelos/opencode-kotlin19");
    }

    [Fact]
    public void GenerateImageTag_WithAddOns_IncludesAddOnNames()
    {
        var tag = _sut.GenerateImageTag("opencode", new RuntimeRequirements(), ["llama-cpp"]);
        tag.Should().Be("agelos/opencode-llama-cpp");
    }

    [Fact]
    public void GenerateImageTag_AllRuntimes_CombinesAllParts()
    {
        var req = new RuntimeRequirements
        {
            DotNet = ["10"],
            Node   = "20",
            Python = "3.12",
            Go     = "1.22",
            Rust   = true,
        };
        var tag = _sut.GenerateImageTag("opencode", req);
        tag.Should().StartWith("agelos/opencode-");
        tag.Should().Contain("dotnet10");
        tag.Should().Contain("node20");
        tag.Should().Contain("py312");
        tag.Should().Contain("go122");
        tag.Should().Contain("rust");
    }

    [Fact]
    public void GenerateImageTag_MultipleAddOns_AllIncluded()
    {
        var tag = _sut.GenerateImageTag("opencode", new RuntimeRequirements(), ["llama-cpp", "extra"]);
        tag.Should().Contain("llama-cpp");
        tag.Should().Contain("extra");
    }

    [Fact]
    public void GenerateImageTag_NullAddOns_SameAsNoAddOns()
    {
        var tagNull = _sut.GenerateImageTag("opencode", new RuntimeRequirements(), null);
        var tagNone = _sut.GenerateImageTag("opencode", new RuntimeRequirements());
        tagNull.Should().Be(tagNone);
    }

    // ── BuildCustomImageAsync — cached path ──────────────────────────────────

    [Fact]
    public async Task BuildCustomImageAsync_ImageAlreadyExists_ReturnsCachedTagWithoutBuilding()
    {
        var runtime = new Mock<IContainerRuntime>();
        runtime.Setup(r => r.ImageExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var result = await _sut.BuildCustomImageAsync(
            "opencode", new RuntimeRequirements(), null, runtime.Object);

        result.Should().Be("agelos/opencode");
        runtime.Verify(r => r.BuildAsync(It.IsAny<BuildOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildCustomImageAsync_ImageMissing_CallsBuildAsync()
    {
        var fileService = new Mock<IFileService>();
        fileService.Setup(f => f.CreateDirectoryAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        fileService.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var runtime = new Mock<IContainerRuntime>();
        runtime.Setup(r => r.ImageExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(false);
        runtime.Setup(r => r.BuildAsync(It.IsAny<BuildOptions>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var builder = new ContainerBuilder(fileService.Object);
        var result = await builder.BuildCustomImageAsync(
            "opencode", new RuntimeRequirements(), null, runtime.Object);

        result.Should().Be("agelos/opencode");
        runtime.Verify(r => r.BuildAsync(
            It.Is<BuildOptions>(o => o.Tag == "agelos/opencode"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

