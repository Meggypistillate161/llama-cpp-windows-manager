namespace LocalLlmConsole.Services;

public sealed partial class OpenCodeConfigService
{
    private static JsonObject ModelEnvelope(JsonObject config, string providerId, string modelId, string fullId)
    {
        var envelope = new JsonObject
        {
            ["$schema"] = config["$schema"]?.DeepClone() ?? SchemaUrl,
            ["model"] = fullId
        };

        if (config["small_model"]?.ToString() == fullId)
            envelope["small_model"] = fullId;

        var provider = config["provider"]?[providerId] as JsonObject ?? new JsonObject();
        var providerEnvelope = new JsonObject();
        foreach (var (key, value) in provider)
        {
            if (key == "models") continue;
            providerEnvelope[key] = value?.DeepClone();
        }

        providerEnvelope["models"] = new JsonObject
        {
            [modelId] = (provider["models"]?[modelId] as JsonObject)?.DeepClone() ?? new JsonObject()
        };
        envelope["provider"] = new JsonObject { [providerId] = providerEnvelope };

        if (config["attachment"] is JsonObject attachment)
            envelope["attachment"] = attachment.DeepClone();
        else
            envelope["attachment"] = DefaultAttachmentObject();

        AddEnabledProvidersEnvelope(envelope, config, providerId);
        return envelope;
    }

    private static JsonObject ModelComparisonEnvelope(JsonObject configOrEnvelope, string providerId, string modelId)
    {
        var provider = configOrEnvelope["provider"]?[providerId] as JsonObject
            ?? throw new InvalidOperationException($"Config must include provider.{providerId}.");
        var model = provider["models"]?[modelId] as JsonObject
            ?? throw new InvalidOperationException($"Config must include provider.{providerId}.models.{modelId}.");

        var comparisonProvider = new JsonObject();
        foreach (var (key, value) in provider)
        {
            if (key == "models") continue;
            comparisonProvider[key] = value?.DeepClone();
        }
        comparisonProvider["models"] = new JsonObject { [modelId] = model.DeepClone() };

        var comparison = new JsonObject { ["provider"] = new JsonObject { [providerId] = comparisonProvider } };
        if (configOrEnvelope["attachment"] is JsonObject attachment)
            comparison["attachment"] = attachment.DeepClone();
        else
            comparison["attachment"] = DefaultAttachmentObject();
        return comparison;
    }

    private static void MergeModelEnvelope(JsonObject config, JsonObject envelope, string fallbackProviderId, string fallbackModelId)
    {
        if (envelope["$schema"] is not null)
            config["$schema"] = envelope["$schema"]!.DeepClone();
        if (envelope["model"] is not null)
            config["model"] = envelope["model"]!.DeepClone();
        if (envelope["small_model"] is not null)
            config["small_model"] = envelope["small_model"]!.DeepClone();
        if (envelope["attachment"] is JsonObject attachment)
            config["attachment"] = attachment.DeepClone();

        if (envelope["provider"] is not JsonObject envelopeProviders)
            throw new InvalidOperationException("Config must include provider settings.");

        var providers = EnsureObject(config, "provider");
        var providerId = envelopeProviders.ContainsKey(fallbackProviderId)
            ? fallbackProviderId
            : envelopeProviders.FirstOrDefault().Key ?? fallbackProviderId;
        if (envelopeProviders[providerId] is not JsonObject sourceProvider)
            throw new InvalidOperationException($"Config must include provider.{providerId}.");

        var targetProvider = EnsureObject(providers, providerId);
        foreach (var (key, value) in sourceProvider)
        {
            if (key == "models") continue;
            targetProvider[key] = value?.DeepClone();
        }

        if (sourceProvider["models"] is not JsonObject sourceModels)
            throw new InvalidOperationException($"Config must include provider.{providerId}.models.");
        var targetModels = EnsureObject(targetProvider, "models");
        if (sourceModels.Count == 0)
            throw new InvalidOperationException("Config must include at least one model.");

        foreach (var (modelId, modelNode) in sourceModels)
        {
            if (modelNode is not JsonObject)
                throw new InvalidOperationException($"provider.{providerId}.models.{modelId} must be a JSON object.");
            targetModels[string.IsNullOrWhiteSpace(modelId) ? fallbackModelId : modelId] = modelNode.DeepClone();
        }

        MergeEnabledProviders(config, envelope, providerId);
        EnsureProviderEnabled(config, providerId);
    }

    private static void RenameEnvelopeModel(JsonObject envelope, string providerId, string oldModelId, string newModelId)
    {
        if (oldModelId == newModelId) return;
        if (envelope["provider"]?[providerId]?["models"] is not JsonObject models) return;
        var node = models[oldModelId]?.DeepClone();
        if (node is null) return;
        models.Remove(oldModelId);
        models[newModelId] = node;
        envelope["model"] = $"{providerId}/{newModelId}";
    }

    private static void RemoveModelReference(JsonObject config, string key, string fullId)
    {
        if (string.Equals(config[key]?.ToString(), fullId, StringComparison.OrdinalIgnoreCase))
            config.Remove(key);
    }

    private static bool JsonEquivalent(JsonNode left, JsonNode right)
        => CanonicalJson(left) == CanonicalJson(right);

    private static string CanonicalJson(JsonNode? node)
    {
        if (node is null) return "null";
        if (node is JsonObject obj)
            return "{" + string.Join(",", obj.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{JsonSerializer.Serialize(item.Key)}:{CanonicalJson(item.Value)}")) + "}";
        if (node is JsonArray array)
            return "[" + string.Join(",", array.Select(CanonicalJson)) + "]";
        return node.ToJsonString();
    }

    private static string LocalModelId(ModelRecord model)
        => SafeOpenCodeId(Path.GetFileNameWithoutExtension(model.ModelPath));

    private static string UniqueModelId(JsonObject models, string modelId)
    {
        if (!models.ContainsKey(modelId)) return modelId;
        for (var index = 2; ; index++)
        {
            var candidate = $"{modelId}-{index}";
            if (!models.ContainsKey(candidate)) return candidate;
        }
    }

    private static bool IsSimilarModel(ModelRecord model, string targetId, string candidateId, string candidateName)
    {
        var targets = new[]
        {
            NormalizeSimilarityText(targetId),
            NormalizeSimilarityText(model.Name),
            NormalizeSimilarityText(Path.GetFileNameWithoutExtension(model.ModelPath))
        }.Where(value => value.Length >= 6).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var candidates = new[]
        {
            NormalizeSimilarityText(candidateId),
            NormalizeSimilarityText(candidateName)
        }.Where(value => value.Length >= 6).ToArray();

        return targets.Any(target => candidates.Any(candidate =>
            string.Equals(candidate, target, StringComparison.OrdinalIgnoreCase)
            || candidate.Contains(target, StringComparison.OrdinalIgnoreCase)
            || target.Contains(candidate, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeSimilarityText(string value)
        => Regex.Replace((value ?? "").ToLowerInvariant(), @"[^a-z0-9]+", "");
}
