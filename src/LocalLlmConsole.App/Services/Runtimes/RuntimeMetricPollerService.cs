namespace LocalLlmConsole.Services;

public sealed record RuntimeMetricPollResult(
    LoadedModelSessionSnapshot Session,
    string RuntimeKey,
    IReadOnlyList<PrometheusSample> Samples,
    RuntimeSlotSnapshot? SlotSnapshot,
    string Error);

public sealed class RuntimeMetricPollerService
{
    private readonly HttpClient _http;

    public RuntimeMetricPollerService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public Task<RuntimeMetricPollResult[]> PollSessionsAsync(
        IReadOnlyList<LoadedModelSessionSnapshot> sessions,
        CancellationToken cancellationToken = default)
        => sessions.Count == 0
            ? Task.FromResult(Array.Empty<RuntimeMetricPollResult>())
            : Task.WhenAll(sessions.Select(session => PollSessionAsync(session, cancellationToken)));

    public static string RuntimeKey(LoadedModelSessionSnapshot session)
        => $"{session.ModelId}|{session.RuntimeId}|{session.LaunchSettings.Port}";

    private async Task<RuntimeMetricPollResult> PollSessionAsync(
        LoadedModelSessionSnapshot session,
        CancellationToken cancellationToken)
    {
        var runtimeKey = RuntimeKey(session);
        var settings = session.LaunchSettings;
        var slotTask = SlotSnapshotAsync(settings, cancellationToken);
        if (!settings.EnableMetrics)
            return new RuntimeMetricPollResult(session, runtimeKey, [], await slotTask, "");

        try
        {
            var raw = await RuntimeEndpointService.RuntimeGetStringAsync(
                _http,
                $"{RuntimeEndpointService.LocalServerBaseUrl(settings)}/metrics",
                settings,
                cancellationToken);
            return new RuntimeMetricPollResult(
                session,
                runtimeKey,
                RuntimeMetrics.ParsePrometheus(raw),
                await slotTask,
                "");
        }
        catch (Exception ex)
        {
            return new RuntimeMetricPollResult(session, runtimeKey, [], await slotTask, ex.Message);
        }
    }

    private async Task<RuntimeSlotSnapshot?> SlotSnapshotAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var raw = await RuntimeEndpointService.RuntimeGetStringAsync(
                _http,
                $"{RuntimeEndpointService.LocalServerBaseUrl(settings)}/slots",
                settings,
                cancellationToken);
            return RuntimeDashboardService.ParseSlotSnapshot(raw);
        }
        catch
        {
            return null;
        }
    }
}
