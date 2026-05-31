using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace LocalLlmConsole;

public sealed class OverviewPageState
{
    public WpfComboBox? ModelCombo { get; private set; }

    public WpfButton? LoadButton { get; private set; }

    public WpfButton? UnloadButton { get; private set; }

    public DataGrid? LoadedSessionsGrid { get; private set; }

    public UiRow? SelectedLoadedSessionRow => LoadedSessionsGrid?.SelectedItem as UiRow;

    public string SelectedLoadedSessionId => SelectedLoadedSessionRow?.Data["SessionId"]?.ToString() ?? "";

    public void Apply(OverviewPageControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        ModelCombo = controls.ModelCombo;
        LoadButton = controls.LoadButton;
        UnloadButton = controls.UnloadButton;
        LoadedSessionsGrid = controls.LoadedSessionsGrid;
    }

    public void FocusLoadedSessionsGrid()
        => LoadedSessionsGrid?.Focus();

    public void FocusModelCombo()
        => ModelCombo?.Focus();

    public ModelRecord? SelectedModel(IReadOnlyList<ModelRecord> modelChoices)
    {
        ArgumentNullException.ThrowIfNull(modelChoices);

        if (ModelCombo?.SelectedItem is ModelRecord model)
            return model;
        if (ModelCombo?.SelectedValue is string selectedId)
            return modelChoices.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        return null;
    }

    public void SelectModelChoice(string? selectedId, IReadOnlyList<ModelRecord> modelChoices)
    {
        ArgumentNullException.ThrowIfNull(modelChoices);
        if (ModelCombo is null) return;

        if (modelChoices.Count == 0)
        {
            ModelCombo.SelectedIndex = -1;
            return;
        }

        var match = modelChoices.FirstOrDefault(model => string.Equals(model.Id, selectedId, StringComparison.OrdinalIgnoreCase))
            ?? modelChoices.First();
        ModelCombo.SelectedValue = match.Id;
    }

    public void SelectModelId(string modelId)
    {
        if (ModelCombo is not null)
            ModelCombo.SelectedValue = modelId;
    }

    public void SetModelActionsEnabled(bool hasSelection, bool selectedModelLoaded)
    {
        if (LoadButton is not null)
            LoadButton.IsEnabled = hasSelection && !selectedModelLoaded;
        if (UnloadButton is not null)
            UnloadButton.IsEnabled = selectedModelLoaded;
    }

    public void RestoreLoadedSessionSelection(string sessionId, IReadOnlyList<UiRow> sessionRows)
    {
        ArgumentNullException.ThrowIfNull(sessionRows);
        if (LoadedSessionsGrid is null) return;

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            LoadedSessionsGrid.SelectedItem = sessionRows.FirstOrDefault(row =>
                string.Equals(row.Data["SessionId"]?.ToString(), sessionId, StringComparison.OrdinalIgnoreCase));
        }

        LoadedSessionsGrid.Items.Refresh();
    }
}
