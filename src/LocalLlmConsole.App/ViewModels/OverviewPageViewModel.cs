using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class OverviewPageViewModel
{
    public ObservableCollection<ModelRecord> ModelChoices { get; } = new();

    public void ReplaceModels(IEnumerable<ModelRecord> models)
    {
        ModelChoices.Clear();
        foreach (var model in models.OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase))
            ModelChoices.Add(model);
    }
}
