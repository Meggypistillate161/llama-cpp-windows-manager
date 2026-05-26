
namespace LocalLlmConsole.Services;

public sealed record ModelCapabilitySummary(
    bool HasMetadata,
    string Architecture,
    string Quantization,
    int ContextLength,
    bool HasChatTemplate,
    bool HasVisionProjector,
    bool LikelyVision,
    bool IsMoe,
    bool LikelyReasoning,
    bool IsEmbedding,
    bool IsFim);

public static class ModelCapabilityService
{
    private sealed record HuggingFaceModelHints(HashSet<string> Capabilities, bool HasVisionProjector);

    public static ModelCapabilitySummary Empty()
        => new(false, "unknown", "", 0, false, false, false, false, false, false, false);

    public static string CacheKey(ModelRecord model)
    {
        var length = 0L;
        var updated = DateTimeOffset.MinValue;
        var projectorPath = "";
        var projectorLength = 0L;
        var projectorUpdated = DateTimeOffset.MinValue;
        try
        {
            var info = new FileInfo(model.ModelPath);
            if (info.Exists)
            {
                length = info.Length;
                updated = info.LastWriteTimeUtc;
            }

            projectorPath = ModelCatalogService.FindVisionProjector(model.ModelPath) ?? "";
            if (!string.IsNullOrWhiteSpace(projectorPath))
            {
                var projectorInfo = new FileInfo(projectorPath);
                if (projectorInfo.Exists)
                {
                    projectorLength = projectorInfo.Length;
                    projectorUpdated = projectorInfo.LastWriteTimeUtc;
                }
            }
        }
        catch
        {
            // Fall back to record data below.
        }

        return $"{model.Id}|{model.ModelPath}|{model.UpdatedAt:O}|{length}|{updated:O}|{projectorPath}|{projectorLength}|{projectorUpdated:O}";
    }

