using Agelos.Cli.Models;

namespace Agelos.Cli.Core;

public static class RuntimeParser
{
    private static readonly Dictionary<string, Func<string, string>> InstallScripts = new()
    {
        ["dotnet"]  = v => $"apt-get update && apt-get install -y dotnet-sdk-{v}.0",
        ["node"]    = v => $"curl -fsSL https://deb.nodesource.com/setup_{v}.x | bash - && apt-get install -y nodejs",
        ["nodejs"]  = v => $"curl -fsSL https://deb.nodesource.com/setup_{v}.x | bash - && apt-get install -y nodejs",
        ["python"]  = v => $"apt-get update && apt-get install -y python{v} python3-pip python3-venv",
        ["go"]      = v => $"wget https://go.dev/dl/go{v}.linux-amd64.tar.gz && tar -C /usr/local -xzf go{v}.linux-amd64.tar.gz",
        ["rust"]    = _ => "curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y",
        ["java"]    = v => $"apt-get update && apt-get install -y openjdk-{v}-jdk",
        ["php"]     = v => $"apt-get update && apt-get install -y php{v} php{v}-cli",
        ["ruby"]    = v => $"apt-get update && apt-get install -y ruby{v}"
    };

    public static RuntimeSpec ParseSpec(string spec)
    {
        var parts = spec.Split(':', 2);
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid runtime spec: {spec}. Expected format: language:version", nameof(spec));

        var language = parts[0].ToLowerInvariant();
        var version = parts[1];
        var installScript = InstallScripts.TryGetValue(language, out var fn)
            ? fn(version)
            : GenerateGenericInstall(language, version);

        return new RuntimeSpec(language, version, installScript);
    }

    public static RuntimeRequirements ParseSpecs(string specs)
    {
        var requirements = new RuntimeRequirements();

        foreach (var spec in specs.Split(',', StringSplitOptions.TrimEntries))
        {
            var parsed = ParseSpec(spec);
            requirements = parsed.Language switch
            {
                "dotnet" => requirements with
                {
                    DotNet = (requirements.DotNet ?? new List<string>())
                        .Append(parsed.Version).Distinct().ToList()
                },
                "node" or "nodejs" => requirements with { Node = parsed.Version },
                "python"           => requirements with { Python = parsed.Version },
                "go" or "golang"   => requirements with { Go = parsed.Version },
                "rust"             => requirements with { Rust = true },
                "java"             => requirements with { Java = parsed.Version },
                "php"              => requirements with { Php = parsed.Version },
                "ruby"             => requirements with { Ruby = parsed.Version },
                _ => requirements with
                {
                    Custom = (requirements.Custom ?? new List<CustomRuntime>())
                        .Append(new CustomRuntime(parsed.Language, parsed.Version, parsed.InstallScript))
                        .ToList()
                }
            };
        }

        return requirements;
    }

    private static string GenerateGenericInstall(string language, string version) =>
        $"apt-get update && apt-get install -y {language} || " +
        $"snap install {language} --classic || " +
        $"echo \"Auto-installation failed for {language}:{version}. Manual setup required.\"";
}
