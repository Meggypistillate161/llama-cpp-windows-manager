
namespace LocalLlmConsole.Services;

public static class RuntimeAdapter
{
    public static ValidationResult Validate(RuntimeLaunchRequest request)
    {
        var errors = new List<string>();
        if (request.Backend == RuntimeBackend.Metal && OperatingSystem.IsWindows())
            errors.Add("Metal is only supported on macOS.");
        if (request.Mode == RuntimeMode.Wsl && !OperatingSystem.IsWindows())
            errors.Add("WSL mode is only supported on Windows.");
        if (request.Mode == RuntimeMode.Wsl && string.IsNullOrWhiteSpace(request.WslDistro))
            errors.Add("WSL runtime requires an Ubuntu distro name.");
        if (request.Mode == RuntimeMode.Native && request.WslDistro is { Length: > 0 })
            errors.Add("WSL distro must be empty for native launches.");
        if (request.Port is < 1 or > 65535)
            errors.Add("Port must be between 1 and 65535.");
        var host = NormalizeHost(request.Host);
        if (string.IsNullOrWhiteSpace(host))
            errors.Add("Runtime host is required.");
        else if (!IsLocalHost(host) && !request.AllowNetworkAccess)
            errors.Add("Runtime host must default to localhost. Enable LAN model access before exposing a model on the network.");
        else if (!IsLocalHost(host) && !IsBindableHost(host))
            errors.Add("Runtime host must be a valid host name or IP address.");
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            errors.Add("Model serving requires a model API key, including local-only mode.");
        else if (!ApiSecurity.IsStrongBearerSecret(request.ApiKey))
            errors.Add("Model API key must be at least 32 non-whitespace characters.");
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
            errors.Add("llama-server executable path is required.");
        if (string.IsNullOrWhiteSpace(request.ModelPath))
            errors.Add("Model path is required.");
        if (request.ContextSize != 0 && request.ContextSize < 512)
            errors.Add("Context size must be 0 for model default or at least 512.");
        if (request.ContextSize > 1_048_576)
            errors.Add("Context size is too large.");
        if (request.Backend == RuntimeBackend.Cuda && request.GpuLayers < 0)
            errors.Add("CUDA GPU layers cannot be negative.");
        if (request.GpuLayers < 0)
            errors.Add("GPU layers cannot be negative.");
        if (request.GpuLayers > 10_000)
            errors.Add("GPU layers is too large.");
        if (request.ParallelSlots < 1)
            errors.Add("Parallel slots must be at least 1.");
        if (request.ParallelSlots > 128)
            errors.Add("Parallel slots is too large.");
        if (request.BatchSize < 1)
            errors.Add("Batch size must be at least 1.");
        if (request.BatchSize > 1_048_576)
            errors.Add("Batch size is too large.");
        if (request.MicroBatchSize < 1)
            errors.Add("Micro batch size must be at least 1.");
        if (request.MicroBatchSize > request.BatchSize)
            errors.Add("Micro batch size cannot be larger than batch size.");
        if (request.Threads < 0)
            errors.Add("Threads cannot be negative. Use 0 for auto.");
        if (request.Threads > 1024)
            errors.Add("Threads is too large.");
        if (request.ReasoningBudget < -1)
            errors.Add("Reasoning budget must be -1 or greater.");
        if (request.ReasoningBudget > 1_048_576)
            errors.Add("Reasoning budget is too large.");
        if (request.Temperature < 0)
            errors.Add("Temperature cannot be negative.");
        if (request.Temperature > 10)
            errors.Add("Temperature is too large.");
        if (request.TopK < 0)
            errors.Add("Top K cannot be negative.");
        if (request.TopK > 100_000)
            errors.Add("Top K is too large.");
        if (request.TopP is < 0 or > 1)
            errors.Add("Top P must be between 0 and 1.");
        if (request.MinP is < 0 or > 1)
            errors.Add("Min P must be between 0 and 1.");
        if (request.MaxTokens < -1)
            errors.Add("Max tokens must be -1 for unlimited or greater.");
        if (request.MaxTokens > 1_048_576)
            errors.Add("Max tokens is too large.");
        if (request.Seed < -1)
            errors.Add("Seed must be -1 for random or greater.");
        if (request.RepeatLastN < -1)
            errors.Add("Repeat window must be -1 or greater.");
        if (request.RepeatLastN > 1_048_576)
            errors.Add("Repeat window is too large.");
        if (request.RepeatPenalty < 0)
            errors.Add("Repeat penalty cannot be negative.");
        if (request.RepeatPenalty > 10)
            errors.Add("Repeat penalty is too large.");
        if (request.PresencePenalty is < -10 or > 10)
            errors.Add("Presence penalty must be between -10 and 10.");
        if (request.FrequencyPenalty is < -10 or > 10)
            errors.Add("Frequency penalty must be between -10 and 10.");
        if (!IsOneOf(request.FlashAttention, "auto", "on", "off"))
            errors.Add("Flash attention must be auto, on, or off.");
        if (!IsOneOf(request.CacheTypeK, CacheTypes))
            errors.Add($"K cache type must be one of: {string.Join(", ", CacheTypes)}.");
        if (!IsOneOf(request.CacheTypeV, CacheTypes))
            errors.Add($"V cache type must be one of: {string.Join(", ", CacheTypes)}.");
        if (!IsOneOf(request.KvOffload, "auto", "on", "off"))
            errors.Add("KV offload must be auto, on, or off.");
        if (!IsOneOf(request.KvUnified, "auto", "on", "off"))
            errors.Add("Unified KV must be auto, on, or off.");
        if (!IsOneOf(request.ContinuousBatching, "on", "off"))
            errors.Add("Continuous batching must be on or off.");
        if (!IsOneOf(request.ReasoningMode, "auto", "on", "off"))
            errors.Add("Reasoning mode must be auto, on, or off.");
        if (!IsOneOf(request.ReasoningFormat, "auto", "none", "deepseek", "deepseek-legacy"))
            errors.Add("Reasoning format must be auto, none, deepseek, or deepseek-legacy.");
        if (!IsOneOf(request.VisionMode, "auto", "on", "off"))
            errors.Add("Vision mode must be auto, on, or off.");
        if (request.VisionMode == "on" && string.IsNullOrWhiteSpace(request.VisionProjectorPath))
            errors.Add("Vision is on, but no mmproj/projector GGUF file was found next to the model.");
        if (request.VisionImageMinTokens < 0)
            errors.Add("Image min tokens cannot be negative.");
        if (request.VisionImageMinTokens > 1_048_576)
            errors.Add("Image min tokens is too large.");
        if (request.VisionImageMaxTokens < 0)
            errors.Add("Image max tokens cannot be negative.");
        if (request.VisionImageMaxTokens > 1_048_576)
            errors.Add("Image max tokens is too large.");
        if (request.VisionImageMaxTokens > 0 && request.VisionImageMinTokens > request.VisionImageMaxTokens)
            errors.Add("Image min tokens cannot be larger than image max tokens.");
        if (!IsOneOf(request.JinjaMode, "auto", "on", "off"))
            errors.Add("Jinja mode must be auto, on, or off.");
        if (!IsOneOf(request.MmapMode, "auto", "on", "off"))
            errors.Add("Mmap mode must be auto, on, or off.");
        if (!IsOneOf(request.MlockMode, "on", "off"))
            errors.Add("Mlock mode must be on or off.");
        if (!IsOneOf(request.RopeScaling, "auto", "none", "linear", "yarn"))
            errors.Add("RoPE scaling must be auto, none, linear, or yarn.");
        if (request.RopeScale < 0)
            errors.Add("RoPE scale cannot be negative.");
        if (request.RopeFreqBase < 0)
            errors.Add("RoPE base cannot be negative.");
        if (request.RopeFreqScale < 0)
            errors.Add("RoPE frequency scale cannot be negative.");
        if (!IsOneOf(request.SpeculativeType, SpeculativeTypes))
            errors.Add($"Speculative type must be one of: {string.Join(", ", SpeculativeTypes)}.");
        if (request.SpecDraftGpuLayers < -1)
            errors.Add("Draft GPU layers must be -1 for auto/unset or greater.");
        if (request.SpecDraftGpuLayers > 10_000)
            errors.Add("Draft GPU layers is too large.");
        if (request.SpecDraftMinTokens < 0)
            errors.Add("Draft min tokens cannot be negative.");
        if (request.SpecDraftMaxTokens < 0)
            errors.Add("Draft max tokens cannot be negative.");
        if (request.SpecDraftMaxTokens > 0 && request.SpecDraftMinTokens > request.SpecDraftMaxTokens)
            errors.Add("Draft min tokens cannot be larger than draft max tokens.");
        if (request.SpecDraftPSplit > 1 || (request.SpecDraftPSplit < 0 && Math.Abs(request.SpecDraftPSplit + 1) > 0.000_001))
            errors.Add("Draft split probability must be -1 for default or between 0 and 1.");
        if (request.SpecDraftPMin > 1 || (request.SpecDraftPMin < 0 && Math.Abs(request.SpecDraftPMin + 1) > 0.000_001))
            errors.Add("Draft min probability must be -1 for default or between 0 and 1.");
        if (!IsOneOf(request.SpecDraftCacheTypeK, CacheTypes))
            errors.Add($"Draft K cache type must be one of: {string.Join(", ", CacheTypes)}.");
        if (!IsOneOf(request.SpecDraftCacheTypeV, CacheTypes))
            errors.Add($"Draft V cache type must be one of: {string.Join(", ", CacheTypes)}.");
        return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Fail(errors);
    }

