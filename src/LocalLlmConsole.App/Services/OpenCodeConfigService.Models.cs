namespace LocalLlmConsole.Services;

public sealed partial class OpenCodeConfigService
{
    public IReadOnlyList<OpenCodeModelEntry> ListModels(string configPath)
    {
        var config = ReadConfigObject(configPath, createIfMissing: false);
        if (config["provider"] is not JsonObject providers) return [];

        var models = new List<OpenCodeModelEntry>();
        foreach (var (providerId, providerNode) in providers)
        {
            if (providerNode is not JsonObject provider) continue;
            if (provider["models"] is not JsonObject providerModels) continue;
            foreach (var (modelId, modelNode) in providerModels)
            {
                var modelName = modelNode is JsonObject modelObject ? modelObject["name"]?.ToString() : "";
                var fullId = $"{providerId}/{modelId}";
                var label = string.IsNullOrWhiteSpace(modelName)
                    ? modelId
                    : modelName;
                models.Add(new OpenCodeModelEntry(fullId, providerId, modelId, label));
            }
        }

        return models
            .OrderBy(model => model.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.ModelId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string ReadModelSnippet(string configPath, OpenCodeModelEntry entry)
    {
        var config = ReadConfigObject(configPath, createIfMissing: false);
        return FormatNode(ModelEnvelope(config, entry.ProviderId, entry.ModelId, entry.FullId));
    }

    public void SaveModelSnippet(string configPath, OpenCodeModelEntry entry, string snippet)
    {
        EnsureConfigFile(configPath);
        var config = ReadConfigObject(configPath, createIfMissing: true);
        var envelope = ParseObject(snippet, "Model config");
        MergeModelEnvelope(config, envelope, entry.ProviderId, entry.ModelId);
        SaveConfigObject(configPath, config);
    }

    public void DeleteModel(string configPath, OpenCodeModelEntry entry)
    {
        var config = ReadConfigObject(configPath, createIfMissing: false);
        if (config["provider"]?[entry.ProviderId]?["models"] is not JsonObject models)
            return;

        models.Remove(entry.ModelId);
        RemoveModelReference(config, "model", entry.FullId);
        RemoveModelReference(config, "small_model", entry.FullId);
        SaveConfigObject(configPath, config);
    }

    public bool SnippetsEquivalent(string left, string right)
    {
        try
        {
            return JsonEquivalent(ParseObject(left, "Saved model config"), ParseObject(right, "Edited model config"));
        }
        catch
        {
            return false;
        }
    }

    public OpenCodeLocalModelDraft CreateLocalModelDraft(string configPath, ModelRecord model, string baseUrl, string apiKey, int contextSize, int outputLimit)
    {
        var config = ReadConfigObject(configPath, createIfMissing: false);
        var modelId = LocalModelId(model);
        var modelObject = CreateLocalModelObject(model, contextSize, outputLimit);
        var fullId = $"{LocalProviderId}/{modelId}";
        var provider = new JsonObject
        {
            ["npm"] = "@ai-sdk/openai-compatible",
            ["name"] = LocalProviderName,
            ["options"] = new JsonObject
            {
                ["baseURL"] = baseUrl,
                ["apiKey"] = string.IsNullOrWhiteSpace(apiKey) ? "EMPTY" : apiKey.Trim(),
                ["timeout"] = 600000,
                ["chunkTimeout"] = 240000,
                ["headers"] = new JsonObject()
            },
            ["models"] = new JsonObject { [modelId] = modelObject }
        };
        var envelope = new JsonObject
        {
            ["$schema"] = SchemaUrl,
            ["model"] = fullId,
            ["provider"] = new JsonObject { [LocalProviderId] = provider },
            ["attachment"] = DefaultAttachmentObject()
        };
        AddEnabledProvidersEnvelope(envelope, config, LocalProviderId);
        return new OpenCodeLocalModelDraft(
            fullId,
            LocalProviderId,
            modelId,
            $"{fullId} - {model.Name}",
            FormatNode(envelope));
    }

    public OpenCodeModelAddAnalysis AnalyzeLocalModelSnippet(string configPath, ModelRecord model, string snippet)
    {
        var targetModelId = LocalModelId(model);
        var fullModelId = $"{LocalProviderId}/{targetModelId}";
        JsonObject proposed;
        try
        {
            var envelope = ParseObject(snippet, "Model config");
            proposed = ModelComparisonEnvelope(envelope, LocalProviderId, targetModelId);
        }
        catch (Exception ex)
        {
            return new OpenCodeModelAddAnalysis(false, ex.Message, fullModelId, false, false, []);
        }

        var config = ReadConfigObject(configPath, createIfMissing: false);
        var sameIdExists = false;
        var sameConfig = false;
        var similar = new List<string>();
        if (config["provider"] is JsonObject providers)
        {
            foreach (var (providerId, providerNode) in providers)
            {
                if (providerNode is not JsonObject provider) continue;
                var providerName = provider["name"]?.ToString();
                if (provider["models"] is not JsonObject providerModels) continue;
                foreach (var (candidateModelId, modelNode) in providerModels)
                {
                    var modelObject = modelNode as JsonObject;
                    var modelName = modelObject?["name"]?.ToString() ?? "";
                    var fullId = $"{providerId}/{candidateModelId}";
                    var label = string.IsNullOrWhiteSpace(modelName) ? fullId : $"{fullId} - {modelName}";
                    if (!string.IsNullOrWhiteSpace(providerName) && !label.Contains(providerName, StringComparison.OrdinalIgnoreCase))
                        label = $"{label} ({providerName})";

                    if (string.Equals(providerId, LocalProviderId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(candidateModelId, targetModelId, StringComparison.OrdinalIgnoreCase))
                    {
                        sameIdExists = true;
                        sameConfig = JsonEquivalent(ModelComparisonEnvelope(config, LocalProviderId, targetModelId), proposed)
                            && IsProviderEnabled(config, LocalProviderId);
                        continue;
                    }

                    if (IsSimilarModel(model, targetModelId, candidateModelId, modelName))
                        similar.Add(label);
                }
            }
        }

        return new OpenCodeModelAddAnalysis(true, "", fullModelId, sameIdExists, sameConfig, similar);
    }

    public string SaveLocalModelSnippet(string configPath, ModelRecord model, string baseUrl, string apiKey, string snippet, bool addAsNew)
    {
        EnsureConfigFile(configPath);
        var envelope = ParseObject(snippet, "Model config");
        var config = ReadConfigObject(configPath, createIfMissing: true);
        var modelId = LocalModelId(model);
        if (addAsNew)
        {
            var existingModels = EnsureObject(EnsureLocalProvider(config, baseUrl, apiKey), "models");
            modelId = UniqueModelId(existingModels, modelId);
            RenameEnvelopeModel(envelope, LocalProviderId, LocalModelId(model), modelId);
        }
        MergeModelEnvelope(config, envelope, LocalProviderId, modelId);
        EnsureLocalProvider(config, baseUrl, apiKey);

        var fullId = $"{LocalProviderId}/{modelId}";
        if (string.IsNullOrWhiteSpace(config["model"]?.ToString()))
            config["model"] = fullId;
        EnsureProviderEnabled(config, LocalProviderId);

        SaveConfigObject(configPath, config);
        return fullId;
    }

    public string AddOrUpdateLocalModel(string configPath, ModelRecord model, string baseUrl, string apiKey, int contextSize)
    {
        EnsureConfigFile(configPath);
        var config = ReadConfigObject(configPath, createIfMissing: true);
        var provider = EnsureLocalProvider(config, baseUrl, apiKey);

        var models = EnsureObject(provider, "models");
        var modelId = LocalModelId(model);
        if (models[modelId] is not JsonObject modelObject)
        {
            modelObject = new JsonObject();
            models[modelId] = modelObject;
        }
        modelObject["name"] = model.Name;
        if (contextSize > 0)
        {
            var limit = EnsureObject(modelObject, "limit");
            limit["context"] = contextSize;
        }

        var fullId = $"{LocalProviderId}/{modelId}";
        if (string.IsNullOrWhiteSpace(config["model"]?.ToString()))
            config["model"] = fullId;
        EnsureProviderEnabled(config, LocalProviderId);

        SaveConfigObject(configPath, config);
        return fullId;
    }

    public bool UpdateLocalProviderCredentials(string configPath, string baseUrl, string apiKey)
    {
        if (!File.Exists(configPath)) return false;
        var config = ReadConfigObject(configPath, createIfMissing: false);
        if (config["provider"]?[LocalProviderId] is not JsonObject) return false;

        EnsureLocalProvider(config, baseUrl, apiKey);
        SaveConfigObject(configPath, config);
        return true;
    }
}
