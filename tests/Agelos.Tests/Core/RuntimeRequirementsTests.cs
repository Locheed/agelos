using Agelos.Cli.Models;
using FluentAssertions;
using Xunit;

namespace Agelos.Tests.Core;

public class RuntimeRequirementsTests
{
    [Fact]
    public void IsEmpty_DefaultRecord_ReturnsTrue()
    {
        new RuntimeRequirements().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_EmptyCustomList_ReturnsTrue()
    {
        new RuntimeRequirements { Custom = [] }.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NonEmptyRequirements))]
    public void IsEmpty_AnyRuntimeSet_ReturnsFalse(RuntimeRequirements req)
    {
        req.IsEmpty.Should().BeFalse();
    }

    public static TheoryData<RuntimeRequirements> NonEmptyRequirements()
    {
        var data = new TheoryData<RuntimeRequirements>();
        data.Add(new RuntimeRequirements { DotNet = ["10"] });
        data.Add(new RuntimeRequirements { Node = "20" });
        data.Add(new RuntimeRequirements { Python = "3.12" });
        data.Add(new RuntimeRequirements { Go = "1.22" });
        data.Add(new RuntimeRequirements { Rust = true });
        data.Add(new RuntimeRequirements { Java = "21" });
        data.Add(new RuntimeRequirements { Php = "8.3" });
        data.Add(new RuntimeRequirements { Ruby = "3.3" });
        data.Add(new RuntimeRequirements { Custom = [new CustomRuntime("kotlin", "1.9")] });
        return data;
    }

    [Fact]
    public void IsEmpty_MultipleRuntimes_ReturnsFalse()
    {
        var req = new RuntimeRequirements { Node = "20", Python = "3.12", Rust = true };
        req.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void CustomRuntime_StoresNameAndVersion()
    {
        var c = new CustomRuntime("kotlin", "1.9", "apt-get install kotlin");
        c.Name.Should().Be("kotlin");
        c.Version.Should().Be("1.9");
        c.InstallScript.Should().Be("apt-get install kotlin");
    }

    [Fact]
    public void CustomRuntime_InstallScriptOptional()
    {
        var c = new CustomRuntime("kotlin", "1.9");
        c.InstallScript.Should().BeNull();
    }

    [Fact]
    public void RuntimeSpec_StoresAllFields()
    {
        var s = new RuntimeSpec("go", "1.22", "wget go.tar.gz");
        s.Language.Should().Be("go");
        s.Version.Should().Be("1.22");
        s.InstallScript.Should().Be("wget go.tar.gz");
    }

    [Fact]
    public void RuntimeRequirements_WithExpression_CreatesNewRecord()
    {
        var original = new RuntimeRequirements { Node = "18" };
        var updated  = original with { Node = "20" };

        original.Node.Should().Be("18");
        updated.Node.Should().Be("20");
    }

    [Fact]
    public void RuntimeRequirements_DotNetList_CanHoldMultipleVersions()
    {
        var req = new RuntimeRequirements { DotNet = ["8", "10"] };
        req.DotNet.Should().HaveCount(2).And.Contain(["8", "10"]);
    }
}

