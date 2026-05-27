namespace LocalLlmConsole.Services;

public sealed class LoadedModelSessionManager : IDisposable
{
    private sealed class LoadedModelSession
    {
        public required string SessionId { get; init; }
        public required ModelRecord Model { get; init; }
        public required RuntimeRecord Runtime { get; init; }
        public required AppSettings LaunchSettings { get; set; }
        public required DateTimeOffset StartedAt { get; set; }
        public required LlamaProcessSupervisor Supervisor { get; init; }
    }

    private readonly Dictionary<string, LoadedModelSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly LlamaProcessSupervisor _inactiveSupervisor = new();

    public string SelectedSessionId { get; private set; } = "";

    public LlamaProcessSupervisor ActiveSupervisor
        => !string.IsNullOrWhiteSpace(SelectedSessionId) && _sessions.TryGetValue(SelectedSessionId, out var session)
            ? session.Supervisor
            : _inactiveSupervisor;

    public AppSettings? ActiveSettings
        => !string.IsNullOrWhiteSpace(SelectedSessionId) && _sessions.TryGetValue(SelectedSessionId, out var session)
            ? session.LaunchSettings
            : null;

    public bool HasRunningSessions => _sessions.Values.Any(session => session.Supervisor.IsRunning);

    public IReadOnlyList<LoadedModelSessionSnapshot> Snapshots()
        => _sessions.Values
            .Select(ToSnapshot)
            .OrderByDescending(snapshot => snapshot.IsSelected)
            .ThenBy(snapshot => snapshot.ModelName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public LoadedModelSessionSnapshot? SelectedSnapshot()
        => Snapshots().FirstOrDefault(snapshot => snapshot.IsSelected)
            ?? Snapshots().FirstOrDefault();

    public LoadedModelSessionSnapshot? SessionForModel(string modelId)
        => Snapshots().FirstOrDefault(snapshot => string.Equals(snapshot.ModelId, modelId, StringComparison.OrdinalIgnoreCase));

    public bool IsModelLoaded(string modelId)
        => SessionForModel(modelId) is { IsRunning: true };

    public bool IsModelActive(string modelId)
        => SessionForModel(modelId) is { IsRunning: true, IsSelected: true };

    public IEnumerable<int> ReservedPorts(string? exceptSessionId = null)
        => _sessions.Values
            .Where(session => !string.Equals(session.SessionId, exceptSessionId, StringComparison.OrdinalIgnoreCase))
            .Select(session => session.LaunchSettings.Port)
            .Where(RuntimePortAllocator.IsValidPort)
            .Distinct();

    public async Task<LoadedModelSessionSnapshot> StartAsync(RuntimeRecord runtime, ModelRecord model, AppSettings settings, string logRoot)
    {
        var sessionId = SessionIdFor(model.Id);
        await StopAsync(sessionId);
        var supervisor = new LlamaProcessSupervisor();
        await supervisor.StartAsync(runtime, model, settings, logRoot);
        var session = new LoadedModelSession
        {
            SessionId = sessionId,
            Model = model,
            Runtime = runtime,
            LaunchSettings = settings,
            StartedAt = DateTimeOffset.UtcNow,
            Supervisor = supervisor
        };
        _sessions[sessionId] = session;
        SelectedSessionId = sessionId;
        return ToSnapshot(session);
    }

    public LoadedModelSessionSnapshot AttachExisting(
        RuntimeRecord runtime,
        ModelRecord model,
        AppSettings settings,
        string logPath,
        LlamaRuntimeState state,
        string processMarker,
        string sessionId,
        DateTimeOffset startedAt,
        int processId = 0)
    {
        var resolvedSessionId = string.IsNullOrWhiteSpace(sessionId) ? SessionIdFor(model.Id) : sessionId;
        var supervisor = new LlamaProcessSupervisor();
        supervisor.AttachExisting(runtime, model.Id, settings, logPath, state, processMarker, processId);
        var session = new LoadedModelSession
        {
            SessionId = resolvedSessionId,
            Model = model,
            Runtime = runtime,
            LaunchSettings = settings,
            StartedAt = startedAt,
            Supervisor = supervisor
        };
        _sessions[resolvedSessionId] = session;
        if (string.IsNullOrWhiteSpace(SelectedSessionId))
            SelectedSessionId = resolvedSessionId;
        return ToSnapshot(session);
    }

    public bool SelectSession(string sessionId)
    {
        if (!_sessions.ContainsKey(sessionId)) return false;
        SelectedSessionId = sessionId;
        return true;
    }

    public bool SelectModel(string modelId)
    {
        var session = _sessions.Values.FirstOrDefault(item => string.Equals(item.Model.Id, modelId, StringComparison.OrdinalIgnoreCase));
        if (session is null) return false;
        SelectedSessionId = session.SessionId;
        return true;
    }

    public async Task StopModelAsync(string modelId)
    {
        var session = _sessions.Values.FirstOrDefault(item => string.Equals(item.Model.Id, modelId, StringComparison.OrdinalIgnoreCase));
        if (session is not null)
            await StopAsync(session.SessionId);
    }

    public async Task StopSelectedAsync()
    {
        if (!string.IsNullOrWhiteSpace(SelectedSessionId))
            await StopAsync(SelectedSessionId);
    }

    public Task StopAsync(string sessionId)
    {
        if (!_sessions.Remove(sessionId, out var session)) return Task.CompletedTask;
        session.Supervisor.Stop();
        session.Supervisor.Dispose();
        if (string.Equals(SelectedSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            SelectedSessionId = _sessions.Keys.FirstOrDefault() ?? "";
        return Task.CompletedTask;
    }

    public async Task StopAllAsync()
    {
        foreach (var sessionId in _sessions.Keys.ToArray())
            await StopAsync(sessionId);
        SelectedSessionId = "";
    }

    public bool MarkLoadedIfRunning(string sessionId)
        => _sessions.TryGetValue(sessionId, out var session) && session.Supervisor.MarkLoadedIfRunning();

    public bool MarkModelLoadedIfRunning(string modelId)
    {
        var session = _sessions.Values.FirstOrDefault(item => string.Equals(item.Model.Id, modelId, StringComparison.OrdinalIgnoreCase));
        return session is not null && session.Supervisor.MarkLoadedIfRunning();
    }

    public int RemoveFailedOrStopped()
    {
        var removed = 0;
        foreach (var session in _sessions.Values.Where(session => !session.Supervisor.IsRunning).ToArray())
        {
            _sessions.Remove(session.SessionId);
            session.Supervisor.Dispose();
            removed++;
        }
        if (!string.IsNullOrWhiteSpace(SelectedSessionId) && !_sessions.ContainsKey(SelectedSessionId))
            SelectedSessionId = _sessions.Keys.FirstOrDefault() ?? "";
        return removed;
    }

    public async Task<int> StopUnavailableRecoveredSessionsAsync(Func<LoadedModelSessionSnapshot, Task<bool>> isAvailable)
    {
        var removed = 0;
        foreach (var session in _sessions.Values.ToArray())
        {
            if (!session.Supervisor.IsRecovered
                || !session.Supervisor.IsRunning
                || session.Supervisor.State is not (LlamaRuntimeState.Loading or LlamaRuntimeState.Loaded))
                continue;

            if (await isAvailable(ToSnapshot(session))) continue;

            await StopAsync(session.SessionId);
            removed++;
        }
        return removed;
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
            session.Supervisor.Dispose();
        _sessions.Clear();
        _inactiveSupervisor.Dispose();
    }

    public static string SessionIdFor(string modelId)
        => ModelCatalogService.SafeId($"session-{modelId}");

    private LoadedModelSessionSnapshot ToSnapshot(LoadedModelSession session)
    {
        var state = session.Supervisor.State;
        var status = state switch
        {
            LlamaRuntimeState.Loading => LoadedModelSessionStatus.Loading,
            LlamaRuntimeState.Loaded => LoadedModelSessionStatus.Running,
            LlamaRuntimeState.Failed => LoadedModelSessionStatus.Failed,
            _ => LoadedModelSessionStatus.Stopped
        };
        return new LoadedModelSessionSnapshot(
            session.SessionId,
            session.Model.Id,
            session.Model.Name,
            session.Runtime.Id,
            session.Runtime.Name,
            session.Runtime.Mode,
            session.Runtime.Backend,
            session.LaunchSettings,
            session.Supervisor.LogPath,
            session.StartedAt,
            session.Supervisor.WslProcessMarker,
            session.Supervisor.ProcessId,
            status,
            session.Supervisor.IsRunning,
            string.Equals(session.SessionId, SelectedSessionId, StringComparison.OrdinalIgnoreCase),
            ModelSizeBytes(session.Model.ModelPath));
    }

    private static long ModelSizeBytes(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch { return 0; }
    }
}
