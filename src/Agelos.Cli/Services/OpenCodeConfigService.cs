using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agelos.Cli.Services;

public class OpenCodeConfigService
{
    private const string LlamaProviderKey  = "llama-local";
    private const string LlamaServerBaseUrl = "http://127.0.0.1:8033/v1";

    public static string GetConfigPath(string projectPath) =>
        Path.Combine(projectPath, ".agelos", "opencode.json");

    public async Task AddModelAsync(string projectPath, string modelId, string displayName, int contextSize, int outputSize)
    {
        var path   = GetConfigPath(projectPath);
        var root   = await LoadOrCreateAsync(path);
        var models = EnsureModelsNode(root);

        models[modelId] = new JsonObject
        {
            ["name"]  = displayName,
            ["limit"] = new JsonObject
            {
                ["context"] = contextSize,
                ["output"]  = outputSize,
            }
        };

        await WriteAsync(path, root);
    }

    public async Task RemoveModelAsync(string projectPath, string modelId)
    {
        var path = GetConfigPath(projectPath);
        if (!File.Exists(path)) return;

        var root = await LoadAsync(path);
        if (root == null) return;

        GetModelsNode(root)?.Remove(modelId);
        await WriteAsync(path, root);
    }

    public async Task<Dictionary<string, string>> ListModelsAsync(string projectPath)
    {
        var path = GetConfigPath(projectPath);
        if (!File.Exists(path)) return [];

        var root = await LoadAsync(path);
        if (root == null) return [];

        var models = GetModelsNode(root);
        if (models == null) return [];

        var result = new Dictionary<string, string>();
        foreach (var kvp in models)
        {
            var name = kvp.Value?["name"]?.GetValue<string>() ?? kvp.Key;
            result[kvp.Key] = name;
        }
        return result;
    }

    private static async Task<JsonNode> LoadOrCreateAsync(string path)
    {
        if (File.Exists(path))
        {
            var loaded = await LoadAsync(path);
            if (loaded != null) return loaded;
        }
        return CreateDefaultConfig();
    }

    private static async Task<JsonNode?> LoadAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonNode.Parse(json);
    }

    private static async Task WriteAsync(string path, JsonNode root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);
    }

    private static JsonObject EnsureModelsNode(JsonNode root)
    {
        var rootObj = (JsonObject)root;

        if (rootObj["provider"] is not JsonObject provider)
        {
            provider = new JsonObject();
            rootObj["provider"] = provider;
        }

        if (provider[LlamaProviderKey] is not JsonObject llama)
        {
            llama = CreateDefaultLlamaProvider();
            provider[LlamaProviderKey] = llama;
        }

        if (llama["models"] is not JsonObject models)
        {
            models = new JsonObject();
            llama["models"] = models;
        }

        return models;
    }

    private static JsonObject? GetModelsNode(JsonNode root) =>
        root["provider"]?[LlamaProviderKey]?["models"] as JsonObject;

    private static JsonNode CreateDefaultConfig() => new JsonObject
    {
        ["$schema"]  = "https://opencode.ai/config.json",
        ["provider"] = new JsonObject
        {
            [LlamaProviderKey] = CreateDefaultLlamaProvider()
        }
    };

    private static JsonObject CreateDefaultLlamaProvider() => new()
    {
        ["npm"]     = "@ai-sdk/openai-compatible",
        ["name"]    = "llama-server (local)",
        ["options"] = new JsonObject { ["baseURL"] = LlamaServerBaseUrl },
        ["models"]  = new JsonObject()
    };
}
