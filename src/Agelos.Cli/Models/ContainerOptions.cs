namespace Agelos.Cli.Models;

public record ContainerOptions
{
    public required string Image { get; init; }
    public required string WorkingDirectory { get; init; }
    public List<VolumeMount> Volumes { get; init; } = new();
    public Dictionary<string, string> Environment { get; init; } = new();
    public List<PortMapping> Ports { get; init; } = new();
    public bool Interactive { get; init; } = true;
    public string[] Command { get; init; } = Array.Empty<string>();
}

public record VolumeMount(string Host, string Container, string Mode = "rw");

public record PortMapping(int Host, int Container);

public record BuildOptions
{
    public required string Dockerfile { get; init; }
    public required string Context { get; init; }
    public required string Tag { get; init; }
    public Dictionary<string, string> BuildArgs { get; init; } = new();
}
