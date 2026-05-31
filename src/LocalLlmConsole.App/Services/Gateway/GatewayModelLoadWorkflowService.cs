namespace LocalLlmConsole.Services;

public delegate Task GatewayModelStopper(ModelRecord model, CancellationToken cancellationToken);

public delegate Task GatewayModelStarter(
    RuntimeRecord runtime,
    ModelRecord model,
    AppSettings launchSettings,
    CancellationToken cancellationToken);

public delegate Task<bool> GatewayEndpointProbe(AppSettings launchSettings, CancellationToken cancellationToken);

public delegate Task<LoadedModelSessionSnapshot?> GatewayReadyMarker(
    ModelRecord model,
    AppSettings launchSettings,
    CancellationToken cancellationToken);

public sealed record GatewayModelLoadWorkflowRequest(
    ModelRecord Model,
    ModelGatewaySwapPolicy Policy,
    AppSettings Settings,
    GatewayModelStopper StopModelAsync,
    GatewayModelStarter StartModelAsync,
    GatewayEndpointProbe EndpointAliveAsync,
    GatewayReadyMarker MarkReadyAsync,
    Action<string>? ReportPhase = null,
    TimeSpan? ReadyTimeout = null,
    TimeSpan? PollInterval = null);

public sealed record GatewayModelLoadWorkflowResult(
    LoadedModelSessionSnapshot Session,
    ModelLaunchSettings Profile,
    RuntimeRecord Runtime,
    AppSettings LaunchSettings);

public sealed class GatewayModelLoadException : InvalidOperationException
{
    public GatewayModelLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class GatewayModelLoadWorkflowService
{
    private static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);

    private readonly StateStore _stateStore;
    private readonly ModelLaunchProfileService _launchProfiles;
    private readonly RuntimeSessionCoordinator _runtimeSessions;