    public static IReadOnlyList<string> BuildArgs(RuntimeLaunchRequest request)
    {
        var validation = Validate(request);
        if (!validation.Ok) throw new InvalidOperationException(string.Join(" ", validation.Errors));
        var host = NormalizeHost(request.Host);
        var ropeScaling = (request.RopeScaling ?? "auto").Trim().ToLowerInvariant();
        var speculativeType = (request.SpeculativeType ?? "none").Trim().ToLowerInvariant();
        var args = new List<string>
        {
            "--model", request.ModelPath,
            "--host", host,
            "--port", request.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--ctx-size", request.ContextSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            args.AddRange(["--api-key", request.ApiKey.Trim()]);
        if (request.Backend is RuntimeBackend.Cuda or RuntimeBackend.Vulkan)
            args.AddRange(["--n-gpu-layers", request.GpuLayers.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        args.AddRange([
            "--parallel", request.ParallelSlots.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--batch-size", request.BatchSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--ubatch-size", request.MicroBatchSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--flash-attn", request.FlashAttention,
            "--cache-type-k", request.CacheTypeK,
            "--cache-type-v", request.CacheTypeV,
            "--temp", request.Temperature.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "--top-k", request.TopK.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--top-p", request.TopP.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "--min-p", request.MinP.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "--repeat-last-n", request.RepeatLastN.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--repeat-penalty", request.RepeatPenalty.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "--presence-penalty", request.PresencePenalty.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "--frequency-penalty", request.FrequencyPenalty.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        ]);
        if (request.MaxTokens >= 0)
            args.AddRange(["--predict", request.MaxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (request.Seed >= 0)
            args.AddRange(["--seed", request.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (request.Threads > 0)
            args.AddRange(["--threads", request.Threads.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (ropeScaling != "auto")
            args.AddRange(["--rope-scaling", ropeScaling]);
        if (request.RopeScale > 0)
            args.AddRange(["--rope-scale", request.RopeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);
        if (request.RopeFreqBase > 0)
            args.AddRange(["--rope-freq-base", request.RopeFreqBase.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);
        if (request.RopeFreqScale > 0)
            args.AddRange(["--rope-freq-scale", request.RopeFreqScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);
        if (request.KvOffload == "on")
            args.Add("--kv-offload");
        else if (request.KvOffload == "off")
            args.Add("--no-kv-offload");
        if (request.KvUnified == "on")
            args.Add("--kv-unified");
        else if (request.KvUnified == "off")
            args.Add("--no-kv-unified");
        if (request.ContinuousBatching == "on")
            args.Add("--cont-batching");
        else if (request.ContinuousBatching == "off")
            args.Add("--no-cont-batching");
        if (request.ReasoningMode != "auto")
            args.AddRange(["--reasoning", request.ReasoningMode]);
        if (request.ReasoningFormat != "auto")
            args.AddRange(["--reasoning-format", request.ReasoningFormat]);
        if (request.ReasoningBudget >= 0)
            args.AddRange(["--reasoning-budget", request.ReasoningBudget.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (request.VisionMode == "off")
            args.Add("--no-mmproj");
        else if (!string.IsNullOrWhiteSpace(request.VisionProjectorPath))
            args.AddRange(["--mmproj", request.VisionProjectorPath]);
        if (request.VisionMode != "off" && request.VisionImageMinTokens > 0)
            args.AddRange(["--image-min-tokens", request.VisionImageMinTokens.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (request.VisionMode != "off" && request.VisionImageMaxTokens > 0)
            args.AddRange(["--image-max-tokens", request.VisionImageMaxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (request.JinjaMode == "on")
            args.Add("--jinja");
        else if (request.JinjaMode == "off")
            args.Add("--no-jinja");
        if (request.MmapMode == "on")
            args.Add("--mmap");
        else if (request.MmapMode == "off")
            args.Add("--no-mmap");
        if (request.MlockMode == "on")
            args.Add("--mlock");
        if (speculativeType != "none")
        {
            args.AddRange(["--spec-type", speculativeType]);
            if (speculativeType.StartsWith("draft-", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(request.SpecDraftModelPath))
                    args.AddRange(["--model-draft", request.SpecDraftModelPath.Trim()]);
                if (request.SpecDraftGpuLayers >= 0)
                    args.AddRange(["--n-gpu-layers-draft", request.SpecDraftGpuLayers.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                if (request.SpecDraftMinTokens > 0)
                    args.AddRange(["--spec-draft-n-min", request.SpecDraftMinTokens.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                if (request.SpecDraftMaxTokens > 0)
                    args.AddRange(["--spec-draft-n-max", request.SpecDraftMaxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                if (request.SpecDraftPSplit >= 0)
                    args.AddRange(["--spec-draft-p-split", request.SpecDraftPSplit.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);
                if (request.SpecDraftPMin >= 0)
                    args.AddRange(["--spec-draft-p-min", request.SpecDraftPMin.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);
                args.AddRange([
                    "--cache-type-k-draft", request.SpecDraftCacheTypeK,
                    "--cache-type-v-draft", request.SpecDraftCacheTypeV
                ]);
            }
        }
        args.AddRange(request.ExtraArgs.Where(arg => !string.IsNullOrWhiteSpace(arg)));
        return args;
    }

    private static readonly string[] CacheTypes = ["f16", "q8_0", "q4_0", "q4_1", "iq4_nl", "q5_0", "q5_1", "f32", "bf16"];
    private static readonly string[] SpeculativeTypes = ["none", "draft-mtp", "draft-simple", "draft-eagle3", "ngram-simple", "ngram-map-k", "ngram-map-k4v", "ngram-mod", "ngram-cache"];

    private static bool IsOneOf(string value, params string[] allowed)
        => allowed.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeHost(string host) => (host ?? "").Trim();

    private static bool IsLocalHost(string host)
        => string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

    private static bool IsBindableHost(string host)
        => string.Equals(host, "::", StringComparison.Ordinal)
            || Uri.CheckHostName(host) != UriHostNameType.Unknown;
}
