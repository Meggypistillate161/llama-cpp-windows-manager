
namespace LocalLlmConsole.Services;

public static class LaunchSettingMetadataService
{
    public static readonly IReadOnlyList<string> AutoOnOffOptions = ["auto", "on", "off"];
    public static readonly IReadOnlyList<string> OnOffOptions = ["on", "off"];
    public static readonly IReadOnlyList<string> OffOnOptions = ["off", "on"];
    public static readonly IReadOnlyList<string> CacheTypeOptions = ["f16", "q8_0", "q4_0", "q4_1", "iq4_nl", "q5_0", "q5_1", "f32", "bf16"];
    public static readonly IReadOnlyList<string> SpeculativeTypeOptions = ["none", "draft-mtp", "draft-simple", "draft-eagle3", "ngram-simple", "ngram-map-k", "ngram-map-k4v", "ngram-mod", "ngram-cache"];
    public static readonly IReadOnlyList<string> ReasoningFormatOptions = ["auto", "none", "deepseek", "deepseek-legacy"];
    public static readonly IReadOnlyList<string> RopeScalingOptions = ["auto", "none", "linear", "yarn"];

    public static string Tooltip(string label) => label switch
    {
        "Context size" => "How much text and conversation history the model can keep in memory. 0 uses the model's GGUF default. Shorthand like 196 or 196k becomes 200,704.",
        "Parallel slots" => "How many requests the server can work on at the same time. Keep this low for one local chat.",
        "Batch size" => "How many tokens llama.cpp processes in a larger chunk. Higher can be faster but uses more memory.",
        "Micro batch" => "The smaller chunk size used inside each batch. Lower it if you hit memory errors.",
        "Threads" => "CPU worker threads. 0 lets llama.cpp choose automatically.",
        "GPU layers" => "How much of the model to load onto the GPU. Higher is usually faster if you have enough VRAM.",
        "Reasoning" => "Turns model reasoning mode on, off, or lets llama.cpp decide from the model template.",
        "Reason format" => "Which reasoning tag format to expect. Use auto unless a model needs a specific style.",
        "Reason budget" => "Token budget for reasoning. -1 lets the model/server choose; 0 usually disables extra reasoning.",
        "Jinja chat" => "Uses the model's Jinja chat template when available. Auto is usually safest.",
        "Vision" => "Enables image input support when a matching mmproj/projector file is available.",
        "Image min" => "Minimum tokens each image may use for dynamic-resolution vision models. 0 leaves the model default.",
        "Image max" => "Maximum tokens each image may use for dynamic-resolution vision models. 0 leaves the model default.",
        "Flash attention" => "A faster attention mode that can reduce memory use. Disable only if the runtime/model has issues.",
        "K cache" => "Precision used for the key part of the KV cache. Smaller types save memory but can reduce quality.",
        "V cache" => "Precision used for the value part of the KV cache. Smaller types save memory but can reduce quality.",
        "KV offload" => "Moves the KV cache to GPU when possible. Faster, but uses more VRAM.",
        "Unified KV" => "Uses one shared KV cache layout for parallel slots. Auto is normally fine.",
        "Continuous batch" => "Keeps batching active between requests for better throughput.",
        "Memory map" => "Loads model weights using memory mapping. Usually faster startup and lower RAM pressure.",
        "Memory lock" => "Prevents model memory from being swapped to disk. Can help stability, but uses locked RAM.",
        "Metrics" => "Exposes llama.cpp metrics so Overview can show runtime stats.",
        "Temperature" => "Controls randomness. Lower is more focused; higher is more creative.",
        "Top K" => "Limits each next-token choice to the best K candidates. 0 disables this limit.",
        "Top P" => "Keeps only the most likely tokens up to this probability mass. Lower is more focused.",
        "Min P" => "Drops very unlikely tokens compared with the best token. Higher is stricter.",
        "Max tokens" => "Maximum tokens to generate for a request. -1 leaves generation unlimited.",
        "Seed" => "Random seed for repeatable output. -1 lets llama.cpp choose a random seed.",
        "Repeat window" => "How many previous tokens are considered for repeat penalties. 0 disables; -1 uses the full context.",
        "Repeat pen" => "Penalty applied to repeated tokens. 1.0 disables this penalty.",
        "Presence" => "Penalty based on whether a token has appeared. Positive values reduce repetition.",
        "Frequency" => "Penalty based on how often a token has appeared. Positive values reduce repetition.",
        "RoPE scaling" => "Optional RoPE scaling mode for long-context experiments. Auto leaves the model/runtime default alone.",
        "RoPE scale" => "RoPE context scaling factor. 0 leaves it unset.",
        "RoPE base" => "RoPE base frequency override. 0 leaves the model default alone.",
        "RoPE freq" => "RoPE frequency scale override. 0 leaves it unset.",
        "Spec type" => "Speculative decoding mode. draft-mtp uses llama.cpp's MTP path when the model/runtime supports it.",
        "Draft model" => "Optional draft GGUF for speculative decoding. Leave blank to auto-detect a nearby draft/MTP file when possible.",
        "Draft GPU" => "GPU layers for the draft model. -1 leaves llama.cpp's default/auto behavior.",
        "Draft K cache" => "KV cache type for the draft model key cache.",
        "Draft V cache" => "KV cache type for the draft model value cache.",
        "Draft max" => "Maximum number of draft tokens per speculative step. 0 uses llama.cpp's default.",
        "Draft min" => "Minimum number of draft tokens per speculative step. 0 uses llama.cpp's default.",
        "Split prob" => "Speculative split probability. -1 uses llama.cpp's default.",
        "Min prob" => "Minimum speculative acceptance probability. -1 uses llama.cpp's default.",
        _ => "Setting used when starting or restarting this model."
    };

    public static string ContextSizeTooltip(string text)
    {
        var tooltip = Tooltip("Context size");
        if (!LaunchSettingParser.TryNormalizeContextSize(text, out var value) || value <= 0)
            return tooltip;

        var normalized = value.ToString(CultureInfo.InvariantCulture);
        var compactText = (text ?? "")
            .Replace(",", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);
        return string.Equals(compactText, normalized, StringComparison.OrdinalIgnoreCase)
            ? tooltip
            : $"{tooltip}{Environment.NewLine}{Environment.NewLine}Suggestion: {value:N0} tokens.";
    }
}
