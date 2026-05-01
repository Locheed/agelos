using System.Text.RegularExpressions;
using System.Xml.Linq;
using Agelos.Cli.Models;
using Agelos.Cli.Services;

namespace Agelos.Cli.Core;

public interface IRuntimeDetector
{
    Task<RuntimeRequirements> DetectAsync(string projectPath);
    Task<bool> IsEmptyProjectAsync(string projectPath);
}

public partial class RuntimeDetector : IRuntimeDetector
{
    private readonly IFileService _fileService;

    public RuntimeDetector(IFileService fileService) => _fileService = fileService;

    public async Task<RuntimeRequirements> DetectAsync(string projectPath)
    {
        var requirements = new RuntimeRequirements();

        var dotnetVersions = await DetectDotNetAsync(projectPath);
        if (dotnetVersions.Any())
            requirements = requirements with { DotNet = dotnetVersions };

        var nodeVersion = await DetectNodeAsync(projectPath);
        if (nodeVersion != null)
            requirements = requirements with { Node = nodeVersion };

        var pythonVersion = await DetectPythonAsync(projectPath);
        if (pythonVersion != null)
            requirements = requirements with { Python = pythonVersion };

        var goVersion = await DetectGoAsync(projectPath);
        if (goVersion != null)
            requirements = requirements with { Go = goVersion };

        if (await _fileService.FileExistsAsync(Path.Combine(projectPath, "Cargo.toml")))
            requirements = requirements with { Rust = true };

        return requirements;
    }

    private async Task<List<string>> DetectDotNetAsync(string projectPath)
    {
        var versions = new HashSet<string>();

        var csprojFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains("node_modules") && !f.Contains("bin") && !f.Contains("obj"));

        foreach (var file in csprojFiles)
        {
            var content = await _fileService.ReadAllTextAsync(file);
            foreach (var version in ExtractDotNetVersions(content))
                versions.Add(version);
        }

        return versions.OrderBy(v => v).ToList();
    }

    private static List<string> ExtractDotNetVersions(string csprojContent)
    {
        var versions = new List<string>();

        try
        {
            var doc = XDocument.Parse(csprojContent);

            var single = doc.Descendants("TargetFramework").FirstOrDefault()?.Value;
            if (single != null)
            {
                var m = DotNetVersionRegex().Match(single);
                if (m.Success) versions.Add(m.Groups[1].Value);
            }

            var multi = doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
            if (multi != null)
            {
                foreach (var fw in multi.Split(';'))
                {
                    var m = DotNetVersionRegex().Match(fw);
                    if (m.Success) versions.Add(m.Groups[1].Value);
                }
            }
        }
        catch
        {
            foreach (Match m in DotNetVersionRegex().Matches(csprojContent))
                versions.Add(m.Groups[1].Value);
        }

        return versions.Distinct().ToList();
    }

    private async Task<string?> DetectNodeAsync(string projectPath)
    {
        var path = Path.Combine(projectPath, "package.json");
        if (!await _fileService.FileExistsAsync(path)) return null;

        var content = await _fileService.ReadAllTextAsync(path);
        var m = NodeVersionRegex().Match(content);
        return m.Success ? m.Groups[1].Value : "20";
    }

    private async Task<string?> DetectPythonAsync(string projectPath)
    {
        var path = Path.Combine(projectPath, "pyproject.toml");
        if (!await _fileService.FileExistsAsync(path)) return null;

        var content = await _fileService.ReadAllTextAsync(path);
        var m = PythonVersionRegex().Match(content);
        return m.Success ? m.Groups[1].Value : "3.12";
    }

    private async Task<string?> DetectGoAsync(string projectPath)
    {
        var path = Path.Combine(projectPath, "go.mod");
        if (!await _fileService.FileExistsAsync(path)) return null;

        var content = await _fileService.ReadAllTextAsync(path);
        var m = GoVersionRegex().Match(content);
        return m.Success ? m.Groups[1].Value : "1.22";
    }

    public Task<bool> IsEmptyProjectAsync(string projectPath)
    {
        var ignoredNames = new HashSet<string>
        {
            ".git", ".gitignore", "README.md", ".agelos.yml", "LICENSE", ".DS_Store"
        };

        var files = Directory.GetFiles(projectPath)
            .Select(Path.GetFileName)
            .Where(f => f != null && !ignoredNames.Contains(f!));

        var dirs = Directory.GetDirectories(projectPath)
            .Select(Path.GetFileName)
            .Where(d => d != null && !ignoredNames.Contains(d!));

        return Task.FromResult(!files.Any() && !dirs.Any());
    }

    [GeneratedRegex(@"net(\d+)\.0")]
    private static partial Regex DotNetVersionRegex();

    [GeneratedRegex(@"""node"":\s*""[^""]*?(\d+)")]
    private static partial Regex NodeVersionRegex();

    [GeneratedRegex(@"python\s*=\s*""[^""]*(\d+\.\d+)")]
    private static partial Regex PythonVersionRegex();

    [GeneratedRegex(@"^go\s+(\d+\.\d+)", RegexOptions.Multiline)]
    private static partial Regex GoVersionRegex();
}
