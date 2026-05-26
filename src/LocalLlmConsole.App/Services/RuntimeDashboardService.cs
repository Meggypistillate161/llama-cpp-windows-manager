
namespace LocalLlmConsole.Services;

public sealed record RuntimeSlotSnapshot(
    double PromptTokensProcessed,
    double GeneratedTokens,
    bool IsProcessing,
    double? PromptTokens,
    double? ContextTokens,
    double? ContextSize);

public static class RuntimeDashboardService
{
    public static RuntimeSlotSnapshot? ParseSlotSnapshot(string raw)
    {
        var node = JsonNode.Parse(raw);
        if (node is not JsonArray slots) return null;

        double promptProcessed = 0;
        double generated = 0;
        double? promptTokens = null;
        double? contextTokens = null;
        double? contextSize = null;
        var processing = false;

        foreach (var slotNode in slots.OfType<JsonObject>())
        {
            var slotProcessing = ReadBool(slotNode, "is_processing", "processing", "busy");
            processing |= slotProcessing;

            var slotPromptProcessed = ReadDouble(slotNode, "n_prompt_tokens_processed", "prompt_tokens_processed", "n_prompt_tokens_processed_total") ?? 0;
            var slotGenerated = ReadDouble(slotNode, "n_decoded", "tokens_predicted", "n_tokens_predicted", "n_tokens_predicted_total");
            if (slotGenerated is null && slotNode["next_token"] is JsonArray nextTokens)
            {
                slotGenerated = nextTokens.OfType<JsonObject>()
                    .Select(next => ReadDouble(next, "n_decoded", "tokens_predicted", "n_tokens_predicted"))
                    .Where(value => value is not null)
                    .Sum(value => value!.Value);
                processing |= nextTokens.OfType<JsonObject>().Any(next => ReadBool(next, "has_next_token"));
            }

            promptProcessed += slotPromptProcessed;
            generated += slotGenerated ?? 0;

            promptTokens = SumNullable(promptTokens, ReadDouble(slotNode, "n_prompt_tokens", "prompt_tokens"));
            var slotContextTokens = slotPromptProcessed + (slotGenerated ?? 0);
            contextTokens = SumNullable(contextTokens, slotContextTokens > 0 ? slotContextTokens : null);
            contextSize = SumNullable(contextSize, ReadDouble(slotNode, "n_ctx", "context_size", "ctx_size"));
        }

        return new RuntimeSlotSnapshot(promptProcessed, generated, processing, promptTokens, contextTokens, contextSize);
    }

    public static double? CounterRate(double? current, double? previous, DateTimeOffset now, DateTimeOffset? previousPollAt, double minElapsedSeconds)
    {
        if (current is null || previous is null || previousPollAt is null || current < previous) return null;
        var elapsed = (now - previousPollAt.Value).TotalSeconds;
        return elapsed < minElapsedSeconds ? null : (current.Value - previous.Value) / elapsed;
    }

    public static double? DeltaRate(double current, double? previous, double elapsedSeconds, bool includeZero)
    {
        if (previous is null || current < previous.Value || elapsedSeconds <= 0) return null;
        var delta = current - previous.Value;
        if (delta <= 0 && !includeZero) return null;
        return delta / elapsedSeconds;
    }

    public static double? SumNullable(double? current, double? next)
        => next is null ? current : (current ?? 0) + next.Value;

    public static double? MaxNullable(double? current, double? next)
    {
        if (current is null) return next;
        if (next is null) return current;
        return Math.Max(current.Value, next.Value);
    }

    public static long WholePositiveDelta(double? current, double? previous)
    {
        if (current is null || previous is null || current.Value < previous.Value) return 0;
        return Math.Max(0, (long)Math.Floor(current.Value - previous.Value));
    }

    public static bool PositiveDelta(double? current, double? previous)
        => current is not null && previous is not null && current.Value > previous.Value;

    public static double? Rate(double? amount, double? seconds)
        => amount is not null && seconds is > 0 ? amount.Value / seconds.Value : null;

    public static string TokenSummaryLabel(double? generated, double? prompt)
    {
        var genText = generated is null ? "?" : generated.Value.ToString("N0");
        var promptText = prompt is null ? "?" : prompt.Value.ToString("N0");
        return $"Gen {genText}\nPrompt {promptText}";
    }

    public static string RateLabel(double? live, double? average)
    {
        if (live is null && average is null) return "Unknown";
        if (live is not null && average is not null) return $"{FormatTokenRate(live.Value)} t/s ({FormatTokenRate(average.Value)} avg)";
        return live is not null ? $"{FormatTokenRate(live.Value)} t/s" : $"{FormatTokenRate(average!.Value)} avg";
    }

    public static string RuntimeSettingsLabel(double? kvUsage, double? kvTokens, double? contextSize, int launchContextSize)
        => $"Context {ContextSizeLabel(contextSize, launchContextSize)}\nKV cache {KvCacheLabel(kvUsage, kvTokens)}";

    public static string WithLastKnownLine(string value, DateTimeOffset capturedAt, DateTimeOffset now)
    {
        var age = now <= capturedAt ? "just now" : DisplayFormatService.Elapsed(now - capturedAt);
        return $"{value.TrimEnd()}\nLast known {age} ago";
    }

    public static string ContextSizeLabel(double? contextSize, int launchContextSize)
    {
        if (contextSize is > 0) return contextSize.Value.ToString("N0");
        return launchContextSize == 0 ? "Model default" : launchContextSize.ToString("N0");
    }

    public static double? ReadDouble(JsonObject obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj[key] is null) continue;
            if (obj[key] is JsonValue value && value.TryGetValue<double>(out var number)) return number;
            if (double.TryParse(obj[key]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        }
        return null;
    }

    public static bool ReadBool(JsonObject obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj[key] is null) continue;
            if (obj[key] is JsonValue value && value.TryGetValue<bool>(out var boolean)) return boolean;
            if (bool.TryParse(obj[key]?.ToString(), out var parsed)) return parsed;
        }
        return false;
    }

    private static string KvCacheLabel(double? usage, double? tokens)
    {
        var parts = new List<string>();
        if (usage is not null)
        {
            var percent = usage <= 1 ? usage.Value * 100 : usage.Value;
            parts.Add($"{percent:0.#}%");
        }
        if (tokens is not null) parts.Add($"{tokens.Value:N0} tokens");
        return parts.Count == 0 ? "Unknown" : string.Join(", ", parts);
    }

    private static string FormatTokenRate(double value)
        => value.ToString("0.0", CultureInfo.InvariantCulture);
}
