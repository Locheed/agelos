using Agelos.Cli.Models;
using Agelos.Cli.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Agelos.Tests.Services;

public class ConfigServiceTests
{
    private static readonly string ProjectPath = Path.Combine("fake", "project");
    private static readonly string ConfigPath  = Path.Combine(ProjectPath, ".agelos.yml");

    // ── LoadConfigAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoadConfigAsync_FileDoesNotExist_ReturnsNull()
    {
        var fs = FileServiceMock();
        fs.Setup(f => f.FileExistsAsync(ConfigPath)).ReturnsAsync(false);

        var result = await new ConfigService(fs.Object).LoadConfigAsync(ProjectPath);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadConfigAsync_ValidYaml_ReturnsDeserializedConfig()
    {
        var yaml = """
            version: "1.0"
            agent: opencode
            runtimes:
              node: "20"
            """;

        var fs = FileServiceMock();
        fs.Setup(f => f.FileExistsAsync(ConfigPath)).ReturnsAsync(true);
        fs.Setup(f => f.ReadAllTextAsync(ConfigPath)).ReturnsAsync(yaml);

        var result = await new ConfigService(fs.Object).LoadConfigAsync(ProjectPath);

        result.Should().NotBeNull();
        result!.Agent.Should().Be("opencode");
        result.Runtimes.Node.Should().Be("20");
    }

    [Fact]
    public async Task LoadConfigAsync_DotNetRuntimes_DeserializesVersionList()
    {
        var yaml = """
            version: "1.0"
            agent: opencode
            runtimes:
              dotNet:
                - "8"
                - "10"
            """;

        var fs = FileServiceMock();
        fs.Setup(f => f.FileExistsAsync(ConfigPath)).ReturnsAsync(true);
        fs.Setup(f => f.ReadAllTextAsync(ConfigPath)).ReturnsAsync(yaml);

        var result = await new ConfigService(fs.Object).LoadConfigAsync(ProjectPath);

        result!.Runtimes.DotNet.Should().HaveCount(2).And.Contain(["8", "10"]);
    }

    [Fact]
    public async Task LoadConfigAsync_UnknownFields_Ignored()
    {
        var yaml = """
            version: "1.0"
            agent: opencode
            unknownField: value
            runtimes: {}
            """;

        var fs = FileServiceMock();
        fs.Setup(f => f.FileExistsAsync(ConfigPath)).ReturnsAsync(true);
        fs.Setup(f => f.ReadAllTextAsync(ConfigPath)).ReturnsAsync(yaml);

        var act = async () => await new ConfigService(fs.Object).LoadConfigAsync(ProjectPath);

        await act.Should().NotThrowAsync();
    }

    // ── SaveConfigAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SaveConfigAsync_WritesYamlToConfigPath()
    {
        string? writtenPath    = null;
        string? writtenContent = null;

        var fs = FileServiceMock();
        fs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
          .Callback<string, string>((p, c) => { writtenPath = p; writtenContent = c; })
          .Returns(Task.CompletedTask);

        var config = new AgelosConfig { Agent = "opencode", Version = "1.0" };
        await new ConfigService(fs.Object).SaveConfigAsync(ProjectPath, config);

        writtenPath.Should().Be(ConfigPath);
        writtenContent.Should().NotBeNullOrWhiteSpace();
        writtenContent.Should().Contain("opencode");
    }

    [Fact]
    public async Task SaveConfigAsync_ThenLoadConfigAsync_RoundTrips()
    {
        var stored = new Dictionary<string, string>();

        var fs = FileServiceMock();
        fs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
          .Callback<string, string>((p, c) => stored[p] = c)
          .Returns(Task.CompletedTask);
        fs.Setup(f => f.FileExistsAsync(ConfigPath)).ReturnsAsync(() => stored.ContainsKey(ConfigPath));
        fs.Setup(f => f.ReadAllTextAsync(ConfigPath)).ReturnsAsync(() => stored[ConfigPath]);

        var svc    = new ConfigService(fs.Object);
        var config = new AgelosConfig
        {
            Version  = "1.0",
            Agent    = "aider",
            Runtimes = new RuntimeRequirements { Python = "3.12" }
        };

        await svc.SaveConfigAsync(ProjectPath, config);
        var loaded = await svc.LoadConfigAsync(ProjectPath);

        loaded.Should().NotBeNull();
        loaded!.Agent.Should().Be("aider");
        loaded.Runtimes.Python.Should().Be("3.12");
    }

    // ── CreateProjectConfigAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CreateProjectConfigAsync_WritesConfigAndCreatesGitignore()
    {
        var written = new Dictionary<string, string>();

        var fs = FileServiceMock();
        fs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
          .Callback<string, string>((p, c) => written[p] = c)
          .Returns(Task.CompletedTask);
        fs.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var runtimes = new RuntimeRequirements { Node = "20" };
        await new ConfigService(fs.Object)
            .CreateProjectConfigAsync(ProjectPath, runtimes, "opencode");

        written.Should().ContainKey(ConfigPath);
        written.Should().ContainKey(Path.Combine(ProjectPath, ".gitignore"));
    }

    [Fact]
    public async Task CreateProjectConfigAsync_DotNetRuntimes_GitignoreContainsBinObj()
    {
        var written = new Dictionary<string, string>();

        var fs = FileServiceMock();
        fs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
          .Callback<string, string>((p, c) => written[p] = c)
          .Returns(Task.CompletedTask);
        fs.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var runtimes = new RuntimeRequirements { DotNet = ["10"] };
        await new ConfigService(fs.Object)
            .CreateProjectConfigAsync(ProjectPath, runtimes, "opencode");

        written[Path.Combine(ProjectPath, ".gitignore")].Should()
            .Contain("bin/").And.Contain("obj/");
    }

    [Fact]
    public async Task CreateProjectConfigAsync_GitignoreAlreadyExists_DoesNotOverwrite()
    {
        var writeCount = 0;

        var fs = FileServiceMock();
        // gitignore exists, config does not
        fs.Setup(f => f.FileExistsAsync(ConfigPath)).ReturnsAsync(false);
        fs.Setup(f => f.FileExistsAsync(Path.Combine(ProjectPath, ".gitignore"))).ReturnsAsync(true);
        fs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
          .Callback<string, string>((_, _) => writeCount++)
          .Returns(Task.CompletedTask);

        await new ConfigService(fs.Object)
            .CreateProjectConfigAsync(ProjectPath, new RuntimeRequirements(), "opencode");

        // Only the .agelos.yml write, not the gitignore
        writeCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateProjectConfigAsync_PythonRuntimes_GitignoreContainsPycache()
    {
        var written = new Dictionary<string, string>();

        var fs = FileServiceMock();
        fs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
          .Callback<string, string>((p, c) => written[p] = c)
          .Returns(Task.CompletedTask);
        fs.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var runtimes = new RuntimeRequirements { Python = "3.12" };
        await new ConfigService(fs.Object)
            .CreateProjectConfigAsync(ProjectPath, runtimes, "opencode");

        written[Path.Combine(ProjectPath, ".gitignore")].Should().Contain("__pycache__/");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Mock<IFileService> FileServiceMock()
    {
        var fs = new Mock<IFileService>();
        fs.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        fs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
          .Returns(Task.CompletedTask);
        return fs;
    }
}