    public GatewayModelLoadWorkflowService(
        StateStore stateStore,
        ModelLaunchProfileService launchProfiles,
        RuntimeSessionCoordinator runtimeSessions)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _launchProfiles = launchProfiles ?? throw new ArgumentNullException(nameof(launchProfiles));
        _runtimeSessions = runtimeSessions ?? throw new ArgumentNullException(nameof(runtimeSessions));
    }

    public async Task<GatewayModelLoadWorkflowResult> EnsureLoadedAsync(
        GatewayModelLoadWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StopModelAsync);
        ArgumentNullException.ThrowIfNull(request.StartModelAsync);
        ArgumentNullException.ThrowIfNull(request.EndpointAliveAsync);
        ArgumentNullException.ThrowIfNull(request.MarkReadyAsync);
        cancellationToken.ThrowIfCancellationRequested();

        var loaded = _runtimeSessions.Sessions.SessionForModel(request.Model.Id);
        if (loaded is { IsRunning: true })
        {
            return new GatewayModelLoadWorkflowResult(
                loaded,
                await _launchProfiles.EnsureAsync(request.Model, request.Settings) ?? ModelLaunchSettings.FromAppSettings(request.Settings),
                ResolveRuntime(await _stateStore.ListRuntimesAsync(), loaded.RuntimeId)
                    ?? new RuntimeRecord(loaded.RuntimeId, loaded.RuntimeName, loaded.Mode, loaded.Backend, "", "{}", DateTimeOffset.UtcNow),
                loaded.LaunchSettings);
        }

        ModelLaunchSettings? profile = null;
        RuntimeRecord? runtime = null;
        AppSettings? launchSettings = null;
        try
        {
            if (request.Policy == ModelGatewaySwapPolicy.SingleActive)
                await StopOtherRunningModelsAsync(request, cancellationToken);

            request.ReportPhase?.Invoke("preparing");
            profile = await EnsureLaunchProfileAsync(request.Model, request.Settings, cancellationToken);
            launchSettings = profile.ApplyTo(request.Settings);
            runtime = ResolveRuntime(await _stateStore.ListRuntimesAsync(), profile.RuntimeId)
                ?? throw new InvalidOperationException(string.IsNullOrWhiteSpace(profile.RuntimeId)
                    ? "Register a llama.cpp runtime before auto-loading models."
                    : $"Saved runtime '{profile.RuntimeId}' is missing.");

            request.ReportPhase?.Invoke("starting");
            await request.StartModelAsync(runtime, request.Model, launchSettings, cancellationToken);
            request.ReportPhase?.Invoke("waiting for API from");

            var ready = await WaitForReadyAsync(request, launchSettings, cancellationToken);
            return new GatewayModelLoadWorkflowResult(ready, profile, runtime, launchSettings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not GatewayModelLoadException)
        {
            throw new GatewayModelLoadException(
                FailureMessage(
                    request.Model,
                    profile,
                    runtime,
                    _runtimeSessions.Sessions.SessionForModel(request.Model.Id)?.LogPath ?? "",
                    ex,
                    request.Policy,
                    _runtimeSessions.Sessions.Snapshots()
                        .Where(session => session.IsRunning && !string.Equals(session.ModelId, request.Model.Id, StringComparison.OrdinalIgnoreCase))
                        .ToArray()),
                ex);
        }
    }

    private async Task StopOtherRunningModelsAsync(GatewayModelLoadWorkflowRequest request, CancellationToken cancellationToken)
    {
        var runningSessions = _runtimeSessions.Sessions.Snapshots()
            .Where(session => session.IsRunning && !string.Equals(session.ModelId, request.Model.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var session in runningSessions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request.ReportPhase?.Invoke($"freeing VRAM from {session.ModelName}; loading");
            var loadedModel = await FindModelByIdAsync(session.ModelId);
            if (loadedModel is not null)
                await request.StopModelAsync(loadedModel, cancellationToken);
        }
    }

    private async Task<ModelLaunchSettings> EnsureLaunchProfileAsync(
        ModelRecord model,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = await _launchProfiles.EnsureAsync(model, settings)
            ?? throw new InvalidOperationException($"Could not create a launch profile for {model.Name}.");
        cancellationToken.ThrowIfCancellationRequested();

        if (settings.AutoLoadGatewayEnabled && profile.Port == settings.AutoLoadGatewayPort)
        {
            profile = profile with { Port = await _launchProfiles.NextAvailablePortAsync(model.Id, settings) };
            await _launchProfiles.SaveAsync(model, profile);
        }

        return profile;
    }

    private async Task<LoadedModelSessionSnapshot> WaitForReadyAsync(
        GatewayModelLoadWorkflowRequest request,
        AppSettings launchSettings,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + (request.ReadyTimeout ?? DefaultReadyTimeout);
        var pollInterval = request.PollInterval ?? DefaultPollInterval;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var session = _runtimeSessions.Sessions.SessionForModel(request.Model.Id);
            if (session is not { IsRunning: true })
                throw new InvalidOperationException($"{request.Model.Name} stopped while loading.");

            if (await request.EndpointAliveAsync(launchSettings, cancellationToken))
            {
                var ready = await request.MarkReadyAsync(request.Model, launchSettings, cancellationToken);
                return ready
                    ?? _runtimeSessions.Sessions.SessionForModel(request.Model.Id)
                    ?? throw new InvalidOperationException($"{request.Model.Name} was ready but no session snapshot was available.");
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for {request.Model.Name} to become ready.");
    }

    private async Task<ModelRecord?> FindModelByIdAsync(string modelId)
        => (await _stateStore.ListModelsAsync())
            .FirstOrDefault(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase));

    private static RuntimeRecord? ResolveRuntime(IReadOnlyList<RuntimeRecord> runtimes, string? runtimeId)
    {
        if (!string.IsNullOrWhiteSpace(runtimeId))
            return runtimes.FirstOrDefault(runtime => string.Equals(runtime.Id, runtimeId, StringComparison.OrdinalIgnoreCase));
        return runtimes.FirstOrDefault();
    }

    public static string FailureMessage(
        ModelRecord model,
        ModelLaunchSettings? profile,
        RuntimeRecord? runtime,
        string logPath,
        Exception exception,
        ModelGatewaySwapPolicy? policy = null,
        IReadOnlyList<LoadedModelSessionSnapshot>? runningSessions = null)
    {
        var original = InnermostMessage(exception);
        var profileText = profile is null
            ? "No launch profile was available."
            : $"Profile port {profile.Port}, context {profile.ContextSize}, GPU layers {profile.GpuLayers}.";
        var runtimeText = runtime is null ? "No runtime was resolved." : $"Runtime: {runtime.Name}.";
        var policyText = FailurePolicyHint(policy, runningSessions);
        var logText = string.IsNullOrWhiteSpace(logPath) ? "" : $" Runtime log: {logPath}.";
        return $"Gateway could not auto-load {model.Name}. {FailureAction(original)} {policyText} {runtimeText} {profileText} Details: {original}.{logText}".Trim();
    }

    private static string FailurePolicyHint(ModelGatewaySwapPolicy? policy, IReadOnlyList<LoadedModelSessionSnapshot>? runningSessions)
    {
        if (policy != ModelGatewaySwapPolicy.KeepLoaded || runningSessions is null || runningSessions.Count == 0)
            return "";

        var names = string.Join(", ", runningSessions.Take(3).Select(session => session.ModelName));
        var suffix = runningSessions.Count > 3 ? ", ..." : "";
        return $"Gateway policy is keeping {runningSessions.Count.ToString(CultureInfo.InvariantCulture)} existing model(s) loaded ({names}{suffix}); switch to Single active model if you want the router to unload them before loading the requested model.";
    }

    public static string FailureAction(string message)
    {
        if (message.Contains("VRAM", StringComparison.OrdinalIgnoreCase)
            || message.Contains("free", StringComparison.OrdinalIgnoreCase)
            || message.Contains("memory", StringComparison.OrdinalIgnoreCase))
            return "Not enough GPU memory was available for the saved launch profile. Unload other models, lower context/GPU layers, or use a smaller/runtime profile.";
        if (message.Contains("runtime", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("missing", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Register", StringComparison.OrdinalIgnoreCase)))
            return "Install or register a runtime, then save the model launch profile again.";
        if (message.Contains("port", StringComparison.OrdinalIgnoreCase))
            return "The model's direct API port conflicts with another listener. Save a different per-model port.";
        if (message.Contains("Timed out", StringComparison.OrdinalIgnoreCase)
            || message.Contains("stopped while loading", StringComparison.OrdinalIgnoreCase))
            return "The runtime process did not become ready. Check the runtime log and reduce the launch settings if it exited during startup.";
        return "Review the saved launch profile and runtime log, then try the request again.";
    }

    private static string InnermostMessage(Exception ex)
    {
        while (ex.InnerException is not null)
            ex = ex.InnerException;
        return string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
    }
}
