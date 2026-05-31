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

    public OpenCodeLocalModelDraft CreateLocalModelDraft(
        string configPath,
        ModelRecord model,
        string baseUrl,
        string apiKey,
        int contextSize,
        int outputLimit,
        bool useGatewayProvider = false,
        bool supportsVision = false)
    {
        var config = ReadConfigObject(configPath, createIfMissing: false);
        var modelId = LocalModelId(model);
        var providerId = LocalProviderIdFor(model, useGatewayProvider);
        var providerName = LocalProviderNameFor(model, useGatewayProvider);
        var modelObject = CreateLocalModelObject(model, contextSize, outputLimit, supportsVision);
        var fullId = $"{providerId}/{modelId}";
        var provider = new JsonObject
        {
            ["npm"] = "@ai-sdk/openai-compatible",
            ["name"] = providerName,
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
            ["provider"] = new JsonObject { [providerId] = provider }
        };
        AddEnabledProvidersEnvelope(envelope, config, providerId);
        return new OpenCodeLocalModelDraft(
            fullId,
            providerId,
            modelId,
            $"{fullId} - {model.Name}",
            FormatNode(envelope));
    }

    public OpenCodeModelAddAnalysis AnalyzeLocalModelSnippet(string configPath, ModelRecord model, string snippet, bool useGatewayProvider = false)
    {
        var targetModelId = LocalModelId(model);
        var targetProviderId = LocalProviderIdFor(model, useGatewayProvider);
        var fullModelId = $"{targetProviderId}/{targetModelId}";
        JsonObject proposed;
        try
        {
            var envelope = ParseObject(snippet, "Model config");
            proposed = ModelComparisonEnvelope(envelope, targetProviderId, targetModelId);
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

                    if (string.Equals(providerId, targetProviderId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(candidateModelId, targetModelId, StringComparison.OrdinalIgnoreCase))
                    {
                        sameIdExists = true;
                        sameConfig = JsonEquivalent(ModelComparisonEnvelope(config, targetProviderId, targetModelId), proposed)
                            && IsProviderEnabled(config, targetProviderId);
                        continue;
                    }

                    if (IsSimilarModel(model, targetModelId, candidateModelId, modelName))
                        similar.Add(label);
                }
            }
        }

        return new OpenCodeModelAddAnalysis(true, "", fullModelId, sameIdExists, sameConfig, similar);
    }

    public string SaveLocalModelSnippet(string configPath, ModelRecord model, string baseUrl, string apiKey, string snippet, bool addAsNew, bool useGatewayProvider = false)
    {
        EnsureConfigFile(configPath);
        var envelope = ParseObject(snippet, "Model config");
        var config = ReadConfigObject(configPath, createIfMissing: true);
        var modelId = LocalModelId(model);
        var providerId = LocalProviderIdFor(model, useGatewayProvider);
        var providerName = LocalProviderNameFor(model, useGatewayProvider);
        if (addAsNew)
        {
            var existingModels = EnsureObject(EnsureLocalProvider(config, providerId, providerName, baseUrl, apiKey), "models");
            modelId = UniqueModelId(existingModels, modelId);
            RenameEnvelopeModel(envelope, providerId, LocalModelId(model), modelId);
        }
        MergeModelEnvelope(config, envelope, providerId, modelId);
        EnsureLocalProvider(config, providerId, providerName, baseUrl, apiKey);

        var fullId = $"{providerId}/{modelId}";
        if (string.IsNullOrWhiteSpace(config["model"]?.ToString()))
            config["model"] = fullId;
        EnsureProviderEnabled(config, providerId);

        SaveConfigObject(configPath, config);
        return fullId;
    }

    public string AddOrUpdateLocalModel(
        string configPath,
        ModelRecord model,
        string baseUrl,
        string apiKey,
        int contextSize,
        int outputLimit,
        bool useGatewayProvider = false,
        bool supportsVision = false)
    {
        EnsureConfigFile(configPath);
        var config = ReadConfigObject(configPath, createIfMissing: true);
        var providerId = LocalProviderIdFor(model, useGatewayProvider);
        var provider = EnsureLocalProvider(config, providerId, LocalProviderNameFor(model, useGatewayProvider), baseUrl, apiKey);

        var models = EnsureObject(provider, "models");
        var modelId = LocalModelId(model);
        if (models[modelId] is not JsonObject modelObject)
        {
            modelObject = new JsonObject();
            models[modelId] = modelObject;
        }
        modelObject["name"] = model.Name;
        var limit = EnsureObject(modelObject, "limit");
        if (contextSize > 0)
            limit["context"] = contextSize;
        limit["output"] = NormalizeOutputLimit(outputLimit);
        ApplyVisionSupport(modelObject, supportsVision);

        var fullId = $"{providerId}/{modelId}";
        if (string.IsNullOrWhiteSpace(config["model"]?.ToString()))
            config["model"] = fullId;
        EnsureProviderEnabled(config, providerId);

        SaveConfigObject(configPath, config);
        return fullId;
    }

    public OpenCodeLocalProviderHealth InspectLocalGatewayProvider(
        string configPath,
        IReadOnlyList<ModelRecord> expectedModels,
        string expectedBaseUrl)
    {
        var config = ReadConfigObject(configPath, createIfMissing: false);
        var expectedCount = expectedModels.Count;
        if (config["attachment"] is not null)
        {
            return new OpenCodeLocalProviderHealth(
                false,
                "OpenCode sync: config contains deprecated attachment settings.",
                "OpenCode 1.14 rejects the old top-level attachment key. Save Settings or update an OpenCode model from this app to remove it.",
                0,
                expectedCount);
        }

        if (config["provider"]?[LocalProviderId] is not JsonObject provider)
        {
            return new OpenCodeLocalProviderHealth(
                false,
                "OpenCode sync: gateway provider is missing.",
                $"Save Settings to create provider.{LocalProviderId} for the auto-load gateway.",
                0,
                expectedCount);
        }

        var options = provider["options"] as JsonObject;
        var baseUrl = options?["baseURL"]?.ToString() ?? "";
        if (!string.Equals(baseUrl, expectedBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return new OpenCodeLocalProviderHealth(
                false,
                "OpenCode sync: gateway provider points at a different endpoint.",
                $"Expected {expectedBaseUrl}, but provider.{LocalProviderId}.options.baseURL is {BlankIfEmpty(baseUrl)}. Save Settings to refresh it.",
                GatewayModelCount(provider),
                expectedCount);
        }

        if (provider["models"] is not JsonObject providerModels)
        {
            return new OpenCodeLocalProviderHealth(
                false,
                "OpenCode sync: gateway provider has no models block.",
                $"Save Settings to recreate provider.{LocalProviderId}.models.",
                0,
                expectedCount);
        }

        var missing = new List<string>();
        var missingOutput = new List<string>();
        foreach (var model in expectedModels)
        {
            var modelId = LocalModelId(model);
            if (providerModels[modelId] is not JsonObject modelObject)
            {
                missing.Add(model.Name);
                continue;
            }

            var output = modelObject["limit"]?["output"];
            if (output is null || !int.TryParse(output.ToString(), out var outputLimit) || outputLimit <= 0)
                missingOutput.Add(model.Name);
        }

        if (missing.Count > 0)
        {
            return new OpenCodeLocalProviderHealth(
                false,
                $"OpenCode sync: missing {missing.Count} gateway model(s).",
                $"Missing: {string.Join(", ", missing.Take(6))}. Save Settings to resync the gateway provider.",
                expectedCount - missing.Count,
                expectedCount);
        }

        if (missingOutput.Count > 0)
        {
            return new OpenCodeLocalProviderHealth(
                false,
                $"OpenCode sync: {missingOutput.Count} gateway model(s) need output limits.",
                $"Missing limit.output: {string.Join(", ", missingOutput.Take(6))}. Save Settings to rewrite the provider for current OpenCode versions.",
                expectedCount - missingOutput.Count,
                expectedCount);
        }

        return new OpenCodeLocalProviderHealth(
            true,
            $"OpenCode sync: healthy. Gateway provider has {expectedCount}/{expectedCount} model(s) at {expectedBaseUrl}.",
            $"OpenCode requests to {LocalProviderId}/<model> will use the auto-load gateway at {expectedBaseUrl}.",
            expectedCount,
            expectedCount);
    }

    public bool UpdateLocalProviderCredentials(string configPath, string baseUrl, string apiKey)
    {
        if (!File.Exists(configPath)) return false;
        var config = ReadConfigObject(configPath, createIfMissing: false);
        if (config["provider"] is not JsonObject providers) return false;

        var updated = false;
        foreach (var (providerId, providerNode) in providers.ToArray())
        {
            if (!IsLocalProviderId(providerId) || providerNode is not JsonObject provider) continue;
            var updateBaseUrl = string.Equals(providerId, LocalProviderId, StringComparison.OrdinalIgnoreCase);
            EnsureLocalProvider(
                config,
                providerId,
                provider["name"]?.ToString() ?? LocalProviderName,
                baseUrl,
                apiKey,
                updateBaseUrl);
            updated = true;
        }

        if (!updated) return false;
        SaveConfigObject(configPath, config);
        return true;
    }

    private static int GatewayModelCount(JsonObject provider)
        => provider["models"] is JsonObject providerModels ? providerModels.Count : 0;

    private static string BlankIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? "(not set)" : value;
}
