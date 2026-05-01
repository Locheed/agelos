namespace Agelos.Cli.Models;

public record ModelInfo(
    string Id,
    string DisplayName,
    string HuggingFaceRepo,
    string FilePrefix,
    int ContextSize,
    int OutputSize,
    QuantOption[] Quants
);

public record QuantOption(string Name, double SizeGb)
{
    public string FileName(string filePrefix) => $"{filePrefix}-{Name}.gguf";

    public string Display => Name switch
    {
        "Q2_K"   => $"{Name,-10} (~{SizeGb:F1} GB)  smallest",
        "Q4_K_M" => $"{Name,-10} (~{SizeGb:F1} GB)  recommended",
        "Q8_0"   => $"{Name,-10} (~{SizeGb:F1} GB)  best quality",
        _        => $"{Name,-10} (~{SizeGb:F1} GB)",
    };
}
