using System.Diagnostics;
using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
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
        Assert.Equal("GPU 0: 76% | 62C | 12.0/24.0 GiB", GpuStatusService.NormalizeMetricSeparators("GPU 0: 76%|62C|12.0/24.0 GiB"));
    }


    [Fact]
    public void GpuStatusServiceFormatsIntelArcSyclLine()
    {
        var formatted = GpuStatusService.FormatIntelArcStatus("[level_zero:gpu][level_zero:0] Intel(R) Arc(TM) A770 Graphics");

        Assert.Equal("Intel(R) Arc(TM) A770 Graphics", formatted);
        Assert.Equal("Intel Arc GPU", GpuStatusService.FormatIntelArcStatus(""));
    }


    [Fact]
    public async Task GpuStatusProbeServiceRunsThroughProcessRunner()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "GpuStatusService.cs"));
        var commands = new List<string>();
        var runner = new ScriptedProcessRunner(psi =>
        {
            commands.Add($"{Path.GetFileName(psi.FileName)} {string.Join(" ", psi.ArgumentList)}");
            if (psi.ArgumentList.Contains("--query-gpu=memory.free,memory.total"))
                return new ProcessRunResult(0, "1024, 24576\n8192, 24576", "");
            if (psi.ArgumentList.Contains("--query-gpu=index,name,utilization.gpu,temperature.gpu,memory.used,memory.total"))
                return new ProcessRunResult(0, "0, NVIDIA RTX, 76, 62, 12288, 24576", "");
            return new ProcessRunResult(0, "[level_zero:gpu][level_zero:0] Intel(R) Arc(TM) A770 Graphics", "");
        });
        var service = new GpuStatusProbeService(runner, () => "sycl-ls.exe");

        var memory = await service.MemoryAsync(TestContext.Current.CancellationToken);
        var summary = await service.SummaryAsync(TestContext.Current.CancellationToken);
        var sycl = await service.WindowsIntelArcSummaryAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(memory);
        Assert.Equal(8, memory.FreeGiB);
        Assert.Equal(24, memory.TotalGiB);
        Assert.Equal("GPU 0: 76% | 62C | 12.0/24.0 GiB", summary);
        Assert.Equal("Intel(R) Arc(TM) A770 Graphics", sycl);
        Assert.Contains(commands, command => command.Contains("--query-gpu=memory.free,memory.total", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--query-gpu=index,name,utilization.gpu,temperature.gpu,memory.used,memory.total", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.StartsWith("sycl-ls", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Process.Start(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new GpuStatusProbeService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new TrackedProcessRunner", source, StringComparison.Ordinal);
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
        var lan = local with { ModelAccessMode = "models", Host = "192.168.1.20" };
        var gateway = local with { ModelAccessMode = "gateway", Host = "127.0.0.1" };

        Assert.Equal("http://127.0.0.1:8081", RuntimeEndpointService.LocalServerBaseUrl(local));
        Assert.Equal("http://127.0.0.1:8081/v1", RuntimeEndpointService.LocalOpenAiBaseUrl(local));
        Assert.Equal("http://127.0.0.1:8082/v1", RuntimeEndpointService.LocalGatewayOpenAiBaseUrl(local));
        Assert.Equal("http://192.168.1.20:8081/v1", RuntimeEndpointService.LanOpenAiBaseUrl(lan));
        Assert.Equal("http://192.168.1.20:8081/v1", RuntimeEndpointService.EndpointDisplay(lan));
        Assert.Equal("http://127.0.0.1:8081/v1", RuntimeEndpointService.EndpointDisplay(gateway));
        Assert.Contains("LAN:", RuntimeEndpointService.GatewayEndpointDisplay(gateway), StringComparison.Ordinal);
        Assert.Equal("[::1]", RuntimeEndpointService.UrlHost("::1"));
    }


    [Fact]
    public void ModelGatewayOptionsFollowAppSettings()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 9091,
            AutoLoadGatewayPolicy = "Single active model",
            ModelAccessMode = "gateway",
            ModelApiKey = new string('a', 32)
        };

        var options = ModelGatewayOptions.FromSettings(settings);

        Assert.True(options.Enabled);
        Assert.True(options.AllowLanAccess);
        Assert.Equal(9091, options.Port);
        Assert.Equal("http://+:9091/", options.ListenerPrefix);
        Assert.Equal(ModelGatewaySwapPolicy.SingleActive, options.SwapPolicy);
    }


    [Fact]
    public void ModelGatewayHostFactoryServiceOwnsGatewayHostAndControllerCreation()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8091
        };
        var calls = new List<string>();
        var expectedController = new FakeModelGatewayRuntimeController();
        var expectedHost = new FakeModelGatewayHost();
        var service = new ModelGatewayHostFactoryService(
            actions =>
            {
                calls.Add("controller");
                Assert.NotNull(actions.ListModelsAsync);
                Assert.NotNull(actions.RunningSessionsAsync);
                Assert.NotNull(actions.EnsureModelLoadedAsync);
                return expectedController;
            },
            (options, runtime) =>
            {
                calls.Add($"host:{options.Port}");
                Assert.Same(expectedController, runtime);
                return expectedHost;
            });
        var actions = new ModelGatewayRuntimeControllerActions(
            _ => Task.FromResult<IReadOnlyList<ModelRecord>>([]),
            _ => Task.FromResult<IReadOnlyList<LoadedModelSessionSnapshot>>([]),
            (_, _, _) => Task.FromException<LoadedModelSessionSnapshot>(new NotSupportedException()));

        var controller = service.CreateRuntimeController(actions);
        var host = service.CreateGatewayHost(ModelGatewayOptions.FromSettings(settings), controller);

        Assert.Same(expectedController, controller);
        Assert.Same(expectedHost, host);
        Assert.Equal(["controller", "host:8091"], calls);
        Assert.Throws<ArgumentNullException>(() => service.CreateRuntimeController(null!));
        Assert.Throws<ArgumentNullException>(() => service.CreateGatewayHost(null!, controller));
        Assert.Throws<ArgumentNullException>(() => service.CreateGatewayHost(ModelGatewayOptions.FromSettings(settings), null!));
    }


    [Fact]
    public void ModelGatewayExtractsAndResolvesRequestedModels()
    {
        var root = CreateTempRoot();
        var now = DateTimeOffset.UtcNow;
        var model = new ModelRecord("qwen-id", "Friendly Qwen", Path.Combine(root, "models", "Qwen3-8B.gguf"), OwnershipKind.External, "{}", now);
        var body = System.Text.Encoding.UTF8.GetBytes("""{"model":"Friendly Qwen","messages":[]}""");

        Assert.Equal("Friendly Qwen", ModelGatewayRequestResolver.ExtractRequestedModel(body));
        Assert.Equal(model, ModelGatewayRequestResolver.ResolveModel([model], "qwen-id"));
        Assert.Equal(model, ModelGatewayRequestResolver.ResolveModel([model], "Friendly Qwen"));
        Assert.Equal(model, ModelGatewayRequestResolver.ResolveModel([model], OpenCodeConfigService.LocalModelIdFor(model)));
        Assert.Equal(model, ModelGatewayRequestResolver.ResolveModel([model], "Qwen3-8B.gguf"));
        Assert.Equal(model, ModelGatewayRequestResolver.ResolveModel([model], "Qwen3-8B"));
        Assert.Null(ModelGatewayRequestResolver.ResolveModel([model], "other"));
    }

    [Fact]
    public void ModelGatewayRequestBodyReaderRejectsOversizedBodies()
    {
        var small = System.Text.Encoding.UTF8.GetBytes("""{"model":"qwen"}""");
        var tooLarge = System.Text.Encoding.UTF8.GetBytes("""{"model":"qwen","messages":["0123456789"]}""");
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "Gateway", "ModelGatewayService.cs"));

        var read = ModelGatewayRequestBodyReader.ReadBodyBuffer(new MemoryStream(small), small.Length, small.Length);
        var declared = Assert.Throws<ModelGatewayRequestBodyTooLargeException>(() =>
            ModelGatewayRequestBodyReader.ReadBodyBuffer(new MemoryStream(small), small.Length + 1, small.Length));
        var streamed = Assert.Throws<ModelGatewayRequestBodyTooLargeException>(() =>
            ModelGatewayRequestBodyReader.ReadBodyBuffer(new MemoryStream(tooLarge), -1, small.Length));

        Assert.Equal(small, read);
        Assert.Contains("too large", declared.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("too large", streamed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("413", source, StringComparison.Ordinal);
        Assert.Contains("request_too_large", source, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodyBytes", source, StringComparison.Ordinal);
    }


    [Fact]
    public void ModelGatewayReturnsActionableLoadAndProxyErrors()
    {
        var source = string.Concat(
            File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelGatewayService.cs")),
            File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelGatewayResponseWriter.cs")));
        var workflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "GatewayModelLoadWorkflowService.cs"));

        Assert.Contains("\"model_load_failed\"", source, StringComparison.Ordinal);
        Assert.Contains("\"upstream_unavailable\"", source, StringComparison.Ordinal);
        Assert.Contains("Auto-load gateway could not load", source, StringComparison.Ordinal);
        Assert.Contains("direct endpoint", source, StringComparison.Ordinal);
        Assert.Contains("Gateway could not auto-load", workflow, StringComparison.Ordinal);
        Assert.Contains("Install or register a runtime", workflow, StringComparison.Ordinal);
    }


    [Fact]
    public void GatewayActivityStatusTrackerOwnsGatewayStatusText()
    {
        var tracker = new GatewayActivityStatusTracker();
        var settings = AppSettings.CreateDefault(CreateTempRoot()) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082,
            AutoLoadGatewayPolicy = "singleActive"
        };
        var model = new ModelRecord("model", "Qwen", "qwen.gguf", OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;

        var disabled = tracker.Build(settings with { AutoLoadGatewayEnabled = false }, gatewayListening: false, now);
        var listening = tracker.Build(settings, gatewayListening: true, now);
        tracker.Start(model, "switching to", now);
        var activity = tracker.Build(settings, gatewayListening: true, now + TimeSpan.FromSeconds(5));
        tracker.SetPhase("loading");
        var loading = tracker.Build(settings, gatewayListening: true, now + TimeSpan.FromSeconds(6));
        tracker.Fail("not enough VRAM");
        var failed = tracker.Build(settings, gatewayListening: true, now + TimeSpan.FromSeconds(7));
        tracker.Complete();
        var completed = tracker.Build(settings, gatewayListening: false, now + TimeSpan.FromSeconds(8));

        Assert.Contains("disabled", disabled.Line, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GatewayStatusVisualKind.Normal, disabled.VisualKind);
        Assert.Contains("listening at http://127.0.0.1:8082", listening.Line, StringComparison.Ordinal);
        Assert.Contains("Single active model", listening.Line, StringComparison.Ordinal);
        Assert.Equal(GatewayStatusVisualKind.Activity, activity.VisualKind);
        Assert.Contains("switching to Qwen", activity.Line, StringComparison.Ordinal);
        Assert.Contains("loading Qwen", loading.Line, StringComparison.Ordinal);
        Assert.Equal(GatewayStatusVisualKind.Warning, failed.VisualKind);
        Assert.Contains("not enough VRAM", failed.Line, StringComparison.Ordinal);
        Assert.Equal(GatewayStatusVisualKind.Normal, completed.VisualKind);
        Assert.Contains("not listening", completed.Line, StringComparison.Ordinal);
    }


    [Fact]
    public void GatewayActivityStatusControllerOwnsActivityTimer()
    {
        var source = ReadMainWindowSources();
        var state = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.State.cs"));
        var controllerSource = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "GatewayActivityStatusController.cs"));
        var factorySource = ReadAppServiceFactorySources();
        var timerFactory = new ManualUiTimerFactory();
        var controller = new GatewayActivityStatusController(new GatewayActivityStatusTracker(), timerFactory);
        var settings = AppSettings.CreateDefault(CreateTempRoot()) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082
        };
        var model = new ModelRecord("model", "Qwen", "qwen.gguf", OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var tickCount = 0;

        controller.Start(model, "switching to", DateTimeOffset.UtcNow, () => tickCount++);

        Assert.True(controller.HasActivityTimer);
        Assert.Equal(1, tickCount);
        Assert.Single(timerFactory.Timers);
        Assert.Equal(TimeSpan.FromSeconds(1), timerFactory.Timers[0].Interval);
        Assert.True(timerFactory.Timers[0].Started);
        Assert.Contains("switching to Qwen", controller.Build(settings, gatewayListening: true, DateTimeOffset.UtcNow).Line, StringComparison.Ordinal);

        timerFactory.Timers[0].Fire();
        Assert.Equal(2, tickCount);

        controller.SetPhase("loading", () => tickCount++);
        Assert.Equal(3, tickCount);
        Assert.Contains("loading Qwen", controller.Build(settings, gatewayListening: true, DateTimeOffset.UtcNow).Line, StringComparison.Ordinal);

        controller.Fail("not enough VRAM", () => tickCount++);
        Assert.False(controller.HasActivityTimer);
        Assert.False(timerFactory.Timers[0].Started);
        Assert.Equal(4, tickCount);
        Assert.Contains("not enough VRAM", controller.Build(settings, gatewayListening: true, DateTimeOffset.UtcNow).Line, StringComparison.Ordinal);

        controller.Start(model, "loading", DateTimeOffset.UtcNow, () => tickCount++);
        Assert.True(controller.HasActivityTimer);
        Assert.Equal(2, timerFactory.Timers.Count);
        controller.Complete(() => tickCount++);
        Assert.False(controller.HasActivityTimer);
        Assert.False(timerFactory.Timers[1].Started);
        Assert.Contains("listening", controller.Build(settings, gatewayListening: true, DateTimeOffset.UtcNow).Line, StringComparison.Ordinal);
        Assert.Contains("DispatcherUiTimerFactory", factorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherUiTimerFactory", controllerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new GatewayActivityStatusController()", state, StringComparison.Ordinal);
        Assert.DoesNotContain("_gatewayActivity = _coreServices.GatewayActivity", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.GatewayActivity.Start(model, phase, DateTimeOffset.Now, UpdateGatewayStatusText)", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.GatewayActivity.Complete(UpdateGatewayStatusText)", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.GatewayActivity.Fail(message, UpdateGatewayStatusText)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_coreServices.Ui.GatewayActivityTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GatewayActivityTimer_Tick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StopGatewayActivityTimer", source, StringComparison.Ordinal);
    }


    [Fact]
    public async Task GatewayModelLoadWorkflowStopsConflictingSessionsFixesGatewayPortAndWaitsForReady()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var settings = AppSettings.CreateDefault(root) with
        {
            Port = 8081,
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082
        };
        var runtime = new RuntimeRecord("runtime", "CUDA", RuntimeMode.Native, RuntimeBackend.Cuda, Path.Combine(root, "llama-server.exe"), "{}", DateTimeOffset.UtcNow);
        var target = new ModelRecord("target", "Target Model", Path.Combine(root, "target.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var loaded = new ModelRecord("loaded", "Loaded Model", Path.Combine(root, "loaded.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var profiled = new ModelRecord("profiled", "Profiled Model", Path.Combine(root, "profiled.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        await store.UpsertRuntimeAsync(runtime);
        await store.UpsertModelAsync(target);
        await store.UpsertModelAsync(loaded);
        await store.UpsertModelAsync(profiled);
        await store.SaveModelLaunchSettingsAsync(target.Id, ModelLaunchSettings.FromAppSettings(settings with { Port = 8082 }, runtime.Id));
        await store.SaveModelLaunchSettingsAsync(profiled.Id, ModelLaunchSettings.FromAppSettings(settings with { Port = 8081 }, runtime.Id));
        await store.SaveModelLaunchSettingsAsync(loaded.Id, ModelLaunchSettings.FromAppSettings(settings with { Port = 8083 }, runtime.Id));
        using var sessions = CreateLoadedModelSessionManager();
        sessions.AttachExisting(runtime, loaded, settings with { Port = 8083 }, Path.Combine(root, "loaded.log"), LlamaRuntimeState.Loaded, "", "loaded-session", DateTimeOffset.UtcNow);
        var runtimeSessions = new RuntimeSessionCoordinator(sessions, Path.Combine(root, "logs"));
        var profiles = new ModelLaunchProfileService(store, sessions);
        var workflow = new GatewayModelLoadWorkflowService(store, profiles, runtimeSessions);
        var phases = new List<string>();
        var stopped = new List<string>();
        AppSettings? startedSettings = null;

        var result = await workflow.EnsureLoadedAsync(new GatewayModelLoadWorkflowRequest(
            target,
            ModelGatewaySwapPolicy.SingleActive,
            settings,
            async (model, _) =>
            {
                stopped.Add(model.Id);
                await runtimeSessions.StopModelAsync(model.Id);
            },
            (startedRuntime, model, launchSettings, _) =>
            {
                startedSettings = launchSettings;
                sessions.AttachExisting(startedRuntime, model, launchSettings, Path.Combine(root, "target.log"), LlamaRuntimeState.Loading, "", "target-session", DateTimeOffset.UtcNow);
                return Task.CompletedTask;
            },
            (_, _) => Task.FromResult(true),
            (model, _, _) =>
            {
                sessions.MarkModelLoadedIfRunning(model.Id);
                return Task.FromResult(sessions.SessionForModel(model.Id));
            },
            phases.Add,
            ReadyTimeout: TimeSpan.FromSeconds(1),
            PollInterval: TimeSpan.FromMilliseconds(1)),
            TestContext.Current.CancellationToken);

        var savedTargetProfile = await store.GetModelLaunchSettingsAsync(target.Id);
        Assert.Equal([loaded.Id], stopped);
        Assert.Equal(8084, savedTargetProfile?.Port);
        Assert.Equal(8084, startedSettings?.Port);
        Assert.Equal(target.Id, result.Session.ModelId);
        Assert.Equal(LoadedModelSessionStatus.Running, result.Session.Status);
        Assert.Contains(phases, phase => phase.Contains("freeing VRAM", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("preparing", phases);
        Assert.Contains("starting", phases);
        Assert.Contains("waiting for API from", phases);
    }


    [Fact]
    public async Task GatewayRuntimeApplicationServiceOwnsActivityRefreshAndErrorBoundary()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var settings = AppSettings.CreateDefault(root) with
        {
            Port = 8084,
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082
        };
        var runtime = new RuntimeRecord("runtime", "CUDA", RuntimeMode.Native, RuntimeBackend.Cuda, Path.Combine(root, "llama-server.exe"), "{}", DateTimeOffset.UtcNow);
        var model = new ModelRecord("target", "Target Model", Path.Combine(root, "target.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        await store.UpsertRuntimeAsync(runtime);
        await store.UpsertModelAsync(model);
        await store.SaveModelLaunchSettingsAsync(model.Id, ModelLaunchSettings.FromAppSettings(settings, runtime.Id));
        using var sessions = CreateLoadedModelSessionManager();
        var runtimeSessions = new RuntimeSessionCoordinator(sessions, Path.Combine(root, "logs"));
        var application = new GatewayRuntimeApplicationService(new GatewayModelLoadWorkflowService(
            store,
            new ModelLaunchProfileService(store, sessions),
            runtimeSessions));
        var calls = new List<string>();

        var result = await application.EnsureModelLoadedAsync(
            new GatewayRuntimeLoadApplicationRequest(
                model,
                ModelGatewaySwapPolicy.KeepLoaded,
                settings,
                ExistingSession: null),
            new GatewayRuntimeLoadApplicationActions(
                (_, _) => throw new InvalidOperationException("Keep-loaded policy should not stop models."),
                (startedRuntime, runtimeModel, launchSettings, _) =>
                {
                    calls.Add($"start:{runtimeModel.Id}:{launchSettings.Port}");
                    sessions.AttachExisting(startedRuntime, runtimeModel, launchSettings, Path.Combine(root, "target.log"), LlamaRuntimeState.Loading, "", "target-session", DateTimeOffset.UtcNow);
                    return Task.CompletedTask;
                },
                (_, _) => Task.FromResult(true),
                (runtimeModel, _, _) =>
                {
                    calls.Add($"ready:{runtimeModel.Id}");
                    sessions.MarkModelLoadedIfRunning(runtimeModel.Id);
                    return Task.FromResult(sessions.SessionForModel(runtimeModel.Id));
                },
                (runtimeModel, phase) => calls.Add($"activity:{phase}:{runtimeModel.Id}"),
                phase => calls.Add($"phase:{phase}"),
                () => calls.Add("complete"),
                message => calls.Add($"fail:{message}"),
                () => { calls.Add("overview"); return Task.CompletedTask; },
                () => { calls.Add("metrics"); return Task.CompletedTask; },
                () => calls.Add("actions"),
                status => calls.Add($"status:{status}")),
            TestContext.Current.CancellationToken);

        Assert.Equal(model.Id, result.ModelId);
        Assert.Contains($"activity:switching to:{model.Id}", calls);
        Assert.Contains("status:Gateway auto-loading Target Model...", calls);
        Assert.Contains($"start:{model.Id}:8084", calls);
        Assert.Contains($"ready:{model.Id}", calls);
        Assert.Contains("status:Gateway loaded Target Model at http://127.0.0.1:8084/v1.", calls);
        Assert.Contains("complete", calls);
        Assert.Contains("overview", calls);
        Assert.Contains("metrics", calls);
        Assert.Contains("actions", calls);
        Assert.DoesNotContain(calls, call => call.StartsWith("fail:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ModelGatewayLifecycleApplicationServiceOwnsRestartAndFailureCleanup()
    {
        var root = CreateTempRoot();
        var service = new ModelGatewayLifecycleApplicationService();
        var apiKey = new string('a', 40);
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8099,
            ModelApiKey = apiKey
        };
        var existing = new FakeModelGatewayHost();
        var created = new List<FakeModelGatewayHost>();
        var calls = new List<string>();
        IModelGatewayHost? currentGateway = existing;

        var result = await service.RestartAsync(
            new ModelGatewayLifecycleRestartRequest(currentGateway, settings),
            Actions(
                gateway => currentGateway = gateway,
                _ => Task.FromResult(settings),
                (_, _) =>
                {
                    var gateway = new FakeModelGatewayHost();
                    created.Add(gateway);
                    return gateway;
                },
                calls),
            TestContext.Current.CancellationToken);

        Assert.True(existing.Disposed);
        var started = Assert.Single(created);
        Assert.True(started.Started);
        Assert.Same(started, currentGateway);
        Assert.True(result.GatewayStarted);
        Assert.Contains("Auto-load gateway listening at http://127.0.0.1:8099/v1.", calls);
        Assert.Contains("status", calls);

        var disabled = settings with { AutoLoadGatewayEnabled = false };
        calls.Clear();
        result = await service.RestartAsync(
            new ModelGatewayLifecycleRestartRequest(currentGateway, disabled),
            Actions(
                gateway => currentGateway = gateway,
                _ => throw new InvalidOperationException("Disabled gateway should not require an API key."),
                (_, _) => throw new InvalidOperationException("Disabled gateway should not create a host."),
                calls),
            TestContext.Current.CancellationToken);

        Assert.True(started.Disposed);
        Assert.Null(currentGateway);
        Assert.False(result.GatewayStarted);
        Assert.DoesNotContain("key", calls);
        Assert.DoesNotContain(calls, call => call.StartsWith("create:", StringComparison.Ordinal));
        Assert.Contains("status", calls);

        var stopOnlyGateway = new FakeModelGatewayHost();
        currentGateway = stopOnlyGateway;
        calls.Clear();
        var stopped = await service.StopAsync(
            new ModelGatewayLifecycleStopRequest(currentGateway),
            new ModelGatewayLifecycleStopActions(
                gateway =>
                {
                    calls.Add(gateway is null ? "gateway:null" : "gateway:set");
                    currentGateway = gateway;
                },
                () => calls.Add("status")));

        Assert.True(stopped);
        Assert.True(stopOnlyGateway.Disposed);
        Assert.Null(currentGateway);
        Assert.Equal(["gateway:null", "status"], calls);

        var failed = new FakeModelGatewayHost(new InvalidOperationException("port busy"));
        calls.Clear();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RestartAsync(
                new ModelGatewayLifecycleRestartRequest(null, settings),
                Actions(
                    gateway => currentGateway = gateway,
                    _ => Task.FromResult(settings),
                    (_, _) => failed,
                    calls),
                TestContext.Current.CancellationToken));

        Assert.Equal("port busy", failure.Message);
        Assert.True(failed.Disposed);
        Assert.Null(currentGateway);
        Assert.Contains("status", calls);

        ModelGatewayLifecycleActions Actions(
            Action<IModelGatewayHost?> setGateway,
            Func<AppSettings, Task<AppSettings>> ensureApiKey,
            Func<ModelGatewayOptions, IModelGatewayRuntimeController, IModelGatewayHost> createGateway,
            List<string> callLog)
            => new(
                gateway =>
                {
                    callLog.Add(gateway is null ? "gateway:null" : "gateway:set");
                    setGateway(gateway);
                },
                settings =>
                {
                    callLog.Add("key");
                    return ensureApiKey(settings);
                },
                () =>
                {
                    callLog.Add("controller");
                    return new FakeModelGatewayRuntimeController();
                },
                (options, controller) =>
                {
                    callLog.Add($"create:{options.Port}:{options.SwapPolicy}");
                    return createGateway(options, controller);
                },
                () => callLog.Add("status"),
                callLog.Add);
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
    public async Task RuntimeEndpointProbeServiceRequiresSuccessForAliveAndSendsAuth()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with
        {
            Port = 8081,
            ModelApiKey = "secret-token"
        };
        var requests = new List<HttpRequestMessage>();
        using var handler = new CapturingHttpHandler(request =>
        {
            requests.Add(CloneRequest(request));
            if (request.RequestUri?.AbsolutePath == "/health")
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            if (request.RequestUri?.AbsolutePath == "/v1/models")
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });
        using var http = new HttpClient(handler);
        var service = new RuntimeEndpointProbeService(http);

        var alive = await service.IsAliveAsync(settings, TestContext.Current.CancellationToken);

        Assert.True(alive);
        Assert.Equal(["/health", "/v1/models"], requests.Select(request => request.RequestUri!.AbsolutePath).ToArray());
        Assert.All(requests, request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("secret-token", request.Headers.Authorization?.Parameter);
        });
    }


    [Fact]
    public async Task RuntimeEndpointProbeServiceTreatsAnyHttpResponseAsResponding()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { Port = 8081 };
        var requests = new List<HttpRequestMessage>();
        using var handler = new CapturingHttpHandler(request =>
        {
            requests.Add(CloneRequest(request));
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        });
        using var http = new HttpClient(handler);
        var service = new RuntimeEndpointProbeService(http);

        var responding = await service.IsRespondingAsync(settings, TestContext.Current.CancellationToken);

        Assert.True(responding);
        Assert.Equal(["/health"], requests.Select(request => request.RequestUri!.AbsolutePath).ToArray());
    }


    [Fact]
    public async Task RuntimeEndpointProbeServiceReadsServedModelsAndFailsClosed()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { Port = 8081 };
        using var handler = new CapturingHttpHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/v1/models")
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[{"id":"qwen"}],"models":["llama"]}""")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });
        using var http = new HttpClient(handler);
        var service = new RuntimeEndpointProbeService(http);
        using var failingHandler = new CapturingHttpHandler(_ => throw new HttpRequestException("offline"));
        using var failingHttp = new HttpClient(failingHandler);
        var failingService = new RuntimeEndpointProbeService(failingHttp);

        var served = await service.ServedModelsAsync(settings, TestContext.Current.CancellationToken);
        var failed = await failingService.ServedModelsAsync(settings, TestContext.Current.CancellationToken);

        Assert.Equal(["qwen", "llama"], served);
        Assert.Empty(failed);
    }


    [Fact]
    public void MainWindowDelegatesRuntimeEndpointProbesToService()
    {
        var runtimeSession = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeSession.cs"));
        var runtimeLifecycle = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.ModelRuntimeLifecycle.cs"));
        var gateway = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.Gateway.cs"));

        Assert.Contains("_coreServices.Runtime.RuntimeEndpointProbe.ServedModelsAsync", runtimeSession, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeEndpointProbe.IsAliveAsync", runtimeSession, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeEndpointProbe.IsRespondingAsync", runtimeSession, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeEndpointProbe.IsRespondingAsync", runtimeLifecycle, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeEndpointProbe.IsAliveAsync", gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("new HttpClient", runtimeSession, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeEndpointAliveAsync", runtimeSession + runtimeLifecycle + gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeEndpointRespondingAsync", runtimeSession + runtimeLifecycle + gateway, StringComparison.Ordinal);
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


    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }

    private static LoadedModelSessionSnapshot RuntimeMetricSession(string root, AppSettings settings)
        => RuntimeSession(root, settings, LoadedModelSessionStatus.Running, isRunning: true);

    private static LoadedModelSessionSnapshot RuntimeSession(
        string root,
        AppSettings settings,
        LoadedModelSessionStatus status,
        bool isRunning)
        => new(
            "session-1",
            "model-1",
            "Qwen",
            "runtime-1",
            "Runtime",
            RuntimeMode.Native,
            RuntimeBackend.Cpu,
            settings,
            Path.Combine(root, "runtime.log"),
            DateTimeOffset.UtcNow,
            "",
            0,
            status,
            IsRunning: isRunning,
            IsSelected: true);


}
