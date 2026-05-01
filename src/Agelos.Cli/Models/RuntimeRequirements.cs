namespace Agelos.Cli.Models;

public record RuntimeRequirements
{
    public List<string>? DotNet { get; init; }
    public string? Node { get; init; }
    public string? Python { get; init; }
    public string? Go { get; init; }
    public bool Rust { get; init; }
    public string? Java { get; init; }
    public string? Php { get; init; }
    public string? Ruby { get; init; }
    public List<CustomRuntime>? Custom { get; init; }

    public bool IsEmpty =>
        DotNet is null &&
        Node is null &&
        Python is null &&
        Go is null &&
        !Rust &&
        Java is null &&
        Php is null &&
        Ruby is null &&
        Custom is null or { Count: 0 };
}

public record CustomRuntime(string Name, string Version, string? InstallScript = null);

public record RuntimeSpec(string Language, string Version, string? InstallScript = null);
