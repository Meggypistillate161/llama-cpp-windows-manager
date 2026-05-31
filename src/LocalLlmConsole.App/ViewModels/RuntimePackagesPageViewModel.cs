using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class RuntimePackagesPageViewModel
{
    public ObservableCollection<RuntimePackagePresetRow> Rows { get; } = new();

    public void ReplaceRows(IEnumerable<RuntimePackagePresetRow> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);
    }

}
