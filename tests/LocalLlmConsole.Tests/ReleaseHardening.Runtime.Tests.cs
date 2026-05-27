using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public void RuntimeSourceCleanupDefaultsOn()
    {
        var root = CreateTempRoot();

        var settings = AppSettings.CreateDefault(root);

        Assert.True(settings.DeleteRuntimeSourceAfterSuccessfulBuild);
    }


    [Fact]
    public void LlamaProcessSupervisorUsesCentralLogRedaction()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LlamaProcessSupervisor.cs"));

        Assert.Contains("LogFileService.RedactSensitiveText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization\\s*:\\s*Bearer", source, StringComparison.Ordinal);
    }


    [Fact]
    public void LlamaProcessSupervisorAttachLoadAndStopTransitionsAreExplicit()
    {
        using var supervisor = new LlamaProcessSupervisor();
        var root = CreateTempRoot();
        var runtime = new RuntimeRecord(
            "runtime-1",
            "Native CPU",
            RuntimeMode.Native,
            RuntimeBackend.Cpu,
            Path.Combine(root, "llama-server.exe"),
            "{}",
            DateTimeOffset.UtcNow);
        var settings = AppSettings.CreateDefault(root);

        supervisor.AttachExisting(runtime, "model-1", settings, Path.Combine(root, "runtime.log"), LlamaRuntimeState.Failed);

        Assert.True(supervisor.IsRunning);
        Assert.Equal("model-1", supervisor.ActiveModelId);
        Assert.Equal("runtime-1", supervisor.ActiveRuntimeId);
        Assert.Equal(LlamaRuntimeState.Loading, supervisor.State);
        Assert.True(supervisor.MarkLoadedIfRunning());
        Assert.Equal(LlamaRuntimeState.Loaded, supervisor.State);

        supervisor.Stop();

        Assert.False(supervisor.IsRunning);
        Assert.Equal("", supervisor.ActiveModelId);
        Assert.Equal("", supervisor.ActiveRuntimeId);
        Assert.Equal(LlamaRuntimeState.Stopped, supervisor.State);
        Assert.Null(supervisor.LastExitCode);
    }


    [Fact]
    public void RuntimeAdapterRejectsNetworkHostWithoutLanMode()
    {
        var request = ValidLaunchRequest() with { Host = "0.0.0.0" };

        var result = RuntimeAdapter.Validate(request);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("localhost", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void RuntimeAdapterAllowsNetworkHostWithExplicitLanModeAndApiKey()
    {
        var apiKey = new string('a', 32);
        var request = ValidLaunchRequest() with
        {
            Host = "0.0.0.0",
            AllowNetworkAccess = true,
            ApiKey = apiKey
        };

        var result = RuntimeAdapter.Validate(request);
        var args = RuntimeAdapter.BuildArgs(request);

        Assert.True(result.Ok);
        Assert.Contains("0.0.0.0", args);
        Assert.Contains("--api-key", args);
        Assert.Contains(apiKey, args);
    }


    [Fact]
    public void RuntimeAdapterRequiresApiKeyForModelServing()
    {
        var request = ValidLaunchRequest() with
        {
            Host = "0.0.0.0",
            AllowNetworkAccess = true,
            ApiKey = ""
        };

        var result = RuntimeAdapter.Validate(request);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("API key", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void RuntimeAdapterRejectsWeakApiKey()
    {
        var request = ValidLaunchRequest() with { ApiKey = "test-key" };

        var result = RuntimeAdapter.Validate(request);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("32", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void RuntimeAdapterRejectsExtremeLaunchValues()
    {
        var request = ValidLaunchRequest() with
        {
            ContextSize = int.MaxValue,
            BatchSize = int.MaxValue,
            MicroBatchSize = int.MaxValue,
            Threads = int.MaxValue
        };

        var result = RuntimeAdapter.Validate(request);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("Context", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("Batch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("Threads", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void RuntimeAdapterBuildsLocalOnlyArgs()
    {
        var args = RuntimeAdapter.BuildArgs(ValidLaunchRequest());

        Assert.Contains("--host", args);
        Assert.Contains("127.0.0.1", args);
        Assert.Contains("--port", args);
        Assert.Contains("8081", args);
        Assert.Contains("--api-key", args);
    }


    [Fact]
    public void RuntimeAdapterTreatsSyclAsGpuBackend()
    {
        var onArgs = RuntimeAdapter.BuildArgs(ValidLaunchRequest() with
        {
            Backend = RuntimeBackend.Sycl,
            GpuLayers = 99,
            MmapMode = "on"
        });
        var offArgs = RuntimeAdapter.BuildArgs(ValidLaunchRequest() with
        {
            Backend = RuntimeBackend.Sycl,
            GpuLayers = 88,
            MmapMode = "off"
        });

        Assert.Contains("--n-gpu-layers", onArgs);
        Assert.Contains("99", onArgs);
        Assert.Contains("--mmap", onArgs);
        Assert.Contains("--n-gpu-layers", offArgs);
        Assert.Contains("88", offArgs);
        Assert.Contains("--no-mmap", offArgs);
    }


    [Fact]
    public void RuntimeAdapterTreatsMetalAsGpuBackend()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeAdapter.cs"));

        Assert.Contains("request.Backend is RuntimeBackend.Cuda or RuntimeBackend.Vulkan or RuntimeBackend.Metal or RuntimeBackend.Sycl", source, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimeAdapterValidatesVisionProjectorPairing()
    {
        var missing = RuntimeAdapter.Validate(ValidLaunchRequest() with { VisionMode = "on", VisionProjectorPath = "" });

        Assert.False(missing.Ok);
        Assert.Contains(missing.Errors, error => error.Contains("mmproj", StringComparison.OrdinalIgnoreCase));

        var args = RuntimeAdapter.BuildArgs(ValidLaunchRequest() with
        {
            VisionMode = "on",
            VisionProjectorPath = "mmproj.gguf",
            VisionImageMinTokens = 256,
            VisionImageMaxTokens = 1024
        });
        Assert.Contains("--mmproj", args);
        Assert.Contains("mmproj.gguf", args);
        Assert.Contains("--image-min-tokens", args);
        Assert.Contains("256", args);
        Assert.Contains("--image-max-tokens", args);
        Assert.Contains("1024", args);

        var offArgs = RuntimeAdapter.BuildArgs(ValidLaunchRequest() with { VisionMode = "off" });
        Assert.Contains("--no-mmproj", offArgs);

        var invalid = RuntimeAdapter.Validate(ValidLaunchRequest() with { VisionImageMinTokens = 2048, VisionImageMaxTokens = 1024 });
        Assert.False(invalid.Ok);
        Assert.Contains(invalid.Errors, error => error.Contains("Image min tokens", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void RuntimeAdapterBuildsSpeculativeSamplingAndRopeArgs()
    {
        var request = ValidLaunchRequest() with
        {
            SpeculativeType = "draft-mtp",
            SpecDraftModelPath = "draft.gguf",
            SpecDraftGpuLayers = 999,
            SpecDraftMinTokens = 1,
            SpecDraftMaxTokens = 4,
            SpecDraftPSplit = 0.2,
            SpecDraftPMin = 0.05,
            SpecDraftCacheTypeK = "q8_0",
            SpecDraftCacheTypeV = "q8_0",
            MaxTokens = 512,
            Seed = 1234,
            RepeatLastN = 128,
            RepeatPenalty = 1.08,
            PresencePenalty = 0.2,
            FrequencyPenalty = 0.1,
            RopeScaling = "yarn",
            RopeScale = 2,
            RopeFreqBase = 1_000_000,
            RopeFreqScale = 0.5
        };

        var args = RuntimeAdapter.BuildArgs(request);

        Assert.Contains("--spec-type", args);
        Assert.Contains("draft-mtp", args);
        Assert.Contains("--model-draft", args);
        Assert.Contains("draft.gguf", args);
        Assert.Contains("--n-gpu-layers-draft", args);
        Assert.Contains("999", args);
        Assert.Contains("--spec-draft-n-min", args);
        Assert.Contains("--spec-draft-n-max", args);
        Assert.Contains("--cache-type-k-draft", args);
        Assert.Contains("--cache-type-v-draft", args);
        Assert.Contains("--predict", args);
        Assert.Contains("512", args);
        Assert.Contains("--seed", args);
        Assert.Contains("1234", args);
        Assert.Contains("--repeat-last-n", args);
        Assert.Contains("--repeat-penalty", args);
        Assert.Contains("1.08", args);
        Assert.Contains("--presence-penalty", args);
        Assert.Contains("--frequency-penalty", args);
        Assert.Contains("--rope-scaling", args);
        Assert.Contains("yarn", args);
        Assert.Contains("--rope-scale", args);
        Assert.Contains("--rope-freq-base", args);
        Assert.Contains("--rope-freq-scale", args);
    }


    [Fact]
    public async Task RuntimeRegistryScanRegistersRuntimeOnceWhenRootContainsExecutable()
    {
        var root = CreateTempRoot();
        var runtimeRoot = Path.Combine(root, "runtimes");
        Directory.CreateDirectory(runtimeRoot);
        await File.WriteAllTextAsync(Path.Combine(runtimeRoot, "llama-server.exe"), "fake exe", TestContext.Current.CancellationToken);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var registry = new RuntimeRegistryService(store);

        var count = await registry.ScanAsync(runtimeRoot);
        var runtimes = await store.ListRuntimesAsync();

        Assert.Equal(1, count);
        var runtime = Assert.Single(runtimes);
        Assert.Equal(RuntimeMode.Native, runtime.Mode);
        Assert.Equal(RuntimeBackend.Cpu, runtime.Backend);
        Assert.Equal(Path.Combine(runtimeRoot, "llama-server.exe"), runtime.ExecutablePath);
    }


    [Fact]
    public async Task RuntimeRegistryInfersCudaFromNearbyRuntimeFiles()
    {
        var root = CreateTempRoot();
        var runtimeRoot = Path.Combine(root, "runtimes");
        var buildRoot = Path.Combine(runtimeRoot, "cuda-build");
        var binRoot = Path.Combine(buildRoot, "bin");
        var libRoot = Path.Combine(buildRoot, "lib");
        Directory.CreateDirectory(binRoot);
        Directory.CreateDirectory(libRoot);
        await File.WriteAllTextAsync(Path.Combine(binRoot, "llama-server"), "fake wsl binary", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(libRoot, "libcudart.so"), "fake cuda lib", TestContext.Current.CancellationToken);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var registry = new RuntimeRegistryService(store);

        var count = await registry.ScanAsync(runtimeRoot);
        var runtime = Assert.Single(await store.ListRuntimesAsync());

        Assert.Equal(1, count);
        Assert.Equal(RuntimeMode.Wsl, runtime.Mode);
        Assert.Equal(RuntimeBackend.Cuda, runtime.Backend);
        Assert.Equal(Path.Combine(binRoot, "llama-server"), runtime.ExecutablePath);
    }


    [Fact]
    public void RuntimeAdapterRejectsInvalidSpeculativeSettings()
    {
        var request = ValidLaunchRequest() with
        {
            SpeculativeType = "maybe-mtp",
            SpecDraftMinTokens = 8,
            SpecDraftMaxTokens = 4,
            SpecDraftPSplit = 2,
            RopeScaling = "banana"
        };

        var result = RuntimeAdapter.Validate(request);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("Speculative type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("Draft min tokens", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("Draft split", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("RoPE", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void RuntimeMetricsParseAndAggregatePrometheusSamples()
    {
        const string raw = """
        # HELP llama_tokens_predicted_total Predicted tokens.
        # TYPE llama_tokens_predicted_total counter
        llama_tokens_predicted_total 12
        llama_prompt_tokens_seconds{slot="0"} 3.5
        llama_kv_cache_usage_ratio NaN
        """;

        var samples = RuntimeMetrics.ParsePrometheus(raw);

        Assert.Equal(3, samples.Count);
        Assert.Equal(12, RuntimeMetrics.Sum(samples, ["tokens", "predicted", "total"], []));
        Assert.Equal(3.5, RuntimeMetrics.First(samples, ["prompt", "tokens", "seconds"], ["total"]));
        Assert.Null(RuntimeMetrics.First(samples, ["kv", "cache", "usage"], []));
        Assert.Equal("counter", samples.Single(sample => sample.Name == "llama_tokens_predicted_total").Type);
    }


    [Fact]
    public void RuntimeDashboardServiceParsesSlotsAndFormatsLabels()
    {
        const string raw = """
        [
          {
            "is_processing": true,
            "n_prompt_tokens_processed": 12,
            "n_decoded": 8,
            "n_prompt_tokens": "20",
            "n_ctx": 4096
          },
          {
            "next_token": [
              { "n_decoded": 5, "has_next_token": true }
            ],
            "prompt_tokens_processed": 3,
            "context_size": "2048"
          }
        ]
        """;

        var snapshot = RuntimeDashboardService.ParseSlotSnapshot(raw);

        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsProcessing);
        Assert.Equal(15, snapshot.PromptTokensProcessed);
        Assert.Equal(13, snapshot.GeneratedTokens);
        Assert.Equal(20, snapshot.PromptTokens);
        Assert.Equal(28, snapshot.ContextTokens);
        Assert.Equal(6144, snapshot.ContextSize);
        Assert.Equal(2, RuntimeDashboardService.DeltaRate(14, 10, 2, includeZero: false));
        Assert.Null(RuntimeDashboardService.DeltaRate(10, 10, 2, includeZero: false));
        Assert.Equal(0, RuntimeDashboardService.DeltaRate(10, 10, 2, includeZero: true));
        Assert.Equal(4, RuntimeDashboardService.WholePositiveDelta(7.9, 3.1));
        double? lifetimeCounter = 10;
        Assert.Equal(0, RuntimeDashboardService.WholePositiveDeltaAndRemember(null, ref lifetimeCounter));
        Assert.Equal(10, lifetimeCounter);
        Assert.Equal(5, RuntimeDashboardService.WholePositiveDeltaAndRemember(15.9, ref lifetimeCounter));
        Assert.Equal(15.9, lifetimeCounter);
        Assert.Equal(0, RuntimeDashboardService.WholePositiveDeltaAndRemember(2, ref lifetimeCounter));
        Assert.Equal(2, lifetimeCounter);
        Assert.True(RuntimeDashboardService.PositiveDelta(4, 3));
        Assert.Equal("Gen 13\nPrompt 15", RuntimeDashboardService.TokenSummaryLabel(13, 15));
        Assert.Equal("2.0 t/s (3.0 avg)", RuntimeDashboardService.RateLabel(2, 3));
        Assert.Equal("Context 6,144\nKV cache 50%, 28 tokens", RuntimeDashboardService.RuntimeSettingsLabel(.5, 28, 6144, 4096));
        var capturedAt = DateTimeOffset.Parse("2026-05-26T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("Gen 13\nPrompt 15\nLast known 5s ago", RuntimeDashboardService.WithLastKnownLine("Gen 13\nPrompt 15", capturedAt, capturedAt.AddSeconds(5)));
    }


    [Fact]
    public void RuntimeLifetimeCounterTrackerTracksRuntimeKeysAndUsesSlotFallback()
    {
        var tracker = new RuntimeLifetimeCounterTracker();
        var firstKey = "model-a|runtime-a|8081";
        var secondKey = "model-b|runtime-b|8082";

        Assert.False(tracker.Observe(firstKey, "model-a", "Model A", generatedCounter: 10, promptCounter: 5, slotSnapshot: null).HasTokens);
        var firstDelta = tracker.Observe(firstKey, "model-a", "Model A", generatedCounter: 14, promptCounter: 9, slotSnapshot: null);

        Assert.Equal("model-a", firstDelta.ModelId);
        Assert.Equal(4, firstDelta.GeneratedTokens);
        Assert.Equal(4, firstDelta.PromptTokens);

        Assert.False(tracker.Observe(secondKey, "model-b", "Model B", generatedCounter: null, promptCounter: null, new RuntimeSlotSnapshot(20, 50, false, null, null, null)).HasTokens);
        var secondDelta = tracker.Observe(secondKey, "model-b", "Model B", generatedCounter: null, promptCounter: null, new RuntimeSlotSnapshot(26, 63, false, null, null, null));

        Assert.Equal("model-b", secondDelta.ModelId);
        Assert.Equal(13, secondDelta.GeneratedTokens);
        Assert.Equal(6, secondDelta.PromptTokens);

        tracker.RetainRuntimeKeys([secondKey]);
        Assert.Equal(1, tracker.Count);
        Assert.False(tracker.Observe(firstKey, "model-a", "Model A", generatedCounter: 100, promptCounter: 100, slotSnapshot: null).HasTokens);
    }


    [Fact]
    public void RuntimeIdleUnloadTrackerTracksEachRuntimeKeyIndependently()
    {
        var tracker = new RuntimeIdleUnloadTracker();
        var now = DateTimeOffset.Parse("2026-05-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var firstKey = "model-a|runtime-a|8081";
        var secondKey = "model-b|runtime-b|8082";

        Assert.False(tracker.Observe(firstKey, new RuntimeSlotSnapshot(0, 0, false, null, null, null), generatedCounter: null, promptCounter: null, idleMinutes: 1, now));
        Assert.False(tracker.Observe(secondKey, new RuntimeSlotSnapshot(0, 0, false, null, null, null), generatedCounter: null, promptCounter: null, idleMinutes: 1, now));

        Assert.True(tracker.Observe(firstKey, new RuntimeSlotSnapshot(0, 0, false, null, null, null), generatedCounter: null, promptCounter: null, idleMinutes: 1, now.AddSeconds(61)));
        Assert.False(tracker.Observe(secondKey, new RuntimeSlotSnapshot(0, 0, true, null, null, null), generatedCounter: null, promptCounter: null, idleMinutes: 1, now.AddSeconds(61)));
        Assert.False(tracker.Observe(secondKey, new RuntimeSlotSnapshot(0, 0, false, null, null, null), generatedCounter: null, promptCounter: null, idleMinutes: 1, now.AddSeconds(90)));
        Assert.True(tracker.Observe(secondKey, new RuntimeSlotSnapshot(0, 0, false, null, null, null), generatedCounter: null, promptCounter: null, idleMinutes: 1, now.AddSeconds(122)));

        tracker.RetainRuntimeKeys([secondKey]);
        Assert.Equal(1, tracker.Count);
        tracker.Reset(secondKey);
        Assert.Equal(0, tracker.Count);
    }


    [Fact]
    public void RuntimeDashboardPollsAllLoadedSessionsForLifetimeMetricsBeforeRenderingSelection()
    {
        var dashboard = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeDashboard.cs"));
        var counters = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeMetricCounters.cs"));
        var metrics = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeMetrics.cs"));

        Assert.Contains("PollRuntimeMetricsForSessionsAsync(_sessions.Snapshots()", dashboard, StringComparison.Ordinal);
        Assert.Contains("Where(session => session is { IsRunning: true, Status: LoadedModelSessionStatus.Running or LoadedModelSessionStatus.Warm })", dashboard, StringComparison.Ordinal);
        Assert.Contains("await TrackLifetimeTokenDeltasAsync(pollResults)", dashboard, StringComparison.Ordinal);
        Assert.True(
            dashboard.IndexOf("await TrackLifetimeTokenDeltasAsync(pollResults)", StringComparison.Ordinal)
            < dashboard.IndexOf("if (selectedOverviewModel is not null && _sessions.SessionForModel", StringComparison.Ordinal));
        Assert.DoesNotContain("ResetLifetimeCounters();", dashboard, StringComparison.Ordinal);
        var lifetimeStart = counters.IndexOf("private async Task TrackLifetimeTokenDeltasAsync", StringComparison.Ordinal);
        var lifetimeEnd = counters.IndexOf("private void ResetLifetimeCounters()", lifetimeStart, StringComparison.Ordinal);
        Assert.DoesNotContain("_llama.ActiveModelId", counters[lifetimeStart..lifetimeEnd], StringComparison.Ordinal);
        Assert.Contains("RuntimeGeneratedTokenCounter(result.Samples)", counters, StringComparison.Ordinal);
        Assert.Contains("RuntimePromptTokenCounter(result.Samples)", counters, StringComparison.Ordinal);
        Assert.Contains("result.SlotSnapshot", counters, StringComparison.Ordinal);
        Assert.Contains("RuntimeMetricKey(LoadedModelSessionSnapshot session)", metrics, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimeDashboardAppliesIdleUnloadPolicyToAllLoadedSessions()
    {
        var dashboard = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeDashboard.cs"));
        var counters = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeMetricCounters.cs"));

        Assert.Contains("await ApplyIdleUnloadPoliciesAsync(pollResults)", dashboard, StringComparison.Ordinal);
        Assert.Contains("_idleUnloadTracker.RetainRuntimeKeys(pollResults.Select(result => result.RuntimeKey))", counters, StringComparison.Ordinal);
        Assert.Contains("_idleUnloadTracker.Observe(result.RuntimeKey", counters, StringComparison.Ordinal);
        Assert.Contains("await StopModelRuntimeAsync(model)", counters, StringComparison.Ordinal);
        Assert.DoesNotContain("StopLoadedRuntimeAsync()", counters, StringComparison.Ordinal);
        Assert.DoesNotContain("_llama.ActiveModelId", counters, StringComparison.Ordinal);
    }


    [Fact]
    public void DisplayFormatServiceFormatsMetricsBytesElapsedAndLongText()
    {
        Assert.Equal("0s", DisplayFormatService.Elapsed(TimeSpan.FromSeconds(-1)));
        Assert.Equal("59s", DisplayFormatService.Elapsed(TimeSpan.FromSeconds(59.9)));
        Assert.Equal("1m 05s", DisplayFormatService.Elapsed(TimeSpan.FromSeconds(65)));
        Assert.Equal("2h 03m 04s", DisplayFormatService.Elapsed(new TimeSpan(2, 3, 4)));
        Assert.Equal("1.5 KB", DisplayFormatService.Bytes(1536));
        Assert.Equal("", DisplayFormatService.Bytes(0));
        Assert.Equal("0 B", DisplayFormatService.BytesOrZero(0));
        Assert.Equal("12.346", DisplayFormatService.MetricNumber(12.3456));
        Assert.Equal("No release notes were provided.", DisplayFormatService.TrimForDisplay("", 100));
        Assert.Equal("abcdef\n\n...", DisplayFormatService.TrimForDisplay("abcdefgh", 6));
    }


    [Fact]
    public void GpuStatusServiceFormatsNvidiaSmiCsvLine()
    {
        var formatted = GpuStatusService.FormatNvidiaSmiCsvLine("0, NVIDIA RTX, 76, 62, 12288, 24576");

        Assert.Equal("GPU 0: 76% | 62C | 12.0/24.0 GiB", formatted);
    }


    [Fact]
    public void GpuStatusServiceFormatsIntelArcSyclLine()
    {
        var formatted = GpuStatusService.FormatIntelArcStatus("[level_zero:gpu][level_zero:0] Intel(R) Arc(TM) A770 Graphics");

        Assert.Equal("Intel(R) Arc(TM) A770 Graphics", formatted);
        Assert.Equal("Intel Arc GPU", GpuStatusService.FormatIntelArcStatus(""));
    }


    [Fact]
    public void RuntimeEndpointServiceBuildsLocalAndLanUrls()
    {
        var root = CreateTempRoot();
        var local = AppSettings.CreateDefault(root) with
        {
            Host = "0.0.0.0",
            Port = 8081,
            ModelAccessMode = "local"
        };
        var lan = local with { ModelAccessMode = "lan", Host = "192.168.1.20" };

        Assert.Equal("http://127.0.0.1:8081", RuntimeEndpointService.LocalServerBaseUrl(local));
        Assert.Equal("http://127.0.0.1:8081/v1", RuntimeEndpointService.LocalOpenAiBaseUrl(local));
        Assert.Equal("http://192.168.1.20:8081/v1", RuntimeEndpointService.LanOpenAiBaseUrl(lan));
        Assert.Equal("http://192.168.1.20:8081/v1", RuntimeEndpointService.EndpointDisplay(lan));
        Assert.Equal("[::1]", RuntimeEndpointService.UrlHost("::1"));
    }


    [Fact]
    public void RuntimeEndpointServiceAddsBearerTokenWhenPresent()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { ModelApiKey = "  secret-token  " };

        using var request = RuntimeEndpointService.RuntimeGetRequest("http://127.0.0.1:8081/health", settings);

        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization?.Parameter);
    }


    [Fact]
    public void RuntimeEndpointServiceParsesServedModelsAndMatchesRegistrations()
    {
        const string json = """
        {
          "data": [
            { "id": "registered-id" },
            { "model": "D:\\models\\Qwen3-8B.gguf" },
            { "name": "Friendly Qwen" }
          ],
          "models": [ "plain-model" ]
        }
        """;
        var now = DateTimeOffset.UtcNow;
        var model = new ModelRecord("registered-id", "Friendly Qwen", @"D:\models\Qwen3-8B.gguf", OwnershipKind.External, "{}", now);

        var served = RuntimeEndpointService.ExtractServedModelIds(json).ToArray();

        Assert.Equal(["registered-id", @"D:\models\Qwen3-8B.gguf", "Friendly Qwen", "plain-model"], served);
        Assert.True(RuntimeEndpointService.ServedModelMatches(model, "registered-id"));
        Assert.True(RuntimeEndpointService.ServedModelMatches(model, @"D:\other\Qwen3-8B.gguf"));
        Assert.True(RuntimeEndpointService.ServedModelMatches(model, "Friendly Qwen"));
        Assert.False(RuntimeEndpointService.ServedModelMatches(model, "other-model"));
    }


    [Fact]
    public void RuntimeMetadataServiceReadsManagedRuntimeMetadataAndCommits()
    {
        var root = CreateTempRoot();
        var runtimeRoot = Path.Combine(root, "runtime", "bin");
        Directory.CreateDirectory(runtimeRoot);
        var runtime = new RuntimeRecord(
            "runtime-1",
            "llama.cpp CUDA",
            RuntimeMode.Wsl,
            RuntimeBackend.Cuda,
            Path.Combine(runtimeRoot, "llama-server"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = Path.Combine(root, "runtime"),
                runtimeMetadata = new
                {
                    repoUrl = "https://github.com/ggml-org/llama.cpp",
                    commit = "abcdef1234567890",
                    assets = new[]
                    {
                        new { name = "llama-b9354-bin-win-cuda-13.1-x64.zip" },
                        new { name = "cudart-llama-bin-win-cuda-13.1-x64.zip" }
                    }
                }
            }),
            DateTimeOffset.UtcNow);
        var sourceDir = Path.Combine(root, "source");
        var refDir = Path.Combine(sourceDir, ".git", "refs", "heads");
        Directory.CreateDirectory(refDir);
        File.WriteAllText(Path.Combine(sourceDir, ".git", "HEAD"), "ref: refs/heads/main");
        File.WriteAllText(Path.Combine(refDir, "main"), "fedcba9876543210");

        Assert.Equal("official-cuda", RuntimeMetadataService.ManagedPresetId(runtime));
        Assert.Equal("official-vulkan", RuntimeMetadataService.ManagedPresetId(runtime with { Name = "llama.cpp Vulkan", Backend = RuntimeBackend.Vulkan }));
        Assert.Equal("official-sycl", RuntimeMetadataService.ManagedPresetId(runtime with { Name = "llama.cpp SYCL", Backend = RuntimeBackend.Sycl }));
        Assert.Equal("official-windows-cuda", RuntimeMetadataService.ManagedPresetId(runtime with { Mode = RuntimeMode.Native, ExecutablePath = Path.Combine(runtimeRoot, "llama-server.exe") }));
        Assert.Equal("official-windows-sycl", RuntimeMetadataService.ManagedPresetId(runtime with { Mode = RuntimeMode.Native, Backend = RuntimeBackend.Sycl, ExecutablePath = Path.Combine(runtimeRoot, "llama-server.exe") }));
        Assert.Equal(Path.Combine(root, "runtime"), RuntimeMetadataService.Folder(runtime));
        Assert.Equal("abcdef1234567890", RuntimeMetadataService.Commit(runtime));
        Assert.Equal("llama-b9354-bin-win-cuda-13.1-x64.zip, cudart-llama-bin-win-cuda-13.1-x64.zip", RuntimeMetadataService.PackageAssetSummary(runtime));
        Assert.True(RuntimeMetadataService.CommitsMatch("abcdef12", "abcdef1234567890"));
        Assert.Equal("abcdef123456", RuntimeMetadataService.ShortCommit("abcdef1234567890"));
        Assert.Equal("commit unavailable", RuntimeMetadataService.DisplayCommit(""));
        Assert.Equal("fedcba9876543210", RuntimeMetadataService.TryReadGitHeadCommit(sourceDir));
        Assert.Equal("123456789abcdef", RuntimeMetadataService.InferCommitFromText("build-123456789abcdef-path"));
    }


    [Fact]
    public void RuntimePackageCatalogServiceSelectsOfficialReleaseAssets()
    {
        var release = RuntimePackageCatalogService.ParseReleaseJson("""
        {
          "tag_name": "b9354",
          "html_url": "https://github.com/ggml-org/llama.cpp/releases/tag/b9354",
          "published_at": "2026-05-27T08:00:00Z",
          "assets": [
            { "name": "llama-b9354-bin-win-cuda-13.1-x64.zip", "browser_download_url": "https://example.com/cuda13.zip", "size": 13 },
            { "name": "cudart-llama-bin-win-cuda-13.1-x64.zip", "browser_download_url": "https://example.com/cudart13.zip", "size": 3 },
            { "name": "llama-b9354-bin-win-cuda-12.4-x64.zip", "browser_download_url": "https://example.com/cuda12.zip", "size": 12 },
            { "name": "cudart-llama-bin-win-cuda-12.4-x64.zip", "browser_download_url": "https://example.com/cudart12.zip", "size": 2 },
            { "name": "llama-b9354-bin-win-vulkan-x64.zip", "browser_download_url": "https://example.com/win-vulkan.zip", "size": 4 },
            { "name": "llama-b9354-bin-win-sycl-x64.zip", "browser_download_url": "https://example.com/win-sycl.zip", "size": 9 },
            { "name": "llama-b9354-bin-win-cpu-x64.zip", "browser_download_url": "https://example.com/win-cpu.zip", "size": 5 },
            { "name": "llama-b9354-bin-ubuntu-cuda-13.1-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-cuda13.tar.gz", "size": 11 },
            { "name": "llama-b9354-bin-ubuntu-cuda-12.4-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-cuda.tar.gz", "size": 8 },
            { "name": "llama-b9354-bin-ubuntu-vulkan-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-vulkan.tar.gz", "size": 6 },
            { "name": "llama-b9354-bin-ubuntu-sycl-f16-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-sycl.tar.gz", "size": 10 },
            { "name": "llama-b9354-bin-ubuntu-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-cpu.tar.gz", "size": 7 }
          ]
        }
        """);

        var presets = RuntimePackageCatalogService.PresetRows();
        var cuda = RuntimePackageCatalogService.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-windows-cuda"), release);
        var cudaCompatibility = RuntimePackageCatalogService.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-windows-cuda"), release, "compatibility");
        var cudaWsl = RuntimePackageCatalogService.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-cuda"), release);
        var cudaWslCompatibility = RuntimePackageCatalogService.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-cuda"), release, "compatibility");
        var vulkanWsl = RuntimePackageCatalogService.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-vulkan"), release);
        var sycl = RuntimePackageCatalogService.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-windows-sycl"), release);
        var syclWsl = RuntimePackageCatalogService.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-sycl"), release);

        Assert.Equal(
            ["official-prebuilt-windows-cuda", "official-prebuilt-cuda", "official-prebuilt-windows-vulkan", "official-prebuilt-vulkan", "official-prebuilt-windows-sycl", "official-prebuilt-sycl", "official-prebuilt-windows-cpu", "official-prebuilt-cpu"],
            presets.Select(preset => preset.Id).ToArray());
        Assert.Equal("b9354", release.TagName);
        Assert.Equal("llama-b9354-bin-win-cuda-13.1-x64.zip", cuda.PrimaryAsset.Name);
        Assert.Equal("cudart-llama-bin-win-cuda-13.1-x64.zip", Assert.Single(cuda.AdditionalAssets).Name);
        Assert.Contains("cudart-llama-bin-win-cuda-13.1-x64.zip", cuda.AssetSummary, StringComparison.Ordinal);
        Assert.True(RuntimePackageCatalogService.AssetSummariesMatch("cudart-llama-bin-win-cuda-13.1-x64.zip, llama-b9354-bin-win-cuda-13.1-x64.zip", cuda.AssetSummary));
        Assert.Equal("llama-b9354-bin-win-cuda-12.4-x64.zip", cudaCompatibility.PrimaryAsset.Name);
        Assert.Equal("cudart-llama-bin-win-cuda-12.4-x64.zip", Assert.Single(cudaCompatibility.AdditionalAssets).Name);
        Assert.Equal("llama-b9354-bin-ubuntu-cuda-13.1-x64.tar.gz", cudaWsl.PrimaryAsset.Name);
        Assert.Equal("llama-b9354-bin-ubuntu-cuda-12.4-x64.tar.gz", cudaWslCompatibility.PrimaryAsset.Name);
        Assert.Equal("CUDA WSL", RuntimePackageCatalogService.BackendLabel(cudaWsl.Preset));
        Assert.Equal("llama-b9354-bin-ubuntu-vulkan-x64.tar.gz", vulkanWsl.PrimaryAsset.Name);
        Assert.Equal("Vulkan WSL", RuntimePackageCatalogService.BackendLabel(vulkanWsl.Preset));
        Assert.Equal("llama-b9354-bin-win-sycl-x64.zip", sycl.PrimaryAsset.Name);
        Assert.Equal("SYCL Windows", RuntimePackageCatalogService.BackendLabel(sycl.Preset));
        Assert.Equal("llama-b9354-bin-ubuntu-sycl-f16-x64.tar.gz", syclWsl.PrimaryAsset.Name);
        Assert.Equal("SYCL WSL", RuntimePackageCatalogService.BackendLabel(syclWsl.Preset));
        Assert.EndsWith(Path.Combine("official-prebuilt-windows-cuda-b9354"), RuntimePackageCatalogService.InstallDir(Path.Combine("D:", "runtimes"), cuda), StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void RuntimePackageCatalogServiceReportsCudaWslUnavailableWhenReleaseOmitsAsset()
    {
        var release = RuntimePackageCatalogService.ParseReleaseJson("""
        {
          "tag_name": "b9357",
          "html_url": "https://github.com/ggml-org/llama.cpp/releases/tag/b9357",
          "target_commitish": "abcdef1234567890",
          "assets": [
            { "name": "llama-b9357-bin-ubuntu-vulkan-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-vulkan.tar.gz", "size": 6 }
          ]
        }
        """);
        var preset = RuntimePackageCatalogService.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-cuda");
        var unavailable = new RuntimePackageUpdateState(false, "", release.TagName, release.HtmlUrl, "not available", DateTimeOffset.UtcNow, TargetCommit: release.TargetCommit, IsAvailable: false);

        var ex = Assert.Throws<RuntimePackageAssetUnavailableException>(() => RuntimePackageCatalogService.SelectAssets(preset, release));

        Assert.Contains("CUDA WSL", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Not published", RuntimePackageCatalogService.LocalStatusLabel([], [], unavailable));
        Assert.False(RuntimePackageCatalogService.CanInstallPackage([], [], unavailable));
    }


    [Fact]
    public void RuntimePackageCatalogServiceExtractsAndFindsRuntimeExecutable()
    {
        var root = CreateTempRoot();
        var source = Path.Combine(root, "source");
        var nested = Path.Combine(source, "llama-b9354-bin-win-cpu-x64", "bin");
        var archive = Path.Combine(root, "runtime.zip");
        var destination = Path.Combine(root, "runtime");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "llama-server.exe"), "fake");
        System.IO.Compression.ZipFile.CreateFromDirectory(source, archive);

        RuntimePackageCatalogService.ExtractArchive(archive, destination);

        var executable = RuntimePackageCatalogService.FindRuntimeExecutable(destination, RuntimeMode.Native);
        Assert.Equal(Path.Combine(destination, "bin", "llama-server.exe"), executable);
        Assert.Equal(destination, RuntimePackageCatalogService.RuntimeFolderFromExecutable(executable));
    }


    [Fact]
    public void RuntimePackageCatalogServiceExtractsCompanionArchivesBesidePrimaryRuntime()
    {
        var root = CreateTempRoot();
        var primarySource = Path.Combine(root, "primary-source");
        var primaryNested = Path.Combine(primarySource, "llama-b9354-bin-win-cuda-x64", "bin");
        var companionSource = Path.Combine(root, "companion-source");
        var companionNested = Path.Combine(companionSource, "cudart-llama-bin-win-cuda-12.4-x64", "bin");
        var primaryArchive = Path.Combine(root, "primary.zip");
        var companionArchive = Path.Combine(root, "companion.zip");
        var destination = Path.Combine(root, "runtime");
        Directory.CreateDirectory(primaryNested);
        Directory.CreateDirectory(companionNested);
        File.WriteAllText(Path.Combine(primaryNested, "llama-server.exe"), "fake server");
        File.WriteAllText(Path.Combine(companionNested, "cudart64_12.dll"), "fake cudart");
        System.IO.Compression.ZipFile.CreateFromDirectory(primarySource, primaryArchive);
        System.IO.Compression.ZipFile.CreateFromDirectory(companionSource, companionArchive);

        RuntimePackageCatalogService.ExtractArchive(primaryArchive, destination);
        RuntimePackageCatalogService.ExtractArchive(companionArchive, destination);

        Assert.True(File.Exists(Path.Combine(destination, "bin", "llama-server.exe")));
        Assert.True(File.Exists(Path.Combine(destination, "bin", "cudart64_12.dll")));
        Assert.False(Directory.Exists(Path.Combine(destination, "cudart-llama-bin-win-cuda-12.4-x64")));
    }


    [Fact]
    public void RuntimePackageMetadataIdentifiesInstalledPrebuiltRuntime()
    {
        var root = CreateTempRoot();
        var runtimeFolder = Path.Combine(root, "official-prebuilt-windows-cuda-b9354");
        Directory.CreateDirectory(runtimeFolder);
        var preset = RuntimePackageCatalogService.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cuda");
        var runtime = new RuntimeRecord(
            "runtime-1",
            "Official llama.cpp CUDA Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(runtimeFolder, "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = runtimeFolder,
                runtimeMetadata = new
                {
                    managedPackageId = preset.Id,
                    managedPresetId = preset.Id,
                    releaseTag = "b9354"
                }
            }),
            DateTimeOffset.UtcNow);

        var installed = RuntimePackageCatalogService.InstalledPackages([runtime], preset);

        Assert.Equal(preset.Id, RuntimeMetadataService.ManagedPackageId(runtime));
        Assert.Equal(preset.Id, RuntimeMetadataService.ManagedPresetId(runtime));
        Assert.Equal("b9354", RuntimeMetadataService.PackageTag(runtime));
        Assert.Single(installed);
        Assert.Equal("b9354", RuntimePackageCatalogService.LatestInstalledTag(installed));
        Assert.False(RuntimePackageCatalogService.CanInstallPackage(installed, null));
        Assert.True(RuntimePackageCatalogService.CanInstallPackage(installed, new RuntimePackageUpdateState(true, "b9354", "b9355", "", "", DateTimeOffset.UtcNow)));
    }


    [Fact]
    public async Task RuntimeEquivalenceServiceLinksSourceBuildAndPrebuiltByFingerprint()
    {
        var root = CreateTempRoot();
        var packageFolder = Path.Combine(root, "runtimes", "official-prebuilt-windows-cuda-b9354");
        var sourceFolder = Path.Combine(root, "runtimes", "official-windows-cuda-20260527");
        Directory.CreateDirectory(Path.Combine(packageFolder, "bin"));
        Directory.CreateDirectory(Path.Combine(sourceFolder, "bin"));
        await File.WriteAllTextAsync(Path.Combine(packageFolder, "bin", "llama-server.exe"), "same binary", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sourceFolder, "bin", "llama-server.exe"), "same binary", TestContext.Current.CancellationToken);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var now = DateTimeOffset.UtcNow;
        var packageRuntime = new RuntimeRecord(
            "package-runtime",
            "Official llama.cpp CUDA Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(packageFolder, "bin", "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = packageFolder,
                runtimeMetadata = new
                {
                    managedPackageId = "official-prebuilt-windows-cuda",
                    managedPresetId = "official-prebuilt-windows-cuda",
                    releaseTag = "b9354"
                }
            }),
            now);
        var sourceRuntime = new RuntimeRecord(
            "source-runtime",
            "Official llama.cpp CUDA Windows Source",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(sourceFolder, "bin", "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = sourceFolder,
                runtimeMetadata = new
                {
                    managedPresetId = "official-windows-cuda",
                    commit = "9777256c3130"
                }
            }),
            now);
        await store.UpsertRuntimeAsync(packageRuntime);
        await store.UpsertRuntimeAsync(sourceRuntime);

        Assert.True(await RuntimeEquivalenceService.ReconcileOfficialRuntimeEquivalenceAsync(store, await store.ListRuntimesAsync(), TestContext.Current.CancellationToken));
        var runtimes = await store.ListRuntimesAsync();
        var reconciledSource = runtimes.Single(runtime => runtime.Id == sourceRuntime.Id);
        var reconciledPackage = runtimes.Single(runtime => runtime.Id == packageRuntime.Id);
        var preset = RuntimePackageCatalogService.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cuda");

        Assert.Equal(RuntimeMetadataService.RuntimeFingerprint(reconciledPackage), RuntimeMetadataService.RuntimeFingerprint(reconciledSource));
        Assert.Contains(preset.Id, RuntimeMetadataService.EquivalentPackageIds(reconciledSource));
        Assert.Contains(preset.SourcePresetId, RuntimeMetadataService.EquivalentSourcePresetIds(reconciledPackage));
        Assert.Contains(reconciledSource, RuntimePackageCatalogService.InstalledPackages(runtimes, preset));
    }


    [Fact]
    public void RuntimePackageCatalogServiceReportsSourceBuildCandidates()
    {
        var root = CreateTempRoot();
        var runtime = new RuntimeRecord(
            "source-runtime",
            "Official llama.cpp CUDA Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(root, "runtime", "bin", "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = Path.Combine(root, "runtime"),
                runtimeMetadata = new
                {
                    managedPresetId = "official-windows-cuda",
                    commit = "9777256c3130"
                }
            }),
            DateTimeOffset.UtcNow);
        var preset = RuntimePackageCatalogService.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cuda");

        var sourceBuilds = RuntimePackageCatalogService.MatchingSourceBuilds([runtime], preset);

        Assert.Single(sourceBuilds);
        Assert.Equal("Built from source", RuntimePackageCatalogService.LocalStatusLabel([], sourceBuilds));
        Assert.Equal("source:9777256c3130", RuntimePackageCatalogService.LocalIdentity([], sourceBuilds));
        Assert.Contains("source built", RuntimePackageCatalogService.LatestLocalLabel([], sourceBuilds, null), StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task RuntimeBuildCatalogServicePersistsCustomPresetsAndReadsSources()
    {
        var root = CreateTempRoot();
        var runtimeRoot = Path.Combine(root, "runtimes");
        var custom = new RuntimeBuildPreset("", "My Runtime", "https://example.com/runtime.git", "main", true, Custom: true);

        await RuntimeBuildCatalogService.SaveCustomPresetsAsync(runtimeRoot, [custom], TestContext.Current.CancellationToken);
        var loaded = Assert.Single(RuntimeBuildCatalogService.ReadCustomPresets(runtimeRoot));
        var sourceDir = RuntimeBuildCatalogService.SourceDir(runtimeRoot, loaded);
        Directory.CreateDirectory(Path.Combine(sourceDir, ".git"));
        File.WriteAllText(Path.Combine(sourceDir, ".git", "HEAD"), "abc123def4567890");
        var source = new RuntimeSourceEntry(loaded.Id, loaded.Label, loaded.RepoUrl, loaded.Branch, loaded.Cuda, sourceDir, "unknown", DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(
            RuntimeBuildCatalogService.SourceMetadataPath(sourceDir),
            System.Text.Json.JsonSerializer.Serialize(source),
            TestContext.Current.CancellationToken);

        var sources = RuntimeBuildCatalogService.Sources(runtimeRoot).ToList();
        var rows = RuntimeBuildCatalogService.PresetRows(runtimeRoot);

        Assert.True(loaded.Custom);
        Assert.Equal(RuntimeMode.Wsl, RuntimeBuildCatalogService.BuildMode(loaded));
        Assert.StartsWith("custom-my-runtime-cuda-", loaded.Id, StringComparison.Ordinal);
        Assert.Equal(
            ["official-windows-cuda", "official-cuda", "official-windows-vulkan", "official-vulkan", "official-windows-sycl", "official-sycl"],
            rows.Take(6).Select(preset => preset.Id).ToArray());
        Assert.Contains(rows, preset => preset.Id == "official-cuda");
        Assert.Contains(rows, preset => preset.Id == "official-vulkan" && RuntimeBuildCatalogService.BuildBackend(preset) == RuntimeBackend.Vulkan);
        Assert.Contains(rows, preset => preset.Id == "official-sycl" && RuntimeBuildCatalogService.BuildBackend(preset) == RuntimeBackend.Sycl);
        Assert.Contains(rows, preset => preset.Id == "official-windows-cpu" && RuntimeBuildCatalogService.BuildMode(preset) == RuntimeMode.Native);
        Assert.Contains(rows, preset => preset.Id == "official-windows-cuda" && RuntimeBuildCatalogService.BuildMode(preset) == RuntimeMode.Native);
        Assert.Contains(rows, preset => preset.Id == "official-windows-vulkan" && RuntimeBuildCatalogService.BuildBackend(preset) == RuntimeBackend.Vulkan && RuntimeBuildCatalogService.BuildMode(preset) == RuntimeMode.Native);
        Assert.Contains(rows, preset => preset.Id == "official-windows-sycl" && RuntimeBuildCatalogService.BuildBackend(preset) == RuntimeBackend.Sycl && RuntimeBuildCatalogService.BuildMode(preset) == RuntimeMode.Native);
        Assert.Equal("Vulkan WSL", RuntimeBuildCatalogService.BackendLabel(rows.Single(preset => preset.Id == "official-vulkan")));
        Assert.Equal("Vulkan Windows", RuntimeBuildCatalogService.BackendLabel(rows.Single(preset => preset.Id == "official-windows-vulkan")));
        Assert.Equal("SYCL WSL", RuntimeBuildCatalogService.BackendLabel(rows.Single(preset => preset.Id == "official-sycl")));
        Assert.Equal("SYCL Windows", RuntimeBuildCatalogService.BackendLabel(rows.Single(preset => preset.Id == "official-windows-sycl")));
        Assert.Contains(rows, preset => preset.Id == loaded.Id);
        Assert.Equal("abc123def4567890", RuntimeBuildCatalogService.SourceCommit(Assert.Single(sources)));
        Assert.StartsWith("custom-my-runtime-windows-cuda-", RuntimeBuildCatalogService.CustomPresetId("My Runtime", "https://example.com/runtime.git", "main", "cuda", RuntimeMode.Native), StringComparison.Ordinal);
        Assert.True(RuntimeBuildCatalogService.IsAllowedGitSource("https://example.com/repo.git"));
        Assert.True(RuntimeBuildCatalogService.IsAllowedGitSource("ssh://git@example.com/repo.git"));
        Assert.True(RuntimeBuildCatalogService.IsAllowedGitSource(Path.GetTempPath()));
        Assert.False(RuntimeBuildCatalogService.IsAllowedGitSource("http://example.com/repo.git"));
        Assert.True(RuntimeBuildCatalogService.IsHttpsGitSource("https://example.com/repo.git"));
        Assert.False(RuntimeBuildCatalogService.IsHttpsGitSource("https://user:token@example.com/repo.git"));
        Assert.False(RuntimeBuildCatalogService.IsHttpsGitSource("ssh://git@example.com/repo.git"));
        Assert.False(RuntimeBuildCatalogService.IsHttpsGitSource(Path.GetTempPath()));
        Assert.True(RuntimeBuildCatalogService.IsSafeUiCustomPreset(custom));
        Assert.False(RuntimeBuildCatalogService.IsSafeUiCustomPreset(custom with { RepoUrl = "ssh://git@example.com/repo.git" }));
        Assert.True(RuntimeBuildCatalogService.IsSafeGitRefName("feature/runtime-build"));
        Assert.False(RuntimeBuildCatalogService.IsSafeGitRefName("bad branch"));
        Assert.Equal(["refs/heads/main", "main"], RuntimeBuildCatalogService.RemoteRefs(loaded));
        Assert.Equal("abcdef123", RuntimeBuildCatalogService.FirstLsRemoteCommit("abcdef123\trefs/heads/main\n"));
        Assert.StartsWith("custom-my-runtime-windows-sycl-", RuntimeBuildCatalogService.CustomPresetId("My Runtime", "https://example.com/runtime.git", "main", "sycl", RuntimeMode.Native), StringComparison.Ordinal);

        var legacySourcePath = Path.Combine(runtimeRoot, "runtime-sources", "legacy", "local-llm-runtime-source.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacySourcePath)!);
        await File.WriteAllTextAsync(
            legacySourcePath,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                PresetId = "legacy",
                Label = "Legacy",
                RepoUrl = "https://example.com/legacy.git",
                Branch = "main",
                Cuda = false,
                SourceDir = Path.GetDirectoryName(legacySourcePath),
                Commit = "abc",
                DownloadedAt = DateTimeOffset.UtcNow,
                Backend = "cpu"
            }),
            TestContext.Current.CancellationToken);
        var legacySource = RuntimeBuildCatalogService.Sources(runtimeRoot).Single(source => source.PresetId == "legacy");
        Assert.Equal(RuntimeMode.Wsl, RuntimeBuildCatalogService.BuildMode(legacySource));
    }


    [Fact]
    public void RuntimeFileServiceRestrictsRuntimeDeletionToSafeFolders()
    {
        var root = CreateTempRoot();
        var runtimeRoot = Path.Combine(root, "runtimes");
        var managed = Path.Combine(runtimeRoot, "managed-runtime");
        var external = Path.Combine(root, "external-runtime");
        var packaged = Path.Combine(root, "packaged-runtime");
        Directory.CreateDirectory(Path.Combine(managed, "bin"));
        Directory.CreateDirectory(packaged);
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(managed, "bin", "llama-server.exe"), "");
        File.WriteAllText(Path.Combine(packaged, "llama-server.exe"), "");
        File.WriteAllText(Path.Combine(packaged, "local-llm-runtime.json"), """{"managedPresetId":"official-cpu"}""");
        var now = DateTimeOffset.UtcNow;
        var managedRuntime = new RuntimeRecord("managed", "Managed", RuntimeMode.Native, RuntimeBackend.Cpu, Path.Combine(managed, "bin", "llama-server.exe"), "{}", now);
        var externalRuntime = new RuntimeRecord("external", "External", RuntimeMode.Native, RuntimeBackend.Cpu, Path.Combine(external, "llama-server.exe"), "{}", now);

        Assert.True(RuntimeFileService.CanDeleteRuntimeFiles(managedRuntime, runtimeRoot, out var managedFolder, out _));
        Assert.Equal(managed, managedFolder);
        Assert.False(RuntimeFileService.CanDeleteRuntimeFiles(externalRuntime, runtimeRoot, out _, out var reason));
        Assert.Contains("outside the app runtimes folder", reason, StringComparison.Ordinal);
        Assert.True(RuntimeFileService.IsPackagedRuntimeFolderSafeToDelete(packaged));

        RuntimeFileService.DeleteRuntimeFiles(runtimeRoot, managed);

        Assert.False(Directory.Exists(managed));
        Assert.Throws<InvalidOperationException>(() => RuntimeFileService.DeleteRuntimeFiles(runtimeRoot, external));
    }


    [Fact]
    public async Task RuntimeBuildJobServiceBuildsPayloadRedactsUrlsAndStampsMetadata()
    {
        var root = CreateTempRoot();
        var installDir = Path.Combine(root, "runtime");
        Directory.CreateDirectory(installDir);
        await File.WriteAllTextAsync(Path.Combine(installDir, "local-llm-runtime.json"), """{"commit":"abc"}""", TestContext.Current.CancellationToken);
        var preset = new RuntimeBuildPreset("custom-cuda", "Custom CUDA", "https://fixture-user:fixture-pass@example.invalid/repo.git", "main", true, Custom: true);

        var sourceDir = Path.Combine(root, "source");
        var payload = System.Text.Json.Nodes.JsonNode.Parse(RuntimeBuildJobService.Payload(preset, "build", installDir, "Queued.", "marker", "Ubuntu", sourceDir))!.AsObject();
        await RuntimeBuildJobService.StampManagedMetadataAsync(installDir, preset, update: true);
        var logPath = Path.Combine(root, "runtime-build.log");
        await RuntimeBuildJobService.AppendJobLogAsync(logPath, JobStatus.Running, "build started", BoundedLogFile.MegabytesToBytes(1));
        await RuntimeBuildJobService.AppendRecoveryLogAsync(logPath, "recovered source", BoundedLogFile.MegabytesToBytes(1));
        var metadata = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(installDir, "local-llm-runtime.json"), TestContext.Current.CancellationToken))!.AsObject();
        var log = await File.ReadAllTextAsync(logPath, TestContext.Current.CancellationToken);

        Assert.Equal("custom-cuda", payload["preset"]?.ToString());
        Assert.Equal("Custom CUDA", payload["label"]?.ToString());
        Assert.Equal(preset.RepoUrl, payload["repoUrl"]?.ToString());
        Assert.Equal("build", payload["action"]?.ToString());
        Assert.Equal("Ubuntu", payload["wslDistro"]?.ToString());
        Assert.Equal("marker", payload["processMarker"]?.ToString());
        Assert.Equal(sourceDir, payload["sourceDir"]?.ToString());
        Assert.Equal("wsl", payload["mode"]?.ToString());
        Assert.Equal("https://redacted:redacted@example.invalid/repo.git", RuntimeBuildJobService.RedactCommandArgument(preset.RepoUrl));
        Assert.Equal("abc", metadata["commit"]?.ToString());
        Assert.Equal("custom-cuda", metadata["managedPresetId"]?.ToString());
        Assert.Equal("wsl", metadata["managedMode"]?.ToString());
        Assert.Equal("update", metadata["managedAction"]?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(metadata["managedInstalledAt"]?.ToString()));
        Assert.Contains("Running: build started", log, StringComparison.Ordinal);
        Assert.Contains("Recovery: recovered source", log, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimeBuildJobServiceCreatesDeterministicBuildPlan()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var preset = new RuntimeBuildPreset("official-cpu", "Official CPU", "https://example.com/llama.cpp.git", "master", false);
        var source = new RuntimeSourceEntry(preset.Id, preset.Label, preset.RepoUrl, preset.Branch, preset.Cuda, Path.Combine(root, "source"), "abcdef1234567890", DateTimeOffset.UtcNow);

        var plan = RuntimeBuildJobService.CreatePlan(preset, update: false, source, settings, new DateTimeOffset(2026, 5, 26, 12, 34, 56, TimeSpan.Zero), "marker");

        Assert.Equal("build", plan.Action);
        Assert.Equal(source.SourceDir, plan.SourceDir);
        Assert.Equal(Path.Combine(settings.CacheRoot, "runtime-builds", "official-cpu-20260526-123456"), plan.BuildDir);
        Assert.Equal(Path.Combine(settings.RuntimeRoot, "official-cpu-20260526-123456"), plan.InstallDir);
        Assert.Equal("marker", plan.ProcessMarker);
        Assert.Contains("abcdef1", plan.QueuedMessage, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimeBuildJobServiceParsesPayloadAndExposesJobControls()
    {
        var root = CreateTempRoot();
        var preset = new RuntimeBuildPreset("official-cuda", "Official CUDA", "https://example.com/llama.cpp.git", "master", true);
        var sourceDir = Path.Combine(root, "runtime-source");
        var payloadJson = RuntimeBuildJobService.Payload(preset, "build", Path.Combine(root, "runtime"), "Building", "marker", "Ubuntu-24.04", sourceDir);
        var now = DateTimeOffset.UtcNow;
        var running = new JobRecord("job-1", "runtime-build", JobStatus.Running, payloadJson, Path.Combine(root, "logs", "job-1.log"), now, now);
        var failed = running with { Id = "job-2", Status = JobStatus.Failed };
        var completed = running with { Id = "job-3", Status = JobStatus.Completed };
        var completedDownload = running with { Id = "job-4", Kind = "runtime-source-download", Status = JobStatus.Completed };
        Directory.CreateDirectory(Path.GetDirectoryName(running.LogPath)!);
        File.WriteAllText(running.LogPath, "[2026-05-26T12:00:00Z] Running: Building\n[ 42%] Building CXX object llama.cpp\n");

        var payload = RuntimeBuildJobService.ParsePayload(payloadJson);
        var vm = new JobsViewModel();
        vm.ReplaceJobs([running, failed, completed]);

        Assert.NotNull(payload);
        Assert.Equal("official-cuda", payload.Preset.Id);
        Assert.Equal("Official CUDA", payload.Preset.Label);
        Assert.True(payload.Preset.Cuda);
        Assert.Equal("build", payload.Action);
        Assert.Equal("marker", payload.ProcessMarker);
        Assert.Equal("Ubuntu-24.04", payload.WslDistro);
        Assert.Equal(sourceDir, payload.SourceDir);
        Assert.Equal(RuntimeMode.Wsl, payload.Mode);
        Assert.True(RuntimeBuildJobService.CanCancel(running));
        Assert.False(RuntimeBuildJobService.CanRetry(running));
        Assert.False(RuntimeBuildJobService.CanClear(running));
        Assert.Contains("Building CXX object", vm.RuntimeRows[0].C5, StringComparison.Ordinal);
        Assert.False(RuntimeBuildJobService.CanCancel(failed));
        Assert.True(RuntimeBuildJobService.CanRetry(failed));
        Assert.True(RuntimeBuildJobService.CanClear(failed));
        Assert.False(RuntimeBuildJobService.CanCancel(completed));
        Assert.False(RuntimeBuildJobService.CanRetry(completed));
        Assert.True(RuntimeBuildJobService.CanClear(completed));
        Assert.False(RuntimeBuildJobService.CanCancel(completedDownload));
        Assert.False(RuntimeBuildJobService.CanRetry(completedDownload));
        Assert.True(RuntimeBuildJobService.CanClear(completedDownload));
        Assert.Equal(3, vm.RuntimeRows.Count);
        Assert.True(vm.RuntimeRows[0].B2);
        Assert.False(vm.RuntimeRows[0].B3);
        Assert.False(vm.RuntimeRows[0].B4);
        Assert.False(vm.RuntimeRows[1].B2);
        Assert.True(vm.RuntimeRows[1].B3);
        Assert.True(vm.RuntimeRows[1].B4);
        Assert.Equal("Clear", vm.RuntimeRows[2].C9);
        Assert.True(vm.RuntimeRows[2].B4);
    }


    [Fact]
    public void RuntimeBuildRetryPreservesDownloadedSourceContext()
    {
        var source = ReadMainWindowSources();

        Assert.Contains("RuntimeSourceFromBuildPayload(payload)", source, StringComparison.Ordinal);
        Assert.Contains("payload.SourceDir", source, StringComparison.Ordinal);
        Assert.Contains("var payloadSourceDir = source?.SourceDir ?? \"\";", source, StringComparison.Ordinal);
        Assert.Contains("RuntimeBuildJobService.Payload(preset, plan.Action, plan.InstallDir, plan.QueuedMessage, plan.ProcessMarker, _settings.WslDistro, payloadSourceDir)", source, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimeBuildToolServiceBuildsHiddenPowerShellCommand()
    {
        var preset = new RuntimeBuildPreset("custom-cuda", "Custom CUDA", "https://example.com/repo.git", "feature/runtime", true, Custom: true);

        var psi = RuntimeBuildToolService.CreateBuildProcessStartInfo(
            "powershell.exe",
            @"D:\tools\Build-LlamaCppRuntime.ps1",
            @"D:\cache\source",
            @"D:\cache\build",
            @"D:\runtimes\install",
            preset,
            RuntimeMode.Wsl,
            "Ubuntu-24.04",
            "marker-1",
            @"C:\Windows\System32\wsl.exe",
            "",
            "",
            noUpdate: true);
        var args = psi.ArgumentList.ToArray();

        Assert.Equal("powershell.exe", psi.FileName);
        Assert.False(psi.UseShellExecute);
        Assert.True(psi.RedirectStandardOutput);
        Assert.Contains("-RepoUrl", args);
        Assert.Contains("https://example.com/repo.git", args);
        Assert.Contains("-Branch", args);
        Assert.Contains("feature/runtime", args);
        Assert.Contains("-WslDistro", args);
        Assert.Contains("Ubuntu-24.04", args);
        Assert.Contains("-ProcessMarker", args);
        Assert.Contains("marker-1", args);
        Assert.Contains("-Cuda", args);
        Assert.Contains("-NoUpdate", args);
        Assert.Contains("-Clean", args);

        var vulkanPreset = new RuntimeBuildPreset("official-vulkan", "Official Vulkan", "https://example.com/repo.git", "master", false, Backend: "vulkan");
        var vulkanPsi = RuntimeBuildToolService.CreateBuildProcessStartInfo(
            "powershell.exe",
            @"D:\tools\Build-LlamaCppRuntime.ps1",
            @"D:\cache\source",
            @"D:\cache\build",
            @"D:\runtimes\install",
            vulkanPreset,
            RuntimeMode.Wsl,
            "Ubuntu-24.04",
            "marker-2",
            @"C:\Windows\System32\wsl.exe",
            "",
            "",
            noUpdate: false);
        var vulkanArgs = vulkanPsi.ArgumentList.ToArray();

        Assert.Contains("-Vulkan", vulkanArgs);
        Assert.DoesNotContain("-Cuda", vulkanArgs);

        var syclPreset = new RuntimeBuildPreset("official-sycl", "Official SYCL", "https://example.com/repo.git", "master", false, Backend: "sycl");
        var syclPsi = RuntimeBuildToolService.CreateBuildProcessStartInfo(
            "powershell.exe",
            @"D:\tools\Build-LlamaCppRuntime.ps1",
            @"D:\cache\source",
            @"D:\cache\build",
            @"D:\runtimes\install",
            syclPreset,
            RuntimeMode.Wsl,
            "Ubuntu-24.04",
            "marker-3",
            @"C:\Windows\System32\wsl.exe",
            "",
            "",
            noUpdate: false);
        var syclArgs = syclPsi.ArgumentList.ToArray();

        Assert.Contains("-Sycl", syclArgs);
        Assert.DoesNotContain("-Cuda", syclArgs);
        Assert.DoesNotContain("-Vulkan", syclArgs);

        var nativePreset = new RuntimeBuildPreset("official-windows-cpu", "Official CPU Windows", "https://example.com/repo.git", "master", false, Mode: RuntimeMode.Native);
        var nativePsi = RuntimeBuildToolService.CreateBuildProcessStartInfo(
            "powershell.exe",
            @"D:\tools\Build-LlamaCppRuntime.ps1",
            @"D:\cache\source",
            @"D:\cache\build",
            @"D:\runtimes\install",
            nativePreset,
            RuntimeMode.Native,
            "",
            "",
            "",
            @"C:\Program Files\Git\cmd\git.exe",
            @"C:\Program Files\CMake\bin\cmake.exe",
            noUpdate: false);
        var nativeArgs = nativePsi.ArgumentList.ToArray();

        Assert.Contains("-Runtime", nativeArgs);
        Assert.Contains("native", nativeArgs);
        Assert.Contains("-GitExe", nativeArgs);
        Assert.Contains(@"C:\Program Files\Git\cmd\git.exe", nativeArgs);
        Assert.Contains("-CMakeExe", nativeArgs);
        Assert.DoesNotContain("-WslDistro", nativeArgs);
        Assert.DoesNotContain("-WslExe", nativeArgs);
    }


    [Fact]
    public async Task TrackedProcessRunnerCapturesOutputErrorAndStandardInput()
    {
        var runner = new TrackedProcessRunner();
        var psi = new System.Diagnostics.ProcessStartInfo(HostExecutableResolver.WindowsPowerShellExe());
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("$text = [Console]::In.ReadToEnd(); Write-Output $text.Trim(); [Console]::Error.WriteLine('runner-error')");

        var result = await runner.RunAsync(
            psi,
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken,
            "runner-output");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("runner-output", result.Output, StringComparison.Ordinal);
        Assert.Contains("runner-error", result.Error, StringComparison.Ordinal);
    }


    [Fact]
    public async Task RuntimePortAllocatorSkipsReservedAndOccupiedPorts()
    {
        var allocator = new RuntimePortAllocator();
        var occupied = new HashSet<int> { 8082 };

        var port = await allocator.AllocateAsync(
            8081,
            [8081],
            candidate => Task.FromResult(occupied.Contains(candidate)));

        Assert.Equal(8083, port);
    }


    [Fact]
    public void ModelPortAllocatorUsesLowestFreePortAndReusesGaps()
    {
        Assert.Equal(8081, ModelPortAllocator.NextAvailable(8081, []));
        Assert.Equal(8082, ModelPortAllocator.NextAvailable(8081, [8081]));
        Assert.Equal(8082, ModelPortAllocator.NextAvailable(8081, [8081, 8083]));
        Assert.Equal(8081, ModelPortAllocator.NextAvailable(8081, [8082, 8083]));
    }


    [Fact]
    public void ModelRuntimeUsesFixedModelPortsForStableOpenCodeEndpoints()
    {
        var source = ReadMainWindowSources();
        var loadSelectedModel = source.IndexOf("private async Task LoadSelectedModelAsync", StringComparison.Ordinal);
        var renderSelectedProfile = source.IndexOf("await RenderSelectedModelLaunchSettingsAsync();", loadSelectedModel, StringComparison.Ordinal);
        var resolveRuntime = source.IndexOf("var runtime = ResolveLaunchRuntime(runtimes);", loadSelectedModel, StringComparison.Ordinal);

        Assert.Contains("Set a unique model port next to the runtime before launching.", source, StringComparison.Ordinal);
        Assert.Contains("_sessions.ReservedPorts(sessionId).Contains(launchSettings.Port)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("launchSettings = launchSettings with { Port = allocatedPort }", source, StringComparison.Ordinal);
        Assert.True(loadSelectedModel >= 0);
        Assert.True(renderSelectedProfile > loadSelectedModel);
        Assert.True(resolveRuntime > renderSelectedProfile);
    }


    [Fact]
    public void ModelLaunchProfilePersistenceIsExplicit()
    {
        var source = ReadMainWindowSources();
        var runtimeSelection = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.LaunchSettingsRuntimeSelection.cs"));
        var renderStart = source.IndexOf("private async Task RenderSelectedModelLaunchSettingsAsync", StringComparison.Ordinal);
        var saveDefaultsStart = source.IndexOf("private async Task SaveLaunchDefaultsFromControlsAsync", StringComparison.Ordinal);
        var saveForModelStart = source.IndexOf("private async Task SaveLaunchSettingsForSelectedModelAsync", StringComparison.Ordinal);
        var loadStart = source.IndexOf("private async Task LoadSelectedModelAsync", StringComparison.Ordinal);
        var loadEnd = source.IndexOf("private async Task UnloadSelectedModelAsync", loadStart, StringComparison.Ordinal);

        Assert.True(renderStart >= 0);
        Assert.True(saveDefaultsStart > renderStart);
        Assert.Contains("var profile = _stateStore is null ? null : await ReadModelLaunchProfileAsync(model);", source, StringComparison.Ordinal);
        Assert.Contains("var draft = await DraftModelLaunchProfileAsync(model);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureModelLaunchProfileAsync(model)", source[renderStart..saveDefaultsStart], StringComparison.Ordinal);

        Assert.Contains("_settings = launchDefaults with { Port = _settings.Port };", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_settings = ReadLaunchSettingsFromControls();", source, StringComparison.Ordinal);

        Assert.True(loadStart >= 0);
        Assert.True(loadEnd > loadStart);
        Assert.DoesNotContain("SaveLaunchSettingsForModelAsync(model, launchSettings)", source[loadStart..loadEnd], StringComparison.Ordinal);
        Assert.Contains("await SaveLaunchSettingsForModelAsync(model, launchSettings);", source[saveForModelStart..], StringComparison.Ordinal);

        Assert.Contains("Missing runtime ({selectedRuntimeId})", runtimeSelection, StringComparison.Ordinal);
        Assert.Contains("return runtimes.FirstOrDefault(runtime => string.Equals(runtime.Id, selectedRuntimeId", runtimeSelection, StringComparison.Ordinal);
        Assert.Contains("Saved runtime '{runtimeId}' is missing.", runtimeSelection, StringComparison.Ordinal);
    }


    [Fact]
    public async Task ActiveRuntimeSessionStorePersistsMultipleSelectedSessions()
    {
        var root = CreateTempRoot();
        var store = new ActiveRuntimeSessionStore(root);
        var settings = AppSettings.CreateDefault(root);
        var first = new ActiveRuntimeSession("model-a", "runtime-a", settings with { Port = 8081 }, "a.log", DateTimeOffset.UtcNow, ProcessId: 11, SessionId: "session-a", IsSelected: false);
        var second = new ActiveRuntimeSession("model-b", "runtime-b", settings with { Port = 8082 }, "b.log", DateTimeOffset.UtcNow, ProcessId: 22, SessionId: "session-b", IsSelected: true);

        await store.SaveAllAsync([first, second], TestContext.Current.CancellationToken);
        var all = await store.ReadAllAsync(TestContext.Current.CancellationToken);
        var selected = await store.TryReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);
        Assert.Equal("session-b", selected?.SessionId);
        Assert.Equal(8082, selected?.LaunchSettings.Port);
    }


    [Fact]
    public void LoadedModelSessionManagerTracksMultipleExposedEndpoints()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var runtime = new RuntimeRecord("runtime", "llama.cpp CUDA", RuntimeMode.Native, RuntimeBackend.Cuda, Path.Combine(root, "llama-server.exe"), "{}", DateTimeOffset.UtcNow);
        var modelA = new ModelRecord("model-a", "Model A", Path.Combine(root, "a.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var modelB = new ModelRecord("model-b", "Model B", Path.Combine(root, "b.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        using var manager = new LoadedModelSessionManager();

        manager.AttachExisting(runtime, modelA, settings with { Port = 8081 }, "a.log", LlamaRuntimeState.Loaded, "", "session-a", DateTimeOffset.UtcNow);
        manager.AttachExisting(runtime, modelB, settings with { Port = 8082 }, "b.log", LlamaRuntimeState.Loaded, "", "session-b", DateTimeOffset.UtcNow);
        manager.SelectModel(modelB.Id);
        var sessions = manager.Snapshots();

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.ModelId == modelA.Id && session.Endpoint.EndsWith(":8081/v1", StringComparison.Ordinal));
        Assert.Contains(sessions, session => session.ModelId == modelB.Id && session.Endpoint.EndsWith(":8082/v1", StringComparison.Ordinal) && session.IsSelected);
        Assert.True(manager.IsModelLoaded(modelA.Id));
        Assert.True(manager.IsModelActive(modelB.Id));
    }


    [Fact]
    public async Task LoadedModelSessionManagerRemovesUnavailableRecoveredLoadedSessions()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var runtime = new RuntimeRecord("runtime", "llama.cpp CUDA", RuntimeMode.Native, RuntimeBackend.Cuda, Path.Combine(root, "llama-server.exe"), "{}", DateTimeOffset.UtcNow);
        var model = new ModelRecord("model-a", "Model A", Path.Combine(root, "a.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        using var manager = new LoadedModelSessionManager();

        manager.AttachExisting(runtime, model, settings with { Port = 8081 }, "a.log", LlamaRuntimeState.Loaded, "", "session-a", DateTimeOffset.UtcNow);

        var removed = await manager.StopUnavailableRecoveredSessionsAsync(_ => Task.FromResult(false));

        Assert.Equal(1, removed);
        Assert.False(manager.HasRunningSessions);
        Assert.Empty(manager.Snapshots());
    }


    [Fact]
    public async Task LoadedModelSessionManagerRemovesUnavailableRecoveredLoadingSessions()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var runtime = new RuntimeRecord("runtime", "llama.cpp WSL", RuntimeMode.Wsl, RuntimeBackend.Cuda, Path.Combine(root, "llama-server"), "{}", DateTimeOffset.UtcNow);
        var model = new ModelRecord("model-a", "Model A", Path.Combine(root, "a.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        using var manager = new LoadedModelSessionManager();

        manager.AttachExisting(runtime, model, settings with { Port = 8081 }, "a.log", LlamaRuntimeState.Loading, "marker", "session-a", DateTimeOffset.UtcNow);

        var removed = await manager.StopUnavailableRecoveredSessionsAsync(_ => Task.FromResult(false));

        Assert.Equal(1, removed);
        Assert.False(manager.HasRunningSessions);
        Assert.Empty(manager.Snapshots());
    }


    [Fact]
    public void VramAdmissionWarnsOrBlocksConservatively()
    {
        var root = CreateTempRoot();
        var modelPath = Path.Combine(root, "model.gguf");
        File.WriteAllBytes(modelPath, new byte[1024 * 1024]);
        var model = new ModelRecord("model", "Model", modelPath, OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var runtime = new RuntimeRecord("runtime", "CUDA", RuntimeMode.Native, RuntimeBackend.Cuda, "llama-server.exe", "{}", DateTimeOffset.UtcNow);
        var syclRuntime = runtime with { Name = "SYCL", Backend = RuntimeBackend.Sycl };
        var settings = AppSettings.CreateDefault(root) with { ContextSize = 131072, GpuLayers = AppSettings.DefaultGpuLayers };
        var service = new VramAdmissionService();

        Assert.Equal(VramAdmissionDecision.Warn, service.Assess(model, runtime, settings, null).Decision);
        Assert.Equal(VramAdmissionDecision.Warn, service.Assess(model, syclRuntime, settings, null).Decision);
        Assert.Equal(VramAdmissionDecision.Block, service.Assess(model, runtime, settings, new VramMemorySnapshot(0.1, 24)).Decision);
        Assert.Equal(VramAdmissionDecision.Allow, service.Assess(model, runtime, settings, new VramMemorySnapshot(8, 24)).Decision);
    }

}
