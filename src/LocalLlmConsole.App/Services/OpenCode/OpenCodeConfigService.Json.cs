namespace LocalLlmConsole.Services;

public sealed partial class OpenCodeConfigService
{
    private JsonObject ReadConfigObject(string configPath, bool createIfMissing)
    {
        if (createIfMissing) EnsureConfigFile(configPath);
        if (!File.Exists(configPath)) return DefaultConfigObject();

        var text = File.ReadAllText(configPath);
        if (string.IsNullOrWhiteSpace(text)) return DefaultConfigObject();
        var node = JsonNode.Parse(text, documentOptions: JsoncOptions);
        return node as JsonObject ?? throw new InvalidOperationException("OpenCode config root must be a JSON object.");
    }

    private static void EnsureConfigFile(string configPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        if (File.Exists(configPath)) return;
        SaveConfigObject(configPath, DefaultConfigObject());
    }

    private static void SaveConfigObject(string configPath, JsonObject config)
    {
        config.Remove("attachment");
        ConfigFileSafetyService.WriteTextWithBackup(configPath, FormatNode(config) + Environment.NewLine, Encoding.UTF8, "OpenCode config file");
    }

    private static JsonObject DefaultConfigObject() => new()
    {
        ["$schema"] = SchemaUrl,
        ["provider"] = new JsonObject(),
        ["agent"] = new JsonObject()
    };

    private static JsonObject EnsureLocalProvider(JsonObject config, string baseUrl, string apiKey = "")
        => EnsureLocalProvider(config, LocalProviderId, LocalProviderName, baseUrl, apiKey, updateBaseUrl: true);

    private static JsonObject EnsureLocalProvider(
        JsonObject config,
        string providerId,
        string providerName,
        string baseUrl,
        string apiKey = "",
        bool updateBaseUrl = true)
    {
        var provider = EnsureObject(EnsureObject(config, "provider"), providerId);
        provider["npm"] = "@ai-sdk/openai-compatible";
        provider["name"] = providerName;
        var options = EnsureObject(provider, "options");
        if (updateBaseUrl && !string.IsNullOrWhiteSpace(baseUrl))
            options["baseURL"] = baseUrl;
        options["apiKey"] = string.IsNullOrWhiteSpace(apiKey) ? "EMPTY" : apiKey.Trim();
        return provider;
    }

    private static JsonObject CreateLocalModelObject(
        ModelRecord model,
        int contextSize,
        int outputLimit,
        bool supportsVision)
    {
        var modelObject = new JsonObject { ["name"] = model.Name };
        var limit = new JsonObject { ["output"] = NormalizeOutputLimit(outputLimit) };
        if (contextSize > 0) limit["context"] = contextSize;
        modelObject["limit"] = limit;
        ApplyVisionSupport(modelObject, supportsVision);
        return modelObject;
    }

    private static void ApplyVisionSupport(JsonObject modelObject, bool supportsVision)
    {
        if (!supportsVision)
        {
            modelObject.Remove("attachment");
            modelObject.Remove("modalities");
            return;
        }

        modelObject["attachment"] = true;
        modelObject["modalities"] = new JsonObject
        {
            ["input"] = new JsonArray("text", "image"),
            ["output"] = new JsonArray("text")
        };
    }

    private static int NormalizeOutputLimit(int outputLimit)
        => outputLimit > 0 ? outputLimit : DefaultOutputLimit;

    private static JsonObject EnsureObject(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing) return existing;
        var next = new JsonObject();
        parent[key] = next;
        return next;
    }

    private static JsonObject ParseObject(string text, string label)
    {
        var node = JsonNode.Parse(text, documentOptions: JsoncOptions);
        return node as JsonObject ?? throw new InvalidOperationException($"{label} must be a JSON object.");
    }

    private static string FormatNode(JsonNode node) => node.ToJsonString(PrettyJson);

}
