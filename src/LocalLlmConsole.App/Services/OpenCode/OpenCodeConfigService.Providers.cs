namespace LocalLlmConsole.Services;

public sealed partial class OpenCodeConfigService
{
    private static void AddEnabledProvidersEnvelope(JsonObject envelope, JsonObject config, string providerId)
    {
        if (config["enabled_providers"] is not JsonArray enabledProviders) return;
        var copy = new JsonArray();
        foreach (var item in enabledProviders)
        {
            var value = item?.ToString();
            if (!string.IsNullOrWhiteSpace(value) && !ContainsString(copy, value))
                copy.Add(value);
        }
        if (!ContainsString(copy, providerId))
            copy.Add(providerId);
        envelope["enabled_providers"] = copy;
    }

    private static void MergeEnabledProviders(JsonObject config, JsonObject envelope, string providerId)
    {
        if (envelope["enabled_providers"] is JsonArray source)
        {
            var enabled = new JsonArray();
            foreach (var item in source)
            {
                var value = item?.ToString();
                if (!string.IsNullOrWhiteSpace(value) && !ContainsString(enabled, value))
                    enabled.Add(value);
            }
            if (!ContainsString(enabled, providerId))
                enabled.Add(providerId);
            config["enabled_providers"] = enabled;
            return;
        }

        EnsureProviderEnabled(config, providerId);
    }

    private static void EnsureProviderEnabled(JsonObject config, string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return;
        if (config["enabled_providers"] is JsonArray enabledProviders
            && !ContainsString(enabledProviders, providerId))
        {
            enabledProviders.Add(providerId);
        }

        if (config["disabled_providers"] is JsonArray disabledProviders)
        {
            for (var index = disabledProviders.Count - 1; index >= 0; index--)
            {
                if (string.Equals(disabledProviders[index]?.ToString(), providerId, StringComparison.OrdinalIgnoreCase))
                    disabledProviders.RemoveAt(index);
            }
        }
    }

    private static bool IsProviderEnabled(JsonObject config, string providerId)
    {
        if (config["disabled_providers"] is JsonArray disabledProviders
            && ContainsString(disabledProviders, providerId))
            return false;

        return config["enabled_providers"] is not JsonArray enabledProviders
            || ContainsString(enabledProviders, providerId);
    }

    private static bool ContainsString(JsonArray array, string value)
        => array.Any(item => string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase));
}
