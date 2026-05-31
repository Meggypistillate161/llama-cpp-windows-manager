namespace LocalLlmConsole.Services;

public sealed partial class StateStore
{
    private static bool IsStoredIntValue(IReadOnlyDictionary<string, string> values, string key, int expected)
        => values.TryGetValue(key, out var json)
            && TryReadJsonNumber(json, out var value)
            && value == expected;

    private static bool IsStoredDoubleValue(IReadOnlyDictionary<string, string> values, string key, double expected)
        => values.TryGetValue(key, out var json)
            && TryReadJsonDouble(json, out var value)
            && Math.Abs(value - expected) < 0.000_001;

    private static bool IsStoredStringValue(IReadOnlyDictionary<string, string> values, string key, string expected)
        => values.TryGetValue(key, out var json)
            && TryReadJsonString(json, out var value)
            && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeLegacyAppLaunchDefaults(IReadOnlyDictionary<string, string> values)
        => !values.ContainsKey("speculativeType")
            && !values.ContainsKey("specDraftModelPath")
            && !values.ContainsKey("visionImageMinTokens")
            && !values.ContainsKey("cudaPackagePreference");

    private static bool LooksLikeLegacyModelLaunchDefaultsJson(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject obj) return false;
            return !HasProperty(obj, nameof(ModelLaunchSettings.SpeculativeType))
                && !HasProperty(obj, nameof(ModelLaunchSettings.SpecDraftModelPath))
                && !HasProperty(obj, nameof(ModelLaunchSettings.VisionImageMinTokens))
                && !HasProperty(obj, nameof(ModelLaunchSettings.MtpHeadPath));
        }
        catch
        {
            return false;
        }
    }

    private static bool HasProperty(JsonObject obj, string name)
        => obj.Any(property => string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase));

    private static ModelLaunchSettings MigrateLegacyModelLaunchDefaults(ModelLaunchSettings settings, bool legacyShape, out bool changed)
    {
        changed = false;
        if (!legacyShape) return settings;

        var migrated = settings;
        if (settings.ContextSize == 0)
        {
            migrated = migrated with { ContextSize = AppSettings.DefaultContextSize };
            changed = true;
        }
        if (settings.GpuLayers == 0)
        {
            migrated = migrated with { GpuLayers = AppSettings.DefaultGpuLayers };
            changed = true;
        }
        if (settings.BatchSize == 2048)
        {
            migrated = migrated with { BatchSize = AppSettings.DefaultBatchSize };
            changed = true;
        }
        if (string.Equals(settings.CacheTypeK, "f16", StringComparison.OrdinalIgnoreCase))
        {
            migrated = migrated with { CacheTypeK = AppSettings.DefaultCacheType };
            changed = true;
        }
        if (string.Equals(settings.CacheTypeV, "f16", StringComparison.OrdinalIgnoreCase))
        {
            migrated = migrated with { CacheTypeV = AppSettings.DefaultCacheType };
            changed = true;
        }
        if (Math.Abs(settings.Temperature - 0.8) < 0.000_001)
        {
            migrated = migrated with { Temperature = AppSettings.DefaultTemperature };
            changed = true;
        }
        return migrated;
    }
}