    public static ModelCapabilitySummary Inspect(ModelRecord model)
    {
        var metadata = GgufMetadataReader.TryRead(model.ModelPath);
        var hfHints = HuggingFaceHintsFromMetadata(model.MetadataJson);
        var architecture = StringMetadata(metadata, "general.architecture");
        var quant = ModelCatalogService.InferQuant(model.ModelPath);
        if (string.IsNullOrWhiteSpace(quant))
            quant = StringMetadata(metadata, "general.file_type");
        var context = ContextLength(metadata, architecture);
        var chatTemplate = StringMetadata(metadata, "tokenizer.chat_template");
        var metadataText = string.Join(" ", metadata.Select(pair => $"{pair.Key} {pair.Value}"));
        var nameText = $"{model.Name} {Path.GetFileName(model.ModelPath)} {model.MetadataJson}";
        var combined = $"{metadataText} {nameText}";
        var hasVisionProjector = hfHints.HasVisionProjector || ModelCatalogService.FindVisionProjector(model.ModelPath) is not null;
        var likelyVision = hasVisionProjector || hfHints.Capabilities.Contains("vision") || LooksVisionCapable(combined);
        var isMoe = metadata.Keys.Any(key => key.EndsWith(".expert_count", StringComparison.OrdinalIgnoreCase)
                || key.EndsWith(".expert_used_count", StringComparison.OrdinalIgnoreCase))
            || combined.Contains("ffn_gate_exp", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("moe", StringComparison.OrdinalIgnoreCase)
            || hfHints.Capabilities.Contains("moe");
        var likelyReasoning = !string.IsNullOrWhiteSpace(chatTemplate)
                && (chatTemplate.Contains("think", StringComparison.OrdinalIgnoreCase)
                    || chatTemplate.Contains("reasoning", StringComparison.OrdinalIgnoreCase))
            || nameText.Contains("qwen3", StringComparison.OrdinalIgnoreCase)
            || nameText.Contains("deepseek-r1", StringComparison.OrdinalIgnoreCase)
            || nameText.Contains("reasoning", StringComparison.OrdinalIgnoreCase)
            || nameText.Contains("gpt-oss", StringComparison.OrdinalIgnoreCase)
            || hfHints.Capabilities.Contains("reasoning");
        var isEmbedding = nameText.Contains("embed", StringComparison.OrdinalIgnoreCase)
            || nameText.Contains("rerank", StringComparison.OrdinalIgnoreCase)
            || StringMetadata(metadata, "pooling_type").Length > 0
            || hfHints.Capabilities.Contains("embedding");
        var isFim = combined.Contains("fim", StringComparison.OrdinalIgnoreCase)
            || metadata.Keys.Any(key => key.Contains("fim", StringComparison.OrdinalIgnoreCase))
            || hfHints.Capabilities.Contains("fim");
        return new ModelCapabilitySummary(
            metadata.Count > 0,
            string.IsNullOrWhiteSpace(architecture) ? "unknown" : architecture,
            string.IsNullOrWhiteSpace(quant) ? "unknown" : quant,
            context,
            !string.IsNullOrWhiteSpace(chatTemplate),
            hasVisionProjector,
            likelyVision,
            isMoe,
            likelyReasoning,
            isEmbedding,
            isFim);
    }

    public static string SummaryText(ModelCapabilitySummary capabilities)
    {
        var chips = new List<string>
        {
            $"Arch: {capabilities.Architecture}",
            $"Quant: {capabilities.Quantization}"
        };
        chips.Add(capabilities.ContextLength > 0 ? $"Context: {capabilities.ContextLength:N0}" : "Context: unknown");
        chips.Add(capabilities.HasChatTemplate ? "Chat template: found" : "Chat template: unknown");
        if (capabilities.HasVisionProjector) chips.Add("Vision: mmproj found");
        else if (capabilities.LikelyVision) chips.Add("Vision: likely, projector not found");
        if (capabilities.IsMoe) chips.Add("MoE");
        if (capabilities.LikelyReasoning) chips.Add("Reasoning likely");
        if (capabilities.IsEmbedding) chips.Add("Embedding/reranker");
        if (capabilities.IsFim) chips.Add("FIM");
        if (!capabilities.HasMetadata) chips.Add("GGUF metadata: unavailable");
        return string.Join("  |  ", chips);
    }

    public static int ContextLength(IReadOnlyDictionary<string, object?> metadata, string architecture)
    {
        if (!string.IsNullOrWhiteSpace(architecture)
            && IntMetadata(metadata, $"{architecture}.context_length") is { } context)
            return context;
        foreach (var pair in metadata)
        {
            if (pair.Key.EndsWith(".context_length", StringComparison.OrdinalIgnoreCase)
                && TryInt(pair.Value, out var value))
                return value;
        }
        return 0;
    }

    public static bool LooksVisionCapable(string text)
    {
        var markers = new[]
        {
            "vision", "visual", "image", "multimodal", "mmproj", "projector",
            "llava", "bakllava", "qwen2-vl", "qwen2.5-vl", "qwen3-vl",
            "internvl", "minicpm-v", "pixtral", "moondream", "mllama",
            "llama-3.2-vision", "gemma-3", "gemma3"
        };
        return markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string StringMetadata(IReadOnlyDictionary<string, object?> metadata, string key)
        => metadata.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "";

    private static HuggingFaceModelHints HuggingFaceHintsFromMetadata(string metadataJson)
    {
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasVisionProjector = false;
        try
        {
            var node = JsonNode.Parse(metadataJson);
            AddCapabilityHints(capabilities, node?["CapabilityHints"]?.ToString());
            AddCapabilityHints(capabilities, node?["capabilityHints"]?.ToString());
            AddCapabilityHints(capabilities, node?["pipelineTag"]?.ToString());
            AddCapabilityHints(capabilities, node?["PipelineTag"]?.ToString());
            var tags = node?["Tags"] as JsonArray ?? node?["tags"] as JsonArray;
            if (tags is not null)
            {
                foreach (var tag in tags)
                    AddCapabilityHints(capabilities, tag?.ToString());
            }

            hasVisionProjector = BoolMetadata(node, "HasVisionProjector")
                || BoolMetadata(node, "hasVisionProjector");
            var text = metadataJson;
            if (LooksVisionCapable(text)) capabilities.Add("vision");
            if (text.Contains("reasoning", StringComparison.OrdinalIgnoreCase) || text.Contains("deepseek-r1", StringComparison.OrdinalIgnoreCase)) capabilities.Add("reasoning");
            if (text.Contains("embedding", StringComparison.OrdinalIgnoreCase) || text.Contains("feature-extraction", StringComparison.OrdinalIgnoreCase)) capabilities.Add("embedding");
            if (text.Contains("fim", StringComparison.OrdinalIgnoreCase) || text.Contains("infill", StringComparison.OrdinalIgnoreCase)) capabilities.Add("fim");
            if (text.Contains("moe", StringComparison.OrdinalIgnoreCase)) capabilities.Add("moe");
        }
        catch
        {
            // Missing or older metadata simply means no HF hints.
        }

        return new HuggingFaceModelHints(capabilities, hasVisionProjector);
    }

    private static void AddCapabilityHints(HashSet<string> capabilities, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var part in value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Contains("vision", StringComparison.OrdinalIgnoreCase)
                || part.Contains("image", StringComparison.OrdinalIgnoreCase)
                || part.Contains("multimodal", StringComparison.OrdinalIgnoreCase)
                || part.Contains("llava", StringComparison.OrdinalIgnoreCase)
                || part.Contains("vl", StringComparison.OrdinalIgnoreCase))
                capabilities.Add("vision");
            if (part.Contains("chat", StringComparison.OrdinalIgnoreCase)
                || part.Contains("conversational", StringComparison.OrdinalIgnoreCase)
                || part.Contains("instruct", StringComparison.OrdinalIgnoreCase)
                || part.Contains("text-generation", StringComparison.OrdinalIgnoreCase))
                capabilities.Add("chat");
            if (part.Contains("embed", StringComparison.OrdinalIgnoreCase)
                || part.Contains("feature-extraction", StringComparison.OrdinalIgnoreCase)
                || part.Contains("sentence-similarity", StringComparison.OrdinalIgnoreCase)
                || part.Contains("rerank", StringComparison.OrdinalIgnoreCase))
                capabilities.Add("embedding");
            if (part.Contains("reasoning", StringComparison.OrdinalIgnoreCase)
                || part.Contains("qwen3", StringComparison.OrdinalIgnoreCase)
                || part.Contains("deepseek-r1", StringComparison.OrdinalIgnoreCase))
                capabilities.Add("reasoning");
            if (part.Contains("moe", StringComparison.OrdinalIgnoreCase) || part.Contains("expert", StringComparison.OrdinalIgnoreCase))
                capabilities.Add("moe");
            if (part.Contains("fim", StringComparison.OrdinalIgnoreCase)
                || part.Contains("infill", StringComparison.OrdinalIgnoreCase)
                || part.Contains("fill-in-the-middle", StringComparison.OrdinalIgnoreCase))
                capabilities.Add("fim");
        }
    }

    private static bool BoolMetadata(JsonNode? node, string key)
        => node?[key] is JsonValue value && value.TryGetValue<bool>(out var result) && result;

    private static int? IntMetadata(IReadOnlyDictionary<string, object?> metadata, string key)
        => metadata.TryGetValue(key, out var value) && TryInt(value, out var number) ? number : null;

    private static bool TryInt(object? value, out int number)
    {
        switch (value)
        {
            case byte b:
                number = b;
                return true;
            case sbyte sb:
                number = sb;
                return true;
            case ushort us:
                number = us;
                return true;
            case short s:
                number = s;
                return true;
            case uint ui when ui <= int.MaxValue:
                number = (int)ui;
                return true;
            case int i:
                number = i;
                return true;
            case ulong ul when ul <= int.MaxValue:
                number = (int)ul;
                return true;
            case long l when l <= int.MaxValue && l >= int.MinValue:
                number = (int)l;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }
}
