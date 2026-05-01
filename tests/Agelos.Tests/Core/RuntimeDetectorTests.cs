using Agelos.Cli.Core;
using Agelos.Cli.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Agelos.Tests.Core;

/// <summary>
/// Tests for RuntimeDetector. Node/Python/Go/Rust detection uses IFileService (mockable).
/// DotNet detection and IsEmptyProjectAsync use Directory.GetFiles directly, so they use a
/// real temp directory.
/// </summary>
public class RuntimeDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public RuntimeDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agelos-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Node detection ───────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_PackageJsonWithEngines_ExtractsNodeVersion()
    {
        var fs = MockFileService();
        SetupFile(fs, "package.json", """{ "engines": { "node": ">=20" } }""");

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Node.Should().Be("20");
    }

    [Fact]
    public async Task DetectAsync_PackageJsonWithoutEngines_DefaultsTo20()
    {
        var fs = MockFileService();
        SetupFile(fs, "package.json", """{ "name": "my-app" }""");

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Node.Should().Be("20");
    }

    [Fact]
    public async Task DetectAsync_NoPackageJson_NodeIsNull()
    {
        var fs = MockFileService();
        // package.json does not exist

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Node.Should().BeNull();
    }

    // ── Python detection ─────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_PyprojectTomlWithPythonVersion_ExtractsPythonVersion()
    {
        var fs = MockFileService();
        SetupFile(fs, "pyproject.toml", """
            [tool.poetry.dependencies]
            python = ">=3.11"
            """);

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Python.Should().Be("3.11");
    }

    [Fact]
    public async Task DetectAsync_PyprojectTomlWithoutVersion_DefaultsTo312()
    {
        var fs = MockFileService();
        SetupFile(fs, "pyproject.toml", "[build-system]\nrequires = []");

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Python.Should().Be("3.12");
    }

    [Fact]
    public async Task DetectAsync_NoPyprojectToml_PythonIsNull()
    {
        var fs = MockFileService();

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Python.Should().BeNull();
    }

    // ── Go detection ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_GoModWithVersion_ExtractsGoVersion()
    {
        var fs = MockFileService();
        SetupFile(fs, "go.mod", "module example.com/mymod\n\ngo 1.22\n");

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Go.Should().Be("1.22");
    }

    [Fact]
    public async Task DetectAsync_GoModWithoutVersion_DefaultsTo122()
    {
        var fs = MockFileService();
        SetupFile(fs, "go.mod", "module example.com/mymod\n");

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Go.Should().Be("1.22");
    }

    [Fact]
    public async Task DetectAsync_NoGoMod_GoIsNull()
    {
        var fs = MockFileService();

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Go.Should().BeNull();
    }

    // ── Rust detection ───────────────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_CargoTomlExists_SetsRustTrue()
    {
        var fs = MockFileService();
        SetupFileExists(fs, "Cargo.toml");

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Rust.Should().BeTrue();
    }

    [Fact]
    public async Task DetectAsync_NoCargoToml_RustFalse()
    {
        var fs = MockFileService();

        var result = await new RuntimeDetector(fs.Object).DetectAsync(_tempDir);

        result.Rust.Should().BeFalse();
    }

    // ── DotNet detection (uses real temp files) ───────────────────────────────

    [Fact]
    public async Task DetectAsync_CsprojWithNet10_ReturnsDotNet10()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var result = await new RuntimeDetector(new FileService()).DetectAsync(_tempDir);

        result.DotNet.Should().ContainSingle().Which.Should().Be("10");
    }

    [Fact]
    public async Task DetectAsync_CsprojWithMultiTargetFrameworks_ReturnsAll()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);

        var result = await new RuntimeDetector(new FileService()).DetectAsync(_tempDir);

        result.DotNet.Should().HaveCount(2).And.Contain(["8", "10"]);
    }

    [Fact]
    public async Task DetectAsync_TwoCsprojFiles_DeduplicatesVersions()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "A.csproj"),
            "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "B.csproj"),
            "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var result = await new RuntimeDetector(new FileService()).DetectAsync(_tempDir);

        result.DotNet.Should().ContainSingle().Which.Should().Be("10");
    }

    [Fact]
    public async Task DetectAsync_NoCsproj_DotNetIsNull()
    {
        var result = await new RuntimeDetector(new FileService()).DetectAsync(_tempDir);
        result.DotNet.Should().BeNullOrEmpty();
    }

    // ── IsEmptyProjectAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task IsEmptyProjectAsync_EmptyDirectory_ReturnsTrue()
    {
        var result = await new RuntimeDetector(new FileService()).IsEmptyProjectAsync(_tempDir);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmptyProjectAsync_OnlyIgnoredFiles_ReturnsTrue()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".gitignore"), "");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "README.md"), "");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "LICENSE"), "");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, ".agelos.yml"), "");

        var result = await new RuntimeDetector(new FileService()).IsEmptyProjectAsync(_tempDir);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmptyProjectAsync_HasSourceFile_ReturnsFalse()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Program.cs"), "");

        var result = await new RuntimeDetector(new FileService()).IsEmptyProjectAsync(_tempDir);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEmptyProjectAsync_HasSubDirectory_ReturnsFalse()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));

        var result = await new RuntimeDetector(new FileService()).IsEmptyProjectAsync(_tempDir);
        result.Should().BeFalse();
    }

    // ── Multi-runtime detection ───────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_FullStack_DetectsAllRuntimes()
    {
        // .csproj on real FS
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "App.csproj"),
            "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var fs = MockFileService();
        SetupFile(fs, "package.json", """{ "engines": { "node": ">=20" } }""");
        SetupFile(fs, "pyproject.toml", """python = ">=3.12" """);
        SetupFile(fs, "go.mod", "module x\n\ngo 1.22");
        SetupFileExists(fs, "Cargo.toml");

        // Use real FileService for .csproj but mock for the rest  →
        // build a wrapper that delegates file reads to real FS and existence checks to mock
        // Simplest: use real FileService and write real files
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "package.json"), """{ "engines": { "node": ">=20" } }""");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "pyproject.toml"), """python = ">=3.12" """);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "go.mod"), "module x\n\ngo 1.22");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Cargo.toml"), "[package]");

        var result = await new RuntimeDetector(new FileService()).DetectAsync(_tempDir);

        result.DotNet.Should().ContainSingle().Which.Should().Be("10");
        result.Node.Should().Be("20");
        result.Python.Should().Be("3.12");
        result.Go.Should().Be("1.22");
        result.Rust.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Mock<IFileService> MockFileService()
    {
        var fs = new Mock<IFileService>();
        // Default: no file exists
        fs.Setup(f => f.FileExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        return fs;
    }

    private void SetupFile(Mock<IFileService> fs, string fileName, string content)
    {
        var fullPath = Path.Combine(_tempDir, fileName);
        fs.Setup(f => f.FileExistsAsync(fullPath)).ReturnsAsync(true);
        fs.Setup(f => f.ReadAllTextAsync(fullPath)).ReturnsAsync(content);
    }

    private void SetupFileExists(Mock<IFileService> fs, string fileName)
    {
        var fullPath = Path.Combine(_tempDir, fileName);
        fs.Setup(f => f.FileExistsAsync(fullPath)).ReturnsAsync(true);
    }
}

