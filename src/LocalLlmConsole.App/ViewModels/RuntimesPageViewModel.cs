using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class RuntimesPageViewModel
{
    public ObservableCollection<RuntimeCatalogRow> Rows { get; } = new();

    public void ReplaceRows(IEnumerable<RuntimeCatalogRow> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);
    }

}
