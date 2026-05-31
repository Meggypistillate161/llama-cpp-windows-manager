using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class RuntimeBuildsPageViewModel
{
    public ObservableCollection<RuntimeBuildPresetRow> Rows { get; } = new();

    public void ReplaceRows(IEnumerable<RuntimeBuildPresetRow> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);
    }

}
