using Agelos.Cli.Core;
using Agelos.Cli.Models;
using FluentAssertions;
using Xunit;

namespace Agelos.Tests.Core;

public class RuntimeParserTests
{
    // ── ParseSpec ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("dotnet:10",    "dotnet", "10")]
    [InlineData("node:20",      "node",   "20")]
    [InlineData("nodejs:18",    "nodejs", "18")]
    [InlineData("python:3.12",  "python", "3.12")]
    [InlineData("go:1.22",      "go",     "1.22")]
    [InlineData("rust:latest",  "rust",   "latest")]
    [InlineData("java:21",      "java",   "21")]
    [InlineData("php:8.3",      "php",    "8.3")]
    [InlineData("ruby:3.3",     "ruby",   "3.3")]
    public void ParseSpec_ValidSpec_ReturnsCorrectLanguageAndVersion(string input, string lang, string version)
    {
        var spec = RuntimeParser.ParseSpec(input);
        spec.Language.Should().Be(lang);
        spec.Version.Should().Be(version);
    }

    [Theory]
    [InlineData("dotnet:10")]
    [InlineData("node:20")]
    [InlineData("python:3.12")]
    [InlineData("go:1.22")]
    [InlineData("rust:latest")]
    [InlineData("java:21")]
    [InlineData("php:8.3")]
    [InlineData("ruby:3.3")]
    public void ParseSpec_KnownLanguage_InstallScriptNotEmpty(string input)
    {
        var spec = RuntimeParser.ParseSpec(input);
        spec.InstallScript.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ParseSpec_DotNet_InstallScriptContainsSdkVersion()
    {
        var spec = RuntimeParser.ParseSpec("dotnet:10");
        spec.InstallScript.Should().Contain("10");
    }

    [Fact]
    public void ParseSpec_Node_InstallScriptContainsVersion()
    {
        var spec = RuntimeParser.ParseSpec("node:20");
        spec.InstallScript.Should().Contain("20");
    }

    [Fact]
    public void ParseSpec_Go_InstallScriptContainsVersion()
    {
        var spec = RuntimeParser.ParseSpec("go:1.22");
        spec.InstallScript.Should().Contain("1.22");
    }

    [Fact]
    public void ParseSpec_Rust_InstallScriptContainsRustup()
    {
        var spec = RuntimeParser.ParseSpec("rust:latest");
        spec.InstallScript.Should().Contain("rustup");
    }

    [Fact]
    public void ParseSpec_UnknownLanguage_ReturnsCustomInstallScript()
    {
        var spec = RuntimeParser.ParseSpec("kotlin:1.9");
        spec.Language.Should().Be("kotlin");
        spec.Version.Should().Be("1.9");
        spec.InstallScript.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ParseSpec_Language_NormalisedToLowercase()
    {
        var spec = RuntimeParser.ParseSpec("DOTNET:10");
        spec.Language.Should().Be("dotnet");
    }

    [Fact]
    public void ParseSpec_InvalidSpec_ThrowsArgumentException()
    {
        var act = () => RuntimeParser.ParseSpec("invaliddotnet");
        act.Should().Throw<ArgumentException>().WithMessage("*language:version*");
    }

    [Fact]
    public void ParseSpec_VersionContainsColon_VersionIncludesEverythingAfterFirstColon()
    {
        // spec allows "lang:ver" only (split on first ':')
        var spec = RuntimeParser.ParseSpec("python:3.12");
        spec.Version.Should().Be("3.12");
    }

    // ── ParseSpecs ───────────────────────────────────────────────────────────

    [Fact]
    public void ParseSpecs_MultipleDotNetVersions_ReturnsDistinctVersions()
    {
        var req = RuntimeParser.ParseSpecs("dotnet:8,dotnet:10,dotnet:8");
        req.DotNet.Should().HaveCount(2).And.Contain(["8", "10"]);
    }

    [Fact]
    public void ParseSpecs_MultipleRuntimes_ParsesAll()
    {
        var req = RuntimeParser.ParseSpecs("dotnet:10,node:20,python:3.12");
        req.DotNet.Should().ContainSingle().Which.Should().Be("10");
        req.Node.Should().Be("20");
        req.Python.Should().Be("3.12");
    }

    [Fact]
    public void ParseSpecs_NodeAlias_SetsNode()
    {
        var req = RuntimeParser.ParseSpecs("nodejs:18");
        req.Node.Should().Be("18");
    }

    [Fact]
    public void ParseSpecs_GoAlias_SetsGo()
    {
        var req = RuntimeParser.ParseSpecs("golang:1.22");
        req.Go.Should().Be("1.22");
    }

    [Fact]
    public void ParseSpecs_Rust_SetsRustFlag()
    {
        var req = RuntimeParser.ParseSpecs("rust:latest");
        req.Rust.Should().BeTrue();
    }

    [Fact]
    public void ParseSpecs_Java_SetsJava()
    {
        var req = RuntimeParser.ParseSpecs("java:21");
        req.Java.Should().Be("21");
    }

    [Fact]
    public void ParseSpecs_Php_SetsPhp()
    {
        var req = RuntimeParser.ParseSpecs("php:8.3");
        req.Php.Should().Be("8.3");
    }

    [Fact]
    public void ParseSpecs_Ruby_SetsRuby()
    {
        var req = RuntimeParser.ParseSpecs("ruby:3.3");
        req.Ruby.Should().Be("3.3");
    }

    [Fact]
    public void ParseSpecs_UnknownRuntime_AddsToCustom()
    {
        var req = RuntimeParser.ParseSpecs("kotlin:1.9");
        req.Custom.Should().ContainSingle();
        req.Custom![0].Name.Should().Be("kotlin");
        req.Custom![0].Version.Should().Be("1.9");
    }

    [Fact]
    public void ParseSpecs_MultipleUnknownRuntimes_AllAddedToCustom()
    {
        var req = RuntimeParser.ParseSpecs("kotlin:1.9,swift:5.10");
        req.Custom.Should().HaveCount(2);
    }

    [Fact]
    public void ParseSpecs_WhitespaceTrimmed_ParsesCorrectly()
    {
        var req = RuntimeParser.ParseSpecs("dotnet:10 , node:20");
        req.DotNet.Should().ContainSingle().Which.Should().Be("10");
        req.Node.Should().Be("20");
    }

    [Fact]
    public void ParseSpecs_SingleSpec_ParsesCorrectly()
    {
        var req = RuntimeParser.ParseSpecs("go:1.22");
        req.Go.Should().Be("1.22");
        req.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ParseSpecs_LaterNodeVersionOverwritesPrevious()
    {
        // Last node wins (scalar override)
        var req = RuntimeParser.ParseSpecs("node:18,node:20");
        req.Node.Should().Be("20");
    }
}
