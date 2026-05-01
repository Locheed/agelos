namespace Agelos.Cli.Models;

public record AgelosConfig
{
    public string Version { get; init; } = "1.0";
    public RuntimeRequirements Runtimes { get; init; } = new();
    public string? Agent { get; init; }
    public string? Model { get; init; }
    public IReadOnlyList<string>? AddOns { get; init; }
}
