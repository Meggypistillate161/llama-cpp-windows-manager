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
        ConfigFileSafetyService.WriteTextWithBackup(configPath, FormatNode(config) + Environment.NewLine, Encoding.UTF8, "OpenCode config file");
    }

    private static JsonObject DefaultConfigObject() => new()
    {
        ["$schema"] = SchemaUrl,
        ["provider"] = new JsonObject(),
        ["agent"] = new JsonObject()
    };

    private static JsonObject DefaultAttachmentObject() => new()
    {
        ["image"] = new JsonObject
        {
            ["auto_resize"] = true,
            ["max_width"] = 2000,
            ["max_height"] = 2000,
            ["max_base64_bytes"] = 5242880
        }
    };

    private static JsonObject EnsureLocalProvider(JsonObject config, string baseUrl, string apiKey = "")
    {
        var provider = EnsureObject(EnsureObject(config, "provider"), LocalProviderId);
        provider["npm"] = "@ai-sdk/openai-compatible";
        provider["name"] = LocalProviderName;
        var options = EnsureObject(provider, "options");
        if (!string.IsNullOrWhiteSpace(baseUrl))
            options["baseURL"] = baseUrl;
        options["apiKey"] = string.IsNullOrWhiteSpace(apiKey) ? "EMPTY" : apiKey.Trim();
        return provider;
    }

    private static JsonObject CreateLocalModelObject(ModelRecord model, int contextSize, int outputLimit)
    {
        var modelObject = new JsonObject { ["name"] = model.Name };
        var limit = new JsonObject();
        if (contextSize > 0) limit["context"] = contextSize;
        if (outputLimit > 0) limit["output"] = outputLimit;
        if (limit.Count > 0) modelObject["limit"] = limit;
        return modelObject;
    }

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
