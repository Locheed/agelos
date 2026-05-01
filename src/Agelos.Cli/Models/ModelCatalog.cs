namespace Agelos.Cli.Models;

public static class ModelCatalog
{
    // Approximate sizes scaled from Q4_K_M baseline
    private static QuantOption[] Q(double q4Gb) =>
    [
        new("Q2_K",   Math.Round(q4Gb * 0.42, 1)),
        new("Q3_K_M", Math.Round(q4Gb * 0.56, 1)),
        new("Q4_K_M", q4Gb),
        new("Q5_K_M", Math.Round(q4Gb * 1.22, 1)),
        new("Q6_K",   Math.Round(q4Gb * 1.42, 1)),
        new("Q8_0",   Math.Round(q4Gb * 1.84, 1)),
    ];

    // Source: bartowski's HuggingFace repos (https://huggingface.co/bartowski)
    // Filenames follow pattern: {FilePrefix}-{Quant}.gguf
    public static readonly ModelInfo[] All =
    [
        // Qwen3 / Qwen3.5 / Qwen3.6
        new("qwen3-0.6b",       "Qwen3 0.6B",             "bartowski/Qwen_Qwen3-0.6B-GGUF",          "Qwen_Qwen3-0.6B",           32768,  8192, Q(0.4)),
        new("qwen3.5-2b",       "Qwen3.5 2B",             "bartowski/Qwen_Qwen3.5-2B-GGUF",          "Qwen_Qwen3.5-2B",          262144, 32768, Q(1.33)),
        new("qwen3.5-4b",       "Qwen3.5 4B",             "bartowski/Qwen_Qwen3.5-4B-GGUF",          "Qwen_Qwen3.5-4B",          262144, 32768, Q(2.87)),
        new("qwen3.5-9b",       "Qwen3.5 9B",             "bartowski/Qwen_Qwen3.5-9B-GGUF",          "Qwen_Qwen3.5-9B",          262144, 32768, Q(5.89)),
        new("qwen3-14b",        "Qwen3 14B",              "bartowski/Qwen_Qwen3-14B-GGUF",            "Qwen_Qwen3-14B",           131072, 32768, Q(9.0)),
        new("qwen3.6-27b",      "Qwen3.6 27B",            "bartowski/Qwen_Qwen3.6-27B-GGUF",          "Qwen_Qwen3.6-27B",         262144, 32768, Q(17.53)),
        new("qwen3.6-35b-a3b",  "Qwen3.6 35B-A3B (MoE)",  "bartowski/Qwen_Qwen3.6-35B-A3B-GGUF",      "Qwen_Qwen3.6-35B-A3B",     262144, 32768, Q(21.39)),
        // Llama 3
        new("llama-3.2-1b",  "Llama 3.2 1B",         "bartowski/Llama-3.2-1B-Instruct-GGUF",      "Llama-3.2-1B-Instruct",      131072,  8192, Q(0.7)),
        new("llama-3.2-3b",  "Llama 3.2 3B",         "bartowski/Llama-3.2-3B-Instruct-GGUF",      "Llama-3.2-3B-Instruct",      131072,  8192, Q(2.0)),
        new("llama-3.1-8b",  "Llama 3.1 8B",         "bartowski/Meta-Llama-3.1-8B-Instruct-GGUF", "Meta-Llama-3.1-8B-Instruct", 131072,  8192, Q(4.9)),
        // Other
        new("mistral-7b",    "Mistral 7B v0.3",      "bartowski/Mistral-7B-Instruct-v0.3-GGUF",   "Mistral-7B-Instruct-v0.3",   32768,   8192, Q(4.4)),
        new("phi-4-mini",    "Phi-4 Mini",           "bartowski/microsoft_Phi-4-mini-instruct-GGUF", "microsoft_Phi-4-mini-instruct", 131072,  8192, Q(2.2)),
        new("gemma-3-4b",    "Gemma 3 4B",           "bartowski/google_gemma-3-4b-it-GGUF",          "google_gemma-3-4b-it",          131072,  8192, Q(2.6)),
        new("gemma-3-12b",   "Gemma 3 12B",          "bartowski/google_gemma-3-12b-it-GGUF",         "google_gemma-3-12b-it",         131072,  8192, Q(7.3)),
        // Gemma 4
        new("gemma-4-e2b",      "Gemma 4 E2B",           "bartowski/google_gemma-4-E2B-it-GGUF",         "google_gemma-4-E2B-it",         131072,  8192, Q(3.46)),
        new("gemma-4-e4b",      "Gemma 4 E4B",           "bartowski/google_gemma-4-E4B-it-GGUF",         "google_gemma-4-E4B-it",         131072,  8192, Q(5.41)),
        new("gemma-4-26b-a4b",  "Gemma 4 26B-A4B (MoE)", "bartowski/google_gemma-4-26B-A4B-it-GGUF",     "google_gemma-4-26B-A4B-it",     262144,  8192, Q(17.0)),
        new("gemma-4-31b",      "Gemma 4 31B",           "bartowski/google_gemma-4-31B-it-GGUF",         "google_gemma-4-31B-it",         262144,  8192, Q(19.6)),
    ];

    public static ModelInfo? FindById(string id) =>
        Array.Find(All, m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
