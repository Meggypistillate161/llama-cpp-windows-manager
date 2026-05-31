using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed record GatewayRoutingOverviewStatus(
    bool Visible,
    bool Enabled,
    string Endpoint,
    string State,
    string Policy,
    string Exposure,
    int RunningSessions)
{
    public static GatewayRoutingOverviewStatus Hidden { get; } = new(false, false, "", "", "", "", 0);

    public static GatewayRoutingOverviewStatus FromEndpoint(string endpoint)
        => string.IsNullOrWhiteSpace(endpoint)
            ? Hidden
            : new(true, true, endpoint.Trim(), "Listening", "", "", 0);
}

public sealed class OverviewPageViewModel
{
    public ObservableCollection<ModelRecord> ModelChoices { get; } = new();
    public ObservableCollection<UiRow> SessionRows { get; } = new();

    public void ReplaceModels(IEnumerable<ModelRecord> models)
    {
        ModelChoices.Clear();
        foreach (var model in models.OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase))
            ModelChoices.Add(model);
    }

    public void ReplaceSessions(IEnumerable<LoadedModelSessionSnapshot> sessions, string gatewayEndpoint = "")
        => _ = ReplaceSessionsIfChanged(sessions, GatewayRoutingOverviewStatus.FromEndpoint(gatewayEndpoint));

    public bool ReplaceSessionsIfChanged(IEnumerable<LoadedModelSessionSnapshot> sessions, string gatewayEndpoint = "")
        => ReplaceSessionsIfChanged(sessions, GatewayRoutingOverviewStatus.FromEndpoint(gatewayEndpoint));

    public void ReplaceSessions(IEnumerable<LoadedModelSessionSnapshot> sessions, GatewayRoutingOverviewStatus gateway)
        => _ = ReplaceSessionsIfChanged(sessions, gateway);

    public bool ReplaceSessionsIfChanged(IEnumerable<LoadedModelSessionSnapshot> sessions, GatewayRoutingOverviewStatus gateway)
    {
        var sessionRows = sessions.ToArray();
        var rows = BuildSessionRows(sessionRows, gateway).ToArray();
        if (RowsEqual(SessionRows, rows)) return false;

        SessionRows.Clear();
        foreach (var row in rows)
            SessionRows.Add(row);
        return true;
    }

    private static IEnumerable<UiRow> BuildSessionRows(IReadOnlyList<LoadedModelSessionSnapshot> sessions, GatewayRoutingOverviewStatus gateway)
    {
        if (gateway.Visible)
            yield return GatewayRow(gateway);

        foreach (var session in sessions.OrderByDescending(session => session.IsSelected).ThenBy(session => session.ModelName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new UiRow
            {
                C1 = session.IsSelected ? $"{session.ModelName} (selected)" : session.ModelName,
                C2 = session.ModelSize,
                C3 = SessionStatusLabel(session),
                C4 = EndpointLabel(session, gateway),
                C5 = session.RuntimeName,
                C6 = $"{session.Backend} {session.Mode}",
                Data = JsonSerializer.SerializeToNode(new { session.SessionId, session.ModelId }) as JsonObject ?? new JsonObject()
            };
        }
    }

    private static UiRow GatewayRow(GatewayRoutingOverviewStatus gateway)
        => new()
        {
            C1 = gateway.Enabled ? "Auto-load gateway" : "Auto-load gateway (off)",
            C2 = "Router",
            C3 = string.IsNullOrWhiteSpace(gateway.State) ? (gateway.Enabled ? "Enabled" : "Off") : gateway.State,
            C4 = gateway.Enabled
                ? $"Gateway: {gateway.Endpoint}{Environment.NewLine}Routes by model id to {gateway.RunningSessions.ToString(CultureInfo.InvariantCulture)} loaded direct session(s)."
                : "Gateway disabled",
            C5 = string.IsNullOrWhiteSpace(gateway.Policy) ? "" : gateway.Policy,
            C6 = string.IsNullOrWhiteSpace(gateway.Exposure) ? "" : gateway.Exposure,
            Data = JsonSerializer.SerializeToNode(new { Kind = "Gateway" }) as JsonObject ?? new JsonObject()
        };

    private static bool RowsEqual(IReadOnlyList<UiRow> left, IReadOnlyList<UiRow> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (!RowEquals(left[i], right[i])) return false;
        }

        return true;
    }

    private static bool RowEquals(UiRow left, UiRow right)
        => string.Equals(left.C1, right.C1, StringComparison.Ordinal)
           && string.Equals(left.C2, right.C2, StringComparison.Ordinal)
           && string.Equals(left.C3, right.C3, StringComparison.Ordinal)
           && string.Equals(left.C4, right.C4, StringComparison.Ordinal)
           && string.Equals(left.C5, right.C5, StringComparison.Ordinal)
           && string.Equals(left.C6, right.C6, StringComparison.Ordinal)
           && string.Equals(left.Data["SessionId"]?.ToString(), right.Data["SessionId"]?.ToString(), StringComparison.Ordinal)
           && string.Equals(left.Data["ModelId"]?.ToString(), right.Data["ModelId"]?.ToString(), StringComparison.Ordinal);

    private static string SessionStatusLabel(LoadedModelSessionSnapshot session) => session.Status switch
    {
        LoadedModelSessionStatus.Running or LoadedModelSessionStatus.Warm => "Loaded",
        LoadedModelSessionStatus.Loading => "Loading",
        LoadedModelSessionStatus.Failed => "Failed",
        _ => session.IsRunning ? "Loaded" : "Stopped"
    };

    private static string EndpointLabel(LoadedModelSessionSnapshot session, GatewayRoutingOverviewStatus gateway)
    {
        if (!gateway.Visible || !gateway.Enabled)
            return session.Endpoint;
        return $"Direct: {session.Endpoint}";
    }
}
