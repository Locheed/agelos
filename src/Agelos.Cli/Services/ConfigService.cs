using Agelos.Cli.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agelos.Cli.Services;

public interface IConfigService
{
    Task<AgelosConfig?> LoadConfigAsync(string projectPath);
    Task SaveConfigAsync(string projectPath, AgelosConfig config);
    Task CreateProjectConfigAsync(string projectPath, RuntimeRequirements runtimes, string agent = "opencode", IReadOnlyList<string>? addOns = null);
}

public class ConfigService : IConfigService
{
    private readonly IFileService _fileService;
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public ConfigService(IFileService fileService)
    {
        _fileService = fileService;

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public async Task<AgelosConfig?> LoadConfigAsync(string projectPath)
    {
        var configPath = Path.Combine(projectPath, ".agelos.yml");
        if (!await _fileService.FileExistsAsync(configPath))
            return null;

        var content = await _fileService.ReadAllTextAsync(configPath);
        return _deserializer.Deserialize<AgelosConfig>(content);
    }

    public async Task SaveConfigAsync(string projectPath, AgelosConfig config)
    {
        var configPath = Path.Combine(projectPath, ".agelos.yml");
        var content = _serializer.Serialize(config);
        await _fileService.WriteAllTextAsync(configPath, content);
    }

    public async Task CreateProjectConfigAsync(string projectPath, RuntimeRequirements runtimes, string agent = "opencode", IReadOnlyList<string>? addOns = null)
    {
        var config = new AgelosConfig
        {
            Version = "1.0",
            Runtimes = runtimes,
            Agent = agent,
            AddOns = addOns
        };

        await SaveConfigAsync(projectPath, config);
        await CreateGitignoreAsync(projectPath, runtimes);
    }

    private async Task CreateGitignoreAsync(string projectPath, RuntimeRequirements runtimes)
    {
        var gitignorePath = Path.Combine(projectPath, ".gitignore");
        if (await _fileService.FileExistsAsync(gitignorePath))
            return;

        var lines = new List<string> { "# Dependencies", "node_modules/", "" };

        if (runtimes.DotNet?.Any() == true)
            lines.AddRange(new[] { "# .NET", "bin/", "obj/", "*.user", "*.suo", "" });

        if (runtimes.Python != null)
            lines.AddRange(new[] { "# Python", "__pycache__/", "*.pyc", "*.pyo", "venv/", ".venv/", "" });

        if (runtimes.Node != null)
            lines.AddRange(new[] { "# Node.js", "dist/", "build/", "" });

        lines.AddRange(new[]
        {
            "# IDE", ".vscode/", ".idea/", "",
            "# Environment", ".env", ".env.local", "",
            "# Agelos", ".agelos/cache/"
        });

        await _fileService.WriteAllTextAsync(gitignorePath, string.Join("\n", lines));
    }
}
