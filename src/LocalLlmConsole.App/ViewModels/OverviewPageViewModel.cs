using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

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

    public void ReplaceSessions(IEnumerable<LoadedModelSessionSnapshot> sessions)
    {
        SessionRows.Clear();
        foreach (var session in sessions.OrderByDescending(session => session.IsSelected).ThenBy(session => session.ModelName, StringComparer.OrdinalIgnoreCase))
        {
            SessionRows.Add(new UiRow
            {
                C1 = session.IsSelected ? $"{session.ModelName} (selected)" : session.ModelName,
                C2 = session.ModelSize,
                C3 = SessionStatusLabel(session),
                C4 = session.Endpoint,
                C5 = session.RuntimeName,
                C6 = $"{session.Backend} {session.Mode}",
                Data = JsonSerializer.SerializeToNode(new { session.SessionId, session.ModelId }) as JsonObject ?? new JsonObject()
            });
        }
    }

    private static string SessionStatusLabel(LoadedModelSessionSnapshot session) => session.Status switch
    {
        LoadedModelSessionStatus.Running or LoadedModelSessionStatus.Warm => "Loaded",
        LoadedModelSessionStatus.Loading => "Loading",
        LoadedModelSessionStatus.Failed => "Failed",
        _ => session.IsRunning ? "Loaded" : "Stopped"
    };
}
