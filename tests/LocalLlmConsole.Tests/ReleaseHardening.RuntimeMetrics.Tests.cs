using System.Diagnostics;
using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
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
            "n_ctx": 4096,
            "n_draft_tokens": 9,
            "n_draft_tokens_accepted": 6
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
        Assert.Equal(9, snapshot.MtpGeneratedTokens);
        Assert.Equal(6, snapshot.MtpAcceptedTokens);
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
        Assert.Equal("2.0 t/s (Gen) | 3.0 t/s (Avg) | 13 t (Total)\nUnknown (Prompt) | 15 t (Total)", RuntimeDashboardService.TokenActivitySummaryLabel(2, 3, null, null, 13, 15));
        Assert.Equal("5.0 t/s (Gen) | 20 t (Total)\n0.0 t/s (Prompt) | 10 t (Total)", RuntimeDashboardService.TokenActivitySummaryLabel(5, 0, 0, 0, 20, 10));
        Assert.Equal(
            "Active 1 | Queued 0\nBusy/decode 1.5",
            RuntimeDashboardService.RuntimeSlotsLabel(
            [
                new PrometheusSample("llamacpp:requests_processing", "", 1, "1", "gauge", ""),
                new PrometheusSample("llamacpp:requests_deferred", "", 0, "0", "gauge", ""),
                new PrometheusSample("llamacpp:n_busy_slots_per_decode", "", 1.5, "1.5", "gauge", "")
            ]));
        Assert.Equal("2.0 t/s (Gen) | 3.0 t/s (Avg) | 9 t (Total)\n1.5 t/s (Accepted) | 2.5 t/s (Avg) | 6 t (Total)", RuntimeDashboardService.MtpTokenSummaryLabel(2, 3, 1.5, 2.5, 9, 6));
        var parsedMtpStats = RuntimeDashboardService.ParseMtpTokenStats(
            "statistics        draft-mtp: #calls(b,g,a) =  566 142602 107915, #gen drafts = 107915, #acc drafts = 103668, #gen tokens = 294686, #acc tokens = 274174, dur(b,g,a) = 0.412, 851457.082, 118.639 ms");
        Assert.NotNull(parsedMtpStats);
        Assert.Equal(294686, parsedMtpStats.GeneratedTokens);
        Assert.Equal(274174, parsedMtpStats.AcceptedTokens);
        Assert.Equal(851.457082, parsedMtpStats.GeneratedSeconds);
        Assert.Equal(851.457082, parsedMtpStats.AcceptedSeconds);
        Assert.Equal(
            new RuntimeMtpTokenSnapshot(297, 171),
            RuntimeDashboardService.ParseMtpTokenStats("draft acceptance rate = 0.57576 (  171 accepted /   297 generated)"));
        Assert.Equal("2.0 t/s (3.0 avg)", RuntimeDashboardService.RateLabel(2, 3));
        Assert.Equal("Context 6,144\nKV cache 50%, 28 tokens", RuntimeDashboardService.RuntimeSettingsLabel(.5, 28, 6144, 4096));
    }


    [Fact]
    public void RuntimeLogTailServiceBuildsMissingLiveAndSlotAwareLogText()
    {
        var root = CreateTempRoot();
        var path = Path.Combine(root, "runtime.log");
        var service = new RuntimeLogTailService();

        var missingLive = service.Build(new RuntimeLogTailRequest(path, IsRuntimeRunning: true, SlotSnapshot: null));
        var missingStopped = service.Build(new RuntimeLogTailRequest(path, IsRuntimeRunning: false, SlotSnapshot: null));
        Directory.CreateDirectory(root);
        File.WriteAllText(path, "start\nall slots are idle\nALL SLOTS ARE IDLE\ndone");
        var processing = service.Build(new RuntimeLogTailRequest(
            path,
            IsRuntimeRunning: true,
            new RuntimeSlotSnapshot(
                PromptTokensProcessed: 12,
                GeneratedTokens: 8,
                IsProcessing: true,
                PromptTokens: 20,
                ContextTokens: 28,
                ContextSize: 4096)));
        var idle = service.Build(new RuntimeLogTailRequest(
            path,
            IsRuntimeRunning: false,
            new RuntimeSlotSnapshot(0, 0, IsProcessing: false, null, null, null)));

        Assert.False(missingLive.HasActiveLog);
        Assert.Equal("Runtime log file has not been created yet.", missingLive.Text);
        Assert.False(missingStopped.HasActiveLog);
        Assert.Equal("No runtime log is active.", missingStopped.Text);
        Assert.True(processing.HasActiveLog);
        Assert.Contains($"Live log: {path}", processing.Text, StringComparison.Ordinal);
        Assert.Contains("Slot status: processing | Prompt 12/20 | Gen 8", processing.Text, StringComparison.Ordinal);
        Assert.Contains("start", processing.Text, StringComparison.Ordinal);
        Assert.Contains("omitted 2 repeated 'all slots are idle' lines", processing.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("ALL SLOTS ARE IDLE", processing.Text, StringComparison.Ordinal);
        Assert.True(idle.HasActiveLog);
        Assert.Contains($"Last runtime log: {path}", idle.Text, StringComparison.Ordinal);
        Assert.Contains("Slot status: idle", idle.Text, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimeOverviewStatusServiceBuildsStoppedLoadedWarmAndFailedLabels()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var selected = new ModelRecord("model-1", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var service = new RuntimeOverviewStatusService();

        var noSelection = service.Labels(new RuntimeOverviewStatusRequest(null, null, LlamaRuntimeState.Stopped, null));
        var stoppedSelection = service.Labels(new RuntimeOverviewStatusRequest(selected, null, LlamaRuntimeState.Stopped, null));
        var running = service.Labels(new RuntimeOverviewStatusRequest(selected, RuntimeSession(root, settings, LoadedModelSessionStatus.Running, true), LlamaRuntimeState.Loaded, null));
        var warm = service.Labels(new RuntimeOverviewStatusRequest(selected, RuntimeSession(root, settings, LoadedModelSessionStatus.Warm, true), LlamaRuntimeState.Loaded, null));
        var loading = service.Labels(new RuntimeOverviewStatusRequest(selected, RuntimeSession(root, settings, LoadedModelSessionStatus.Loading, true), LlamaRuntimeState.Loading, null));
        var failed = service.Labels(new RuntimeOverviewStatusRequest(selected, RuntimeSession(root, settings, LoadedModelSessionStatus.Failed, false) with { RuntimeName = "" }, LlamaRuntimeState.Failed, 17));

        Assert.Equal(new RuntimeOverviewStatusLabels("None", "Stopped"), noSelection);
        Assert.Equal(new RuntimeOverviewStatusLabels("Stopped: Qwen", "No loaded runtime"), stoppedSelection);
        Assert.Equal(new RuntimeOverviewStatusLabels("Loaded: Qwen", "Runtime"), running);
        Assert.Equal(new RuntimeOverviewStatusLabels("Loaded: Qwen", "Runtime"), warm);
        Assert.Equal(new RuntimeOverviewStatusLabels("Loading: Qwen", "Runtime"), loading);
        Assert.Equal(new RuntimeOverviewStatusLabels("Failed (17): Qwen", "Unknown runtime"), failed);
    }


    [Fact]
    public void RuntimeMetricSummaryTrackerTracksLiveRatesAndLastKnownValues()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { SpeculativeType = "draft-mtp" };
        var tracker = new RuntimeMetricSummaryTracker();
        var capturedAt = DateTimeOffset.Parse("2026-05-26T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var firstSamples = new[]
        {
            new PrometheusSample("llama_tokens_predicted_total", "", 10, "10", "counter", ""),
            new PrometheusSample("llama_tokens_predicted_seconds_total", "", 5, "5", "counter", ""),
            new PrometheusSample("llama_prompt_tokens_total", "", 4, "4", "counter", ""),
            new PrometheusSample("llama_prompt_seconds_total", "", 2, "2", "counter", ""),
            new PrometheusSample("llama_mtp_tokens_generated_total", "", 6, "6", "counter", ""),
            new PrometheusSample("llama_mtp_tokens_generated_seconds_total", "", 3, "3", "counter", ""),
            new PrometheusSample("llama_mtp_tokens_accepted_total", "", 4, "4", "counter", ""),
            new PrometheusSample("llama_mtp_tokens_accepted_seconds_total", "", 2, "2", "counter", ""),
            new PrometheusSample("llama_requests_processing", "", 1, "1", "gauge", ""),
            new PrometheusSample("llama_requests_deferred", "", 2, "2", "gauge", ""),
            new PrometheusSample("llama_n_busy_slots_per_decode", "", 1.25, "1.25", "gauge", "")
        };
        var secondSamples = new[]
        {
            new PrometheusSample("llama_tokens_predicted_total", "", 16, "16", "counter", ""),
            new PrometheusSample("llama_tokens_predicted_seconds_total", "", 8, "8", "counter", ""),
            new PrometheusSample("llama_prompt_tokens_total", "", 8, "8", "counter", ""),
            new PrometheusSample("llama_prompt_seconds_total", "", 4, "4", "counter", ""),
            new PrometheusSample("llama_mtp_tokens_generated_total", "", 12, "12", "counter", ""),
            new PrometheusSample("llama_mtp_tokens_generated_seconds_total", "", 4, "4", "counter", ""),
            new PrometheusSample("llama_mtp_tokens_accepted_total", "", 10, "10", "counter", ""),
            new PrometheusSample("llama_mtp_tokens_accepted_seconds_total", "", 5, "5", "counter", ""),
            new PrometheusSample("llama_requests_processing", "", 2, "2", "gauge", ""),
            new PrometheusSample("llama_requests_deferred", "", 0, "0", "gauge", ""),
            new PrometheusSample("llama_n_busy_slots_per_decode", "", 1.5, "1.5", "gauge", "")
        };

        var first = tracker.Apply("model|runtime|8081", firstSamples, settings, slotSnapshot: null, mtpTokenSnapshot: null, capturedAt);
        var second = tracker.Apply("model|runtime|8081", secondSamples, settings, slotSnapshot: null, mtpTokenSnapshot: null, capturedAt.AddSeconds(2));
        var stale = tracker.Apply("model|runtime|8081", [], settings, slotSnapshot: null, mtpTokenSnapshot: null, capturedAt.AddSeconds(5));

        Assert.False(first.UsedLastKnown);
        Assert.Equal("Gen 10\nPrompt 4", first.TotalTokens);
        Assert.Equal("Unknown (Gen) | 2.0 t/s (Avg) | 10 t (Total)\nUnknown (Prompt) | 2.0 t/s (Avg) | 4 t (Total)", first.Tokens);
        Assert.False(second.UsedLastKnown);
        Assert.Equal("Gen 3.0 t/s (2.0 avg)\nPrompt 2.0 t/s (2.0 avg)", second.GenerationRate);
        Assert.Equal("Gen 16\nPrompt 8", second.TotalTokens);
        Assert.Equal("3.0 t/s (Gen) | 2.0 t/s (Avg) | 16 t (Total)\n2.0 t/s (Prompt) | 2.0 t/s (Avg) | 8 t (Total)", second.Tokens);
        Assert.Equal("3.0 t/s (Gen) | 3.0 t/s (Avg) | 12 t (Total)\n3.0 t/s (Accepted) | 2.0 t/s (Avg) | 10 t (Total)", second.MtpTokens);
        Assert.Equal("Active 2 | Queued 0\nBusy/decode 1.5", second.Slots);
        Assert.True(stale.UsedLastKnown);
        Assert.Equal(capturedAt.AddSeconds(2), stale.LastKnownCapturedAt);
        Assert.Equal(second.GenerationRate, stale.GenerationRate);
        Assert.Equal(second.MtpTokens, stale.MtpTokens);
        Assert.Equal(11, tracker.LastKnownSamples("model|runtime|8081").Count);
    }

    [Fact]
    public void RuntimeMetricSummaryTrackerShowsConfiguredMtpIdleInsteadOfBlank()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { SpeculativeType = "draft-mtp" };
        var tracker = new RuntimeMetricSummaryTracker();
        var capturedAt = DateTimeOffset.Parse("2026-05-26T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var summary = tracker.Apply("model|runtime|8081", [], settings, slotSnapshot: null, mtpTokenSnapshot: null, capturedAt: capturedAt);

        Assert.False(summary.UsedLastKnown);
        Assert.Equal("0.0 t/s (Gen)\n0.0 t/s (Accepted)", summary.MtpTokens);
    }

    [Fact]
    public void RuntimeMetricSummaryTrackerUsesLogMtpDurationsForAverages()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { SpeculativeType = "draft-mtp" };
        var tracker = new RuntimeMetricSummaryTracker();
        var capturedAt = DateTimeOffset.Parse("2026-05-26T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var firstStats = RuntimeDashboardService.ParseMtpTokenStats(
            "statistics draft-mtp: #calls(b,g,a) = 1 10 10, #gen drafts = 10, #acc drafts = 8, #gen tokens = 100, #acc tokens = 80, dur(b,g,a) = 0.001, 10000.000, 0.250 ms");
        var secondStats = RuntimeDashboardService.ParseMtpTokenStats(
            "statistics draft-mtp: #calls(b,g,a) = 2 20 20, #gen drafts = 20, #acc drafts = 13, #gen tokens = 160, #acc tokens = 130, dur(b,g,a) = 0.001, 20000.000, 0.500 ms");

        var first = tracker.Apply("model|runtime|8081", [], settings, slotSnapshot: null, mtpTokenSnapshot: firstStats, capturedAt: capturedAt);
        var second = tracker.Apply("model|runtime|8081", [], settings, slotSnapshot: null, mtpTokenSnapshot: secondStats, capturedAt: capturedAt.AddSeconds(2));
        var idle = tracker.Apply("model|runtime|8081", [], settings, slotSnapshot: null, mtpTokenSnapshot: secondStats, capturedAt: capturedAt.AddSeconds(4));
        var stale = tracker.Apply("model|runtime|8081", [], settings, slotSnapshot: null, mtpTokenSnapshot: null, capturedAt: capturedAt.AddSeconds(6));

        Assert.Equal("Unknown (Gen) | 10.0 t/s (Avg) | 100 t (Total)\nUnknown (Accepted) | 8.0 t/s (Avg) | 80 t (Total)", first.MtpTokens);
        Assert.Equal("30.0 t/s (Gen) | 8.0 t/s (Avg) | 160 t (Total)\n25.0 t/s (Accepted) | 6.5 t/s (Avg) | 130 t (Total)", second.MtpTokens);
        Assert.Equal("0.0 t/s (Gen) | 8.0 t/s (Avg) | 160 t (Total)\n0.0 t/s (Accepted) | 6.5 t/s (Avg) | 130 t (Total)", idle.MtpTokens);
        Assert.True(stale.UsedLastKnown);
        Assert.Equal(idle.MtpTokens, stale.MtpTokens);
    }

    [Fact]
    public void GpuSummaryCacheOwnsFreshnessAndFallback()
    {
        var cache = new GpuSummaryCache();
        var now = DateTimeOffset.Parse("2026-05-28T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        Assert.False(cache.TryGet(now, out var initial));
        Assert.Equal("Unavailable", initial);

        Assert.Equal("Intel Arc 24 GB free", cache.Store("Intel Arc 24 GB free", now));
        Assert.True(cache.TryGet(now.AddSeconds(9), out var fresh));
        Assert.Equal("Intel Arc 24 GB free", fresh);

        Assert.False(cache.TryGet(now.AddSeconds(10), out var expired));
        Assert.Equal("Unavailable", expired);
        Assert.Equal("Unavailable", cache.Store("", now));
        Assert.Equal("GPU 0: 76% | 62C | 12.0/24.0 GiB", cache.Store("GPU 0: 76%|62C|12.0/24.0 GiB", now));

        cache.Store("NVIDIA 16 GB free", now);
        cache.Clear();

        Assert.False(cache.TryGet(now.AddSeconds(1), out var cleared));
        Assert.Equal("Unavailable", cleared);
    }

    [Fact]
    public async Task RuntimeGpuSummaryApplicationServiceChoosesProbeAndCachesByActiveSession()
    {
        var root = CreateTempRoot();
        var now = DateTimeOffset.Parse("2026-05-28T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var files = new List<string>();
        var runner = new ScriptedProcessRunner(psi =>
        {
            files.Add(psi.FileName ?? "");
            return new ProcessRunResult(0, "[level_zero:gpu][level_zero:0] Intel(R) Arc(TM) A770 Graphics", "");
        });
        var service = new RuntimeGpuSummaryApplicationService(
            new GpuStatusProbeService(runner, () => "sycl-ls.exe"),
            new GpuSummaryCache(),
            () => "wsl.exe");
        var nativeSycl = Session(RuntimeMode.Native, RuntimeBackend.Sycl, AppSettings.CreateDefault(root), now);

        var first = await service.SummaryAsync(nativeSycl, now, TestContext.Current.CancellationToken);
        var cached = await service.SummaryAsync(nativeSycl, now.AddSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal("Intel(R) Arc(TM) A770 Graphics", first);
        Assert.Equal(first, cached);
        Assert.Equal(["sycl-ls.exe"], files);

        files.Clear();
        var wslService = new RuntimeGpuSummaryApplicationService(
            new GpuStatusProbeService(runner, () => "sycl-ls.exe"),
            new GpuSummaryCache(),
            () => "wsl.exe");
        var wslSycl = Session(RuntimeMode.Wsl, RuntimeBackend.Sycl, AppSettings.CreateDefault(root) with { WslDistro = "Ubuntu-24.04" }, now);

        var wsl = await wslService.SummaryAsync(wslSycl, now, TestContext.Current.CancellationToken);

        Assert.Equal("Intel(R) Arc(TM) A770 Graphics", wsl);
        Assert.Equal("wsl.exe", files.Single());
        Assert.Equal(["-d", "Ubuntu-24.04", "--", "bash", "-lc"], runner.Commands.Last().Take(5).ToArray());

        var nvidiaRunner = new ScriptedProcessRunner(_ => new ProcessRunResult(0, "0, NVIDIA RTX, 76, 62, 12288, 24576", ""));
        var nvidiaService = new RuntimeGpuSummaryApplicationService(
            new GpuStatusProbeService(nvidiaRunner, () => ""),
            new GpuSummaryCache(),
            () => "wsl.exe");

        var nvidia = await nvidiaService.SummaryAsync(Session(RuntimeMode.Native, RuntimeBackend.Cuda, AppSettings.CreateDefault(root), now), now, TestContext.Current.CancellationToken);

        Assert.Equal("GPU 0: 76% | 62C | 12.0/24.0 GiB", nvidia);

        static LoadedModelSessionSnapshot Session(RuntimeMode mode, RuntimeBackend backend, AppSettings settings, DateTimeOffset startedAt)
            => new(
                "session",
                "model",
                "Model",
                "runtime",
                "Runtime",
                mode,
                backend,
                settings,
                "",
                startedAt,
                "",
                0,
                LoadedModelSessionStatus.Running,
                IsRunning: true,
                IsSelected: true);
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
    public async Task RuntimeIdleUnloadPolicyServiceOwnsReentrancyAndUnloadSelection()
    {
        var service = new RuntimeIdleUnloadPolicyService();
        var root = CreateTempRoot();
        var now = DateTimeOffset.Parse("2026-05-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var first = PollResult(root, "model-a", "Model A", 8081, new RuntimeSlotSnapshot(0, 0, false, null, null, null));
        var second = PollResult(root, "model-b", "Model B", 8082, new RuntimeSlotSnapshot(0, 0, false, null, null, null));
        var unloaded = new List<string>();

        var firstPass = await service.ApplyAsync(
            [first, second],
            idleMinutes: 1,
            now: now,
            (_, _) => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, firstPass);
        Assert.Equal(2, service.TrackedRuntimeCount);

        var secondPass = await service.ApplyAsync(
            [first, second],
            idleMinutes: 1,
            now: now.AddSeconds(61),
            async (idle, token) =>
            {
                unloaded.Add(idle.Session.ModelId);
                var nested = await service.ApplyAsync([idle], 1, now.AddSeconds(62), (_, _) => Task.CompletedTask, token);
                Assert.Equal(0, nested);
                Assert.True(service.IsApplying);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, secondPass);
        Assert.Equal(["model-a", "model-b"], unloaded);
        Assert.False(service.IsApplying);

        service.Reset(first.RuntimeKey);
        Assert.Equal(1, service.TrackedRuntimeCount);

        var resetPass = await service.ApplyAsync(
            [],
            idleMinutes: 1,
            now: now.AddMinutes(2),
            (_, _) => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, resetPass);
        Assert.Equal(0, service.TrackedRuntimeCount);

        static RuntimeMetricPollResult PollResult(string root, string modelId, string modelName, int port, RuntimeSlotSnapshot slot)
        {
            var settings = AppSettings.CreateDefault(root) with { Port = port };
            var session = new LoadedModelSessionSnapshot(
                $"session-{modelId}",
                modelId,
                modelName,
                $"runtime-{port}",
                $"Runtime {port}",
                RuntimeMode.Native,
                RuntimeBackend.Cpu,
                settings,
                Path.Combine(root, $"{modelId}.log"),
                DateTimeOffset.UtcNow,
                "",
                0,
                LoadedModelSessionStatus.Running,
                IsRunning: true,
                IsSelected: false);

            return new RuntimeMetricPollResult(
                session,
                RuntimeMetricPollerService.RuntimeKey(session),
                [],
                slot,
                "");
        }
    }


    [Fact]
    public void RuntimeDashboardRefreshCoordinatorOwnsAdmissionGateAndPollSelection()
    {
        var coordinator = new RuntimeDashboardRefreshCoordinator();
        var source = ReadMainWindowSources();
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var running = RuntimeSession(root, settings with { Port = 8081 }, LoadedModelSessionStatus.Running, isRunning: true);
        var warm = RuntimeSession(root, settings with { Port = 8082 }, LoadedModelSessionStatus.Warm, isRunning: true) with { SessionId = "session-2" };
        var loading = RuntimeSession(root, settings with { Port = 8083 }, LoadedModelSessionStatus.Loading, isRunning: true) with { SessionId = "session-3" };
        var stopped = RuntimeSession(root, settings with { Port = 8084 }, LoadedModelSessionStatus.Running, isRunning: false) with { SessionId = "session-4" };

        Assert.True(coordinator.ShouldRunTimer("Overview", hasRunningSessions: false));
        Assert.True(coordinator.ShouldRunTimer("Models", hasRunningSessions: true));
        Assert.False(coordinator.ShouldRunTimer("Models", hasRunningSessions: false));
        Assert.Null(coordinator.TryBeginRefresh(new RuntimeDashboardRefreshTarget(false, false, false, false)));

        using (var refresh = coordinator.TryBeginRefresh(new RuntimeDashboardRefreshTarget(false, true, false, false)))
        {
            Assert.NotNull(refresh);
            Assert.Null(coordinator.TryBeginRefresh(new RuntimeDashboardRefreshTarget(true, false, false, false)));
        }

        using var nextRefresh = coordinator.TryBeginRefresh(new RuntimeDashboardRefreshTarget(true, false, false, false));
        Assert.NotNull(nextRefresh);

        var pollable = coordinator.PollableSessions([running, warm, loading, stopped]);
        Assert.Equal(["session-1", "session-2"], pollable.Select(session => session.SessionId).ToArray());
        Assert.Contains("_coreServices.Ui.RuntimeDashboardRefreshTimer.Start(", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.RuntimeDashboardRefreshTimer.Stop()", source, StringComparison.Ordinal);
        Assert.Contains("RuntimeDashboardTimerRefreshAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeDashboardTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeDashboardTimer_Tick", source, StringComparison.Ordinal);
    }


    [Fact]
    public async Task RuntimeTelemetryApplicationServiceOwnsPollingAndCounters()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { EnableMetrics = false };
        var running = RuntimeSession(root, settings with { Port = 8081 }, LoadedModelSessionStatus.Running, isRunning: true);
        var warm = RuntimeSession(root, settings with { Port = 8082 }, LoadedModelSessionStatus.Warm, isRunning: true) with { SessionId = "session-2" };
        var loading = RuntimeSession(root, settings with { Port = 8083 }, LoadedModelSessionStatus.Loading, isRunning: true) with { SessionId = "session-3" };
        var stopped = RuntimeSession(root, settings with { Port = 8084 }, LoadedModelSessionStatus.Running, isRunning: false) with { SessionId = "session-4" };

        using var http = new HttpClient(new CapturingHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("""[{"is_processing":false,"n_prompt_tokens_processed":0,"n_decoded":0,"n_ctx":4096}]""")
        }));
        var service = new RuntimeTelemetryApplicationService(
            new RuntimeMetricPollerService(http),
            new RuntimeDashboardRefreshCoordinator(),
            new RuntimeMetricSummaryTracker(),
            new RuntimeLifetimeCounterTracker(),
            new RuntimeIdleUnloadPolicyService());

        Assert.True(service.ShouldRunRefreshTimer("Overview", hasRunningSessions: false));
        using var refresh = service.TryBeginRefresh(new RuntimeDashboardRefreshTarget(false, true, false, false));
        Assert.NotNull(refresh);

        var results = await service.PollSessionsAsync([running, warm, loading, stopped], TestContext.Current.CancellationToken);
        Assert.Equal(["session-1", "session-2"], results.Select(result => result.Session.SessionId).ToArray());

        var first = service.ObserveLifetimeTokenDeltas([CounterResult(generated: 10, prompt: 4)]);
        var second = service.ObserveLifetimeTokenDeltas([CounterResult(generated: 16, prompt: 8)]);

        Assert.Empty(first);
        var delta = Assert.Single(second);
        Assert.Equal(4, delta.PromptTokens);
        Assert.Equal(6, delta.GeneratedTokens);

        RuntimeMetricPollResult CounterResult(int generated, int prompt)
        {
            var session = RuntimeSession(root, settings with { Port = 8081 }, LoadedModelSessionStatus.Running, isRunning: true);
            return new RuntimeMetricPollResult(
                session,
                RuntimeMetricPollerService.RuntimeKey(session),
                [
                    new PrometheusSample("llama_tokens_predicted_total", "", generated, generated.ToString(System.Globalization.CultureInfo.InvariantCulture), "counter", ""),
                    new PrometheusSample("llama_prompt_tokens_total", "", prompt, prompt.ToString(System.Globalization.CultureInfo.InvariantCulture), "counter", "")
                ],
                null,
                "");
        }
    }


    [Fact]
    public async Task RuntimeTelemetryApplicationServiceOwnsIdleUnloadActions()
    {
        var root = CreateTempRoot();
        var now = DateTimeOffset.Parse("2026-05-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var model = new ModelRecord("model-a", "Model A", Path.Combine(root, "a.gguf"), OwnershipKind.External, "{}", now);
        var settings = AppSettings.CreateDefault(root) with { Port = 8081 };
        var session = RuntimeSession(root, settings, LoadedModelSessionStatus.Running, isRunning: true) with
        {
            ModelId = model.Id,
            ModelName = model.Name
        };
        var result = new RuntimeMetricPollResult(
            session,
            RuntimeMetricPollerService.RuntimeKey(session),
            [],
            new RuntimeSlotSnapshot(0, 0, false, null, null, null),
            "");
        var statuses = new List<string>();
        var stopped = new List<string>();
        var actions = new RuntimeIdleUnloadApplicationActions(
            id => Task.FromResult<ModelRecord?>(id == model.Id ? model : null),
            unloaded =>
            {
                stopped.Add(unloaded.Id);
                return Task.CompletedTask;
            },
            statuses.Add);
        var service = new RuntimeTelemetryApplicationService(
            new RuntimeMetricPollerService(new HttpClient(new CapturingHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)))),
            new RuntimeDashboardRefreshCoordinator(),
            new RuntimeMetricSummaryTracker(),
            new RuntimeLifetimeCounterTracker(),
            new RuntimeIdleUnloadPolicyService());

        var firstPass = await service.ApplyIdleUnloadPoliciesAsync(
            [result],
            idleMinutes: 1,
            now,
            actions,
            TestContext.Current.CancellationToken);
        var secondPass = await service.ApplyIdleUnloadPoliciesAsync(
            [result],
            idleMinutes: 1,
            now.AddSeconds(61),
            actions,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, firstPass);
        Assert.Equal(1, secondPass);
        Assert.Equal(["Auto-unloading Model A after 1 idle minute."], statuses);
        Assert.Equal([model.Id], stopped);
    }


    [Fact]
    public void RuntimeDashboardSelectionServiceChoosesRenderedSessionAndRuntimeKey()
    {
        var root = CreateTempRoot();
        var service = new RuntimeDashboardSelectionService();
        var defaults = AppSettings.CreateDefault(root) with { Port = 8081 };
        var activeSettings = defaults with { Port = 8091 };
        var selectedSettings = defaults with { Port = 8099 };
        var selectedModel = new ModelRecord("model-1", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var selectedSession = RuntimeSession(root, selectedSettings, LoadedModelSessionStatus.Running, isRunning: true);

        var selected = service.Select(new RuntimeDashboardSelectionRequest(
            selectedModel,
            SelectedOverviewModelIsActive: false,
            SelectedOverviewModelIsLoaded: true,
            selectedSession,
            SelectedSession: null,
            ActiveSessionSettings: activeSettings,
            ActiveRuntimeSettings: defaults,
            defaults,
            ActiveModelId: "active-model",
            ActiveRuntimeId: "active-runtime"));
        var fallback = service.Select(new RuntimeDashboardSelectionRequest(
            SelectedOverviewModel: null,
            SelectedOverviewModelIsActive: false,
            SelectedOverviewModelIsLoaded: false,
            SelectedOverviewModelSession: null,
            SelectedSession: null,
            ActiveSessionSettings: null,
            ActiveRuntimeSettings: activeSettings,
            defaults,
            ActiveModelId: "active-model",
            ActiveRuntimeId: "active-runtime"));

        Assert.True(selected.SelectSelectedOverviewModel);
        Assert.False(selected.SelectedOverviewModelHasNoRunningSession);
        Assert.Same(selectedSession, selected.Session);
        Assert.Equal(selectedSettings.Port, selected.MetricsSettings.Port);
        Assert.Equal(RuntimeMetricPollerService.RuntimeKey(selectedSession), selected.RuntimeKey);
        Assert.False(fallback.SelectSelectedOverviewModel);
        Assert.False(fallback.SelectedOverviewModelHasNoRunningSession);
        Assert.Null(fallback.Session);
        Assert.Equal(activeSettings.Port, fallback.MetricsSettings.Port);
        Assert.Equal("active-model|active-runtime|8091", fallback.RuntimeKey);

        var stoppedSelected = service.Select(new RuntimeDashboardSelectionRequest(
            selectedModel,
            SelectedOverviewModelIsActive: false,
            SelectedOverviewModelIsLoaded: false,
            selectedSession with { IsRunning = false },
            SelectedSession: null,
            ActiveSessionSettings: activeSettings,
            ActiveRuntimeSettings: defaults,
            defaults,
            ActiveModelId: "active-model",
            ActiveRuntimeId: "active-runtime"));

        Assert.True(stoppedSelected.SelectedOverviewModelHasNoRunningSession);
    }


    [Fact]
    public void RuntimeDashboardRenderDecisionServiceChoosesMetricRenderBranch()
    {
        var root = CreateTempRoot();
        var service = new RuntimeDashboardRenderDecisionService();
        var settings = AppSettings.CreateDefault(root) with { EnableMetrics = true };
        var session = RuntimeSession(root, settings, LoadedModelSessionStatus.Running, isRunning: true);
        var slot = new RuntimeSlotSnapshot(4, 8, false, 2, 16, 4096);
        var sample = new PrometheusSample("llama_tokens_predicted_total", "", 7, "7", "counter", "");
        var freshResult = new RuntimeMetricPollResult(session, RuntimeMetricPollerService.RuntimeKey(session), [sample], slot, "");
        var errorResult = new RuntimeMetricPollResult(session, RuntimeMetricPollerService.RuntimeKey(session), [], slot, "temporarily unavailable");

        var noRuntime = service.Decide(new RuntimeDashboardRenderDecisionRequest(
            SelectedSession: null,
            settings,
            SelectedPollResult: null));
        var metricsDisabled = service.Decide(new RuntimeDashboardRenderDecisionRequest(
            session,
            settings with { EnableMetrics = false },
            freshResult));
        var fresh = service.Decide(new RuntimeDashboardRenderDecisionRequest(
            session,
            settings,
            freshResult));
        var unavailable = service.Decide(new RuntimeDashboardRenderDecisionRequest(
            session,
            settings,
            errorResult));
        var noResponse = service.Decide(new RuntimeDashboardRenderDecisionRequest(
            session,
            settings,
            SelectedPollResult: null));

        Assert.Equal(RuntimeDashboardRenderDecisionKind.NoRuntime, noRuntime.Kind);
        Assert.Equal(RuntimeDashboardRenderDecisionKind.MetricsDisabled, metricsDisabled.Kind);
        Assert.Equal(slot, metricsDisabled.SlotSnapshot);
        Assert.Equal(RuntimeDashboardRenderDecisionKind.FreshMetrics, fresh.Kind);
        Assert.Equal([sample], fresh.Samples);
        Assert.Equal(RuntimeDashboardRenderDecisionKind.MetricsUnavailable, unavailable.Kind);
        Assert.Equal("temporarily unavailable", unavailable.Error);
        Assert.Equal("No metrics response.", noResponse.Error);
    }

    [Fact]
    public void RuntimeMetricRowsRenderServiceBuildsLastKnownAndErrorRows()
    {
        var service = new RuntimeMetricRowsRenderService();
        var sample = new PrometheusSample("llama_tokens_predicted_total", "", 7, "7", "counter", "");

        var fromSamples = service.FromSamples([sample]);
        Assert.Equal([sample], fromSamples.Samples);
        Assert.Null(fromSamples.LeadingRow);

        var lastKnown = service.Unavailable("temporarily unavailable", [sample]);
        Assert.Equal([sample], lastKnown.Samples);
        Assert.NotNull(lastKnown.LeadingRow);
        Assert.Equal("metrics_status", lastKnown.LeadingRow.C1);
        Assert.Equal("Last known values; refresh paused (temporarily unavailable)", lastKnown.LeadingRow.C3);

        var missing = service.Unavailable("No metrics response.", []);
        Assert.Null(missing.LeadingRow);
        Assert.Single(missing.Samples);
        Assert.Equal("metrics_error", missing.Samples[0].Name);
        Assert.Equal("No metrics response.", missing.Samples[0].RawValue);
    }

    [Fact]
    public void RuntimeDashboardMetricsApplicationServiceOwnsRenderBranchSideEffects()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { EnableMetrics = true };
        var session = RuntimeSession(root, settings, LoadedModelSessionStatus.Running, isRunning: true);
        var runtimeKey = RuntimeMetricPollerService.RuntimeKey(session);
        var slot = new RuntimeSlotSnapshot(4, 8, false, 2, 16, 4096);
        var sample = new PrometheusSample("llama_tokens_predicted_total", "", 7, "7", "counter", "");
        var freshResult = new RuntimeMetricPollResult(session, runtimeKey, [sample], slot, "");
        var unavailableResult = new RuntimeMetricPollResult(session, runtimeKey, [], null, "temporarily unavailable");
        var service = new RuntimeDashboardMetricsApplicationService(
            new RuntimeTelemetryApplicationService(
                new RuntimeMetricPollerService(new HttpClient()),
                new RuntimeDashboardRefreshCoordinator(),
                new RuntimeMetricSummaryTracker(),
                new RuntimeLifetimeCounterTracker(),
                new RuntimeIdleUnloadPolicyService()),
            new RuntimeDashboardRenderDecisionService(),
            new RuntimeMetricRowsRenderService());
        var calls = new List<string>();
        var rows = new List<RuntimeMetricRowsRenderPlan>();
        var summaries = new List<RuntimeMetricSummaryPresentation>();

        var fresh = service.Apply(
            new RuntimeDashboardMetricsApplicationRequest(true, session, settings, freshResult, runtimeKey),
            Actions());
        var freshCalls = calls.ToArray();
        var freshRows = rows.ToArray();
        var freshSummaries = summaries.ToArray();
        Clear();

        var unavailable = service.Apply(
            new RuntimeDashboardMetricsApplicationRequest(true, session, settings, unavailableResult, runtimeKey),
            Actions());
        var unavailableRows = rows.ToArray();
        var unavailableSummary = summaries.Single();
        Clear();

        var offOverview = service.Apply(
            new RuntimeDashboardMetricsApplicationRequest(false, session, settings, freshResult, runtimeKey),
            Actions());
        var offOverviewCalls = calls.ToArray();
        var offOverviewSummary = summaries.Single();
        Clear();

        var noRuntime = service.Apply(
            new RuntimeDashboardMetricsApplicationRequest(true, null, settings, null, runtimeKey),
            Actions());

        Assert.Equal(RuntimeDashboardRenderDecisionKind.FreshMetrics, fresh);
        Assert.Contains("log:slot", freshCalls);
        Assert.Equal([sample], freshRows.Single().Samples);
        Assert.Null(freshSummaries.Single().LastKnownCapturedAt);

        Assert.Equal(RuntimeDashboardRenderDecisionKind.MetricsUnavailable, unavailable);
        Assert.Equal("metrics_status", unavailableRows.Single().LeadingRow?.C1);
        Assert.NotNull(unavailableSummary.LastKnownCapturedAt);

        Assert.Equal(RuntimeDashboardRenderDecisionKind.FreshMetrics, offOverview);
        Assert.DoesNotContain(offOverviewCalls, call => call.StartsWith("log:", StringComparison.Ordinal));
        Assert.DoesNotContain(offOverviewCalls, call => call.StartsWith("rows:", StringComparison.Ordinal));
        Assert.Null(offOverviewSummary.LastKnownCapturedAt);

        Assert.Equal(RuntimeDashboardRenderDecisionKind.NoRuntime, noRuntime);
        Assert.Equal(RuntimeMetricSummaryPresentation.NoRuntime, summaries.Single());

        RuntimeDashboardMetricsApplicationActions Actions()
            => new(
                slotSnapshot => calls.Add(slotSnapshot is null ? "log:none" : "log:slot"),
                plan =>
                {
                    rows.Add(plan);
                    calls.Add($"rows:{plan.Samples.Count}:{plan.LeadingRow?.C1 ?? ""}");
                },
                () =>
                {
                    calls.Add("mtp:none");
                    return null;
                },
                summary =>
                {
                    summaries.Add(summary);
                    calls.Add($"summary:{summary.Tokens}");
                });

        void Clear()
        {
            calls.Clear();
            rows.Clear();
            summaries.Clear();
        }
    }


    [Fact]
    public async Task RuntimeDashboardRefreshApplicationServiceOwnsRefreshSequence()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { Port = 8081, EnableMetrics = true };
        var model = new ModelRecord("model-1", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var session = RuntimeSession(root, settings, LoadedModelSessionStatus.Running, isRunning: true) with
        {
            ModelId = model.Id,
            ModelName = model.Name
        };
        using var handler = new CapturingHttpHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/metrics")
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("llama_tokens_predicted_total 7\n")
                };
            }

            if (request.RequestUri?.AbsolutePath == "/slots")
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"is_processing":true,"n_prompt_tokens_processed":4,"n_decoded":7,"n_ctx":4096}]""")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });
        using var http = new HttpClient(handler);
        var telemetry = new RuntimeTelemetryApplicationService(
            new RuntimeMetricPollerService(http),
            new RuntimeDashboardRefreshCoordinator(),
            new RuntimeMetricSummaryTracker(),
            new RuntimeLifetimeCounterTracker(),
            new RuntimeIdleUnloadPolicyService());
        var service = new RuntimeDashboardRefreshApplicationService(
            telemetry,
            new RuntimeDashboardSelectionService(),
            new RuntimeDashboardMetricsApplicationService(
                telemetry,
                new RuntimeDashboardRenderDecisionService(),
                new RuntimeMetricRowsRenderService()));
        var calls = new List<string>();
        AppSettings? activeRuntimeSettings = null;

        var outcome = await service.RefreshAsync(
            new RuntimeDashboardRefreshApplicationRequest(
                new RuntimeDashboardRefreshTarget(true, true, true, true),
                true,
                settings,
                "",
                "",
                LlamaRuntimeState.Loaded,
                true),
            new RuntimeDashboardRefreshApplicationActions(
                () =>
                {
                    calls.Add("mark");
                    return Task.CompletedTask;
                },
                () => calls.Add("overview"),
                () => [session],
                results =>
                {
                    calls.Add($"lifetime:{results.Count}");
                    return Task.CompletedTask;
                },
                results =>
                {
                    calls.Add($"idle:{results.Count}");
                    return Task.CompletedTask;
                },
                () => model,
                _ => false,
                _ => true,
                _ => session,
                () => null,
                () => null,
                () => activeRuntimeSettings,
                selectedModelId =>
                {
                    calls.Add($"select:{selectedModelId}");
                    return new RuntimeSessionSelectResult(true, settings);
                },
                selectedSettings =>
                {
                    activeRuntimeSettings = selectedSettings;
                    calls.Add($"active:{selectedSettings?.Port}");
                },
                () =>
                {
                    calls.Add("labels");
                    return Task.FromResult(("Model label", "Runtime label"));
                },
                modelStatus => calls.Add($"model:{modelStatus}"),
                () =>
                {
                    calls.Add("save");
                    return Task.CompletedTask;
                },
                () => calls.Add("progress"),
                () =>
                {
                    calls.Add("gpu-read");
                    return Task.FromResult("GPU summary");
                },
                gpu => calls.Add($"gpu:{gpu}"),
                (_, _) =>
                {
                    calls.Add("stopped");
                    return Task.CompletedTask;
                },
                new RuntimeDashboardMetricsApplicationActions(
                    _ => calls.Add("metrics-log"),
                    _ => calls.Add("metrics-rows"),
                    () =>
                    {
                        calls.Add("metrics-mtp");
                        return null;
                    },
                    _ => calls.Add("metrics-summary")),
                () => calls.Add("actions")),
            TestContext.Current.CancellationToken);

        Assert.Equal(RuntimeDashboardRefreshApplicationOutcome.Applied, outcome);
        Assert.DoesNotContain("stopped", calls);
        Assert.DoesNotContain("save", calls);
        Assert.Equal(
            [
                "mark",
                "overview",
                "lifetime:1",
                "idle:1",
                $"select:{model.Id}",
                "active:8081",
                "labels",
                "model:Model label",
                "progress",
                "gpu-read",
                "gpu:GPU summary",
                "metrics-log",
                "metrics-rows",
                "metrics-mtp",
                "metrics-summary",
                "actions"
            ],
            calls);
    }


    [Fact]
    public void RuntimeDashboardPollsAllLoadedSessionsForLifetimeMetricsBeforeRenderingSelection()
    {
        var dashboard = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeDashboard.cs"));
        var counters = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeMetricCounters.cs"));
        var metrics = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeMetrics.cs"));
        var refreshApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeDashboardRefreshApplicationService.cs"));
        var selection = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeDashboardSelectionService.cs"));
        var renderDecisions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeDashboardRenderDecisionService.cs"));
        var metricRows = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeMetricRowsRenderService.cs"));
        var session = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeSession.cs"));
        var poller = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeMetricPollerService.cs"));
        var refreshCoordinator = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeDashboardRefreshCoordinator.cs"));
        var telemetry = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeTelemetryApplicationService.cs"));
        var logTail = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeLogTailService.cs"));
        var overviewStatus = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeOverviewStatusService.cs"));
        var overviewModelSelection = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "OverviewModelSelectionApplicationService.cs"));
        var overviewSelection = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.OverviewSelection.cs"));

        Assert.Contains("_coreServices.Runtime.RuntimeDashboardRefreshApplication.RefreshAsync(", dashboard, StringComparison.Ordinal);
        Assert.Contains("await _telemetry.PollSessionsAsync(actions.SessionSnapshots()", refreshApplication, StringComparison.Ordinal);
        Assert.Contains("var pollableSessions = _refreshCoordinator.PollableSessions(sessions)", telemetry, StringComparison.Ordinal);
        Assert.Contains("_poller.PollSessionsAsync(pollableSessions, cancellationToken)", telemetry, StringComparison.Ordinal);
        Assert.Contains("Where(session => session is { IsRunning: true, Status: LoadedModelSessionStatus.Running or LoadedModelSessionStatus.Warm })", refreshCoordinator, StringComparison.Ordinal);
        Assert.Contains("await actions.TrackLifetimeTokenDeltasAsync(pollResults)", refreshApplication, StringComparison.Ordinal);
        Assert.True(
            refreshApplication.IndexOf("await actions.TrackLifetimeTokenDeltasAsync(pollResults)", StringComparison.Ordinal)
            < refreshApplication.IndexOf("var selectedOverviewModel = actions.SelectedOverviewModel()", StringComparison.Ordinal));
        Assert.DoesNotContain("ResetLifetimeCounters();", dashboard, StringComparison.Ordinal);
        var lifetimeStart = counters.IndexOf("private async Task TrackLifetimeTokenDeltasAsync", StringComparison.Ordinal);
        var lifetimeEnd = counters.IndexOf("private void ResetLifetimeCounters()", lifetimeStart, StringComparison.Ordinal);
        Assert.DoesNotContain("_llama.ActiveModelId", counters[lifetimeStart..lifetimeEnd], StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeTelemetryApplication.ObserveLifetimeTokenDeltas(pollResults)", counters, StringComparison.Ordinal);
        Assert.Contains("var lifetimeMetrics = AppServices.LifetimeMetricsApplication", counters, StringComparison.Ordinal);
        Assert.Contains("await lifetimeMetrics.AddUsageAsync(delta)", counters, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStore.AddTokenUsageAsync", counters, StringComparison.Ordinal);
        Assert.Contains("RuntimeDashboardService.GeneratedTokenCounter(result.Samples)", telemetry, StringComparison.Ordinal);
        Assert.Contains("RuntimeDashboardService.PromptTokenCounter(result.Samples)", telemetry, StringComparison.Ordinal);
        Assert.Contains("result.SlotSnapshot", telemetry, StringComparison.Ordinal);
        Assert.Contains("_selection.Select(new RuntimeDashboardSelectionRequest(", refreshApplication, StringComparison.Ordinal);
        Assert.Contains("_metricsApplication.Apply(", refreshApplication, StringComparison.Ordinal);
        Assert.Contains("RuntimeDashboardRenderDecisionKind.MetricsUnavailable", renderDecisions, StringComparison.Ordinal);
        var metricsApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeDashboardMetricsApplicationService.cs"));
        Assert.Contains("_renderDecisions.Decide(new RuntimeDashboardRenderDecisionRequest(", metricsApplication, StringComparison.Ordinal);
        Assert.Contains("_rowsRender.Unavailable(", metricsApplication, StringComparison.Ordinal);
        Assert.Contains("_telemetry.ResetMetricCounters()", metricsApplication, StringComparison.Ordinal);
        Assert.Contains("Last known values; refresh paused", metricRows, StringComparison.Ordinal);
        Assert.DoesNotContain("Last known values; refresh paused", metrics, StringComparison.Ordinal);
        Assert.Contains("RuntimeMetricPollerService.RuntimeKey(session)", selection, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeMetricKey(LoadedModelSessionSnapshot session)", metrics, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeLogTail.Build(new RuntimeLogTailRequest(", metrics, StringComparison.Ordinal);
        Assert.Contains("LogFileService.Tail(request.LogPath", logTail, StringComparison.Ordinal);
        Assert.Contains("Slot status: processing", logTail, StringComparison.Ordinal);
        Assert.DoesNotContain("LogFileService.Tail(_llama.LogPath", metrics, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeOverviewStatus.Labels(new RuntimeOverviewStatusRequest(", overviewSelection, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.OverviewModelSelectionApplication.SelectAsync(", overviewSelection, StringComparison.Ordinal);
        Assert.Contains("OverviewModelSelectionActions()", overviewSelection, StringComparison.Ordinal);
        Assert.Contains("Load it to expose an OpenAI-compatible endpoint.", overviewModelSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("Load it to expose an OpenAI-compatible endpoint.", overviewSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedLoadedModel && !IsModelActive", overviewSelection, StringComparison.Ordinal);
        Assert.Contains("LoadedModelSessionStatus.Warm => \"Loaded\"", overviewStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadedModelSessionStatus.Warm => \"Loaded\"", overviewSelection, StringComparison.Ordinal);
        Assert.Contains("RuntimeMetrics.ParsePrometheus(raw)", poller, StringComparison.Ordinal);
        Assert.Contains("RuntimeDashboardService.ParseSlotSnapshot(raw)", poller, StringComparison.Ordinal);
        Assert.DoesNotContain("PollRuntimeMetricsForSessionAsync", dashboard, StringComparison.Ordinal);
        var refreshStart = refreshApplication.IndexOf("public async Task<RuntimeDashboardRefreshApplicationOutcome> RefreshAsync", StringComparison.Ordinal);
        var readinessIndex = refreshApplication.IndexOf("await actions.MarkLoadedSessionsIfReadyAsync();", refreshStart, StringComparison.Ordinal);
        var sessionRowsIndex = refreshApplication.IndexOf("actions.RefreshOverviewSessionRows();", readinessIndex, StringComparison.Ordinal);
        Assert.True(readinessIndex >= 0 && sessionRowsIndex > readinessIndex);
        Assert.Contains("ReplaceSessionsIfChanged", session, StringComparison.Ordinal);
    }


    [Fact]
    public async Task OverviewModelSelectionApplicationServiceOwnsLoadedInactiveAndStoppedSelection()
    {
        var root = CreateTempRoot();
        var model = new ModelRecord(
            "model-1",
            "Qwen",
            Path.Combine(root, "models", "qwen.gguf"),
            OwnershipKind.External,
            "{}",
            DateTimeOffset.UtcNow);
        var activeSettings = AppSettings.CreateDefault(root) with { Port = 8084 };
        var service = new OverviewModelSelectionApplicationService();
        var calls = new List<string>();

        OverviewModelSelectionApplicationActions Actions(bool selectSucceeds = true)
            => new(
                modelId =>
                {
                    calls.Add($"select:{modelId}");
                    return new RuntimeSessionSelectResult(selectSucceeds, selectSucceeds ? activeSettings : null);
                },
                settings => calls.Add($"active:{settings?.Port}"),
                () =>
                {
                    calls.Add("save");
                    return Task.CompletedTask;
                },
                () => calls.Add("reset"),
                () =>
                {
                    calls.Add("metrics");
                    return Task.CompletedTask;
                },
                status => calls.Add($"status:{status}"));

        var ignored = await service.SelectAsync(
            new OverviewModelSelectionApplicationRequest(null, IsLoaded: false, IsActive: false),
            Actions());
        var stopped = await service.SelectAsync(
            new OverviewModelSelectionApplicationRequest(model, IsLoaded: false, IsActive: false),
            Actions());
        var active = await service.SelectAsync(
            new OverviewModelSelectionApplicationRequest(model, IsLoaded: true, IsActive: true),
            Actions());
        var switched = await service.SelectAsync(
            new OverviewModelSelectionApplicationRequest(model, IsLoaded: true, IsActive: false),
            Actions());
        var staleLoaded = await service.SelectAsync(
            new OverviewModelSelectionApplicationRequest(model, IsLoaded: true, IsActive: false),
            Actions(selectSucceeds: false));

        Assert.Equal(OverviewModelSelectionOutcome.Ignored, ignored);
        Assert.Equal(OverviewModelSelectionOutcome.NotLoaded, stopped);
        Assert.Equal(OverviewModelSelectionOutcome.ActiveLoaded, active);
        Assert.Equal(OverviewModelSelectionOutcome.SwitchedLoaded, switched);
        Assert.Equal(OverviewModelSelectionOutcome.NotLoaded, staleLoaded);
        Assert.Equal([
            "reset",
            "status:Qwen is not loaded. Load it to expose an OpenAI-compatible endpoint.",
            "metrics",
            "reset",
            "metrics",
            $"select:{model.Id}",
            "active:8084",
            "save",
            "reset",
            "metrics",
            $"select:{model.Id}",
            "status:Selected model is no longer loaded.",
            "reset",
            "metrics"
        ], calls);
    }


    [Fact]
    public async Task OverviewLoadedSessionSelectionApplicationServiceOwnsModelLookupRefreshAndRuntimeSelection()
    {
        var root = CreateTempRoot();
        var model = new ModelRecord(
            "model-1",
            "Qwen",
            Path.Combine(root, "models", "qwen.gguf"),
            OwnershipKind.External,
            "{}",
            DateTimeOffset.UtcNow);
        var activeSettings = AppSettings.CreateDefault(root) with { Port = 8085 };
        var service = new OverviewLoadedSessionSelectionApplicationService();
        var calls = new List<string>();
        var knownModels = new List<ModelRecord>();
        var selectSucceeds = true;

        var actions = new OverviewLoadedSessionSelectionApplicationActions(
            modelId =>
            {
                calls.Add($"find:{modelId}");
                return knownModels.FirstOrDefault(item => string.Equals(item.Id, modelId, StringComparison.OrdinalIgnoreCase));
            },
            () =>
            {
                calls.Add("refresh-selector");
                knownModels.Add(model);
                return Task.CompletedTask;
            },
            modelId => calls.Add($"select-ui:{modelId}"),
            modelId =>
            {
                calls.Add($"select-runtime:{modelId}");
                return new RuntimeSessionSelectResult(selectSucceeds, selectSucceeds ? activeSettings : null);
            },
            settings => calls.Add($"active:{settings?.Port}"),
            () => calls.Add("reset"),
            () =>
            {
                calls.Add("save");
                return Task.CompletedTask;
            },
            () =>
            {
                calls.Add("metrics");
                return Task.CompletedTask;
            },
            () => calls.Add("actions"),
            status => calls.Add($"status:{status}"));

        var ignored = await service.SelectAsync("", actions);
        var selectedAfterRefresh = await service.SelectAsync(model.Id, actions);
        knownModels.Clear();
        selectSucceeds = false;
        var stale = await service.SelectAsync(model.Id, actions);

        Assert.Equal(OverviewLoadedSessionSelectionOutcome.Ignored, ignored);
        Assert.Equal(OverviewLoadedSessionSelectionOutcome.Selected, selectedAfterRefresh);
        Assert.Equal(OverviewLoadedSessionSelectionOutcome.Stale, stale);
        Assert.Equal([
            $"find:{model.Id}",
            "refresh-selector",
            $"find:{model.Id}",
            $"select-ui:{model.Id}",
            $"select-runtime:{model.Id}",
            "active:8085",
            "reset",
            "save",
            "metrics",
            "actions",
            "status:Selected loaded model Qwen.",
            $"find:{model.Id}",
            "refresh-selector",
            $"find:{model.Id}",
            $"select-ui:{model.Id}",
            $"select-runtime:{model.Id}",
            "status:Selected session is no longer loaded."
        ], calls);
    }


    [Fact]
    public async Task RuntimeMetricPollerServicePollsMetricsAndSlotsForRunningSessions()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { Port = 8081, EnableMetrics = true };
        var session = RuntimeMetricSession(root, settings);
        var paths = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var handler = new CapturingHttpHandler(request =>
        {
            paths.Enqueue(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath switch
            {
                "/metrics" => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    llama_tokens_predicted_total 42
                    llama_prompt_tokens_total 9
                    """)
                },
                "/slots" => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"is_processing":true,"n_prompt_tokens_processed":9,"n_decoded":4,"n_prompt_tokens":12,"n_ctx":4096}]""")
                },
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            };
        });
        using var http = new HttpClient(handler);
        var service = new RuntimeMetricPollerService(http);

        var results = await service.PollSessionsAsync([session], TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("model-1|runtime-1|8081", result.RuntimeKey);
        Assert.Empty(result.Error);
        Assert.Contains(result.Samples, sample => sample.Name == "llama_tokens_predicted_total" && sample.Value == 42);
        Assert.Equal(9, result.SlotSnapshot?.PromptTokensProcessed);
        Assert.Equal(4, result.SlotSnapshot?.GeneratedTokens);
        Assert.Contains("/metrics", paths);
        Assert.Contains("/slots", paths);
    }


    [Fact]
    public async Task RuntimeMetricPollerServiceSkipsMetricsWhenDisabledButKeepsSlots()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { Port = 8081, EnableMetrics = false };
        var session = RuntimeMetricSession(root, settings);
        var paths = new List<string>();
        using var handler = new CapturingHttpHandler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath == "/slots"
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"is_processing":false,"n_prompt_tokens_processed":5,"n_decoded":2}]""")
                }
                : new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        });
        using var http = new HttpClient(handler);
        var service = new RuntimeMetricPollerService(http);

        var result = Assert.Single(await service.PollSessionsAsync([session], TestContext.Current.CancellationToken));

        Assert.Empty(result.Samples);
        Assert.Empty(result.Error);
        Assert.Equal(5, result.SlotSnapshot?.PromptTokensProcessed);
        Assert.Equal(["/slots"], paths);
    }


    [Fact]
    public async Task RuntimeMetricPollerServiceReturnsErrorWhenMetricsFail()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { Port = 8081, EnableMetrics = true };
        var session = RuntimeMetricSession(root, settings);
        using var handler = new CapturingHttpHandler(request =>
            request.RequestUri!.AbsolutePath == "/slots"
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") }
                : new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
        using var http = new HttpClient(handler);
        var service = new RuntimeMetricPollerService(http);

        var result = Assert.Single(await service.PollSessionsAsync([session], TestContext.Current.CancellationToken));

        Assert.Empty(result.Samples);
        Assert.Contains("503", result.Error, StringComparison.Ordinal);
        Assert.NotNull(result.SlotSnapshot);
    }


}
