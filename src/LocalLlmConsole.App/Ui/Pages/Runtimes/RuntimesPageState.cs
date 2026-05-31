using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace LocalLlmConsole;

public sealed class RuntimesPageState
{
    public TextBlock? RuntimesFolderText { get; private set; }

    public RuntimeRecord? SelectedRuntime => RuntimeGrid?.SelectedItem is RuntimeCatalogRow row ? row.Runtime : null;

    public string SelectedCudaPackagePreference => RuntimeCudaPreferenceCombo?.SelectedItem?.ToString() ?? "";

    private DataGrid? RuntimeGrid { get; set; }

    private DataGrid? RuntimePackageGrid { get; set; }

    private DataGrid? RuntimeBuildGrid { get; set; }

    private DataGrid? RuntimeJobsGrid { get; set; }

    private WpfButton? RuntimeAdvancedToggleButton { get; set; }

    private WpfComboBox? RuntimeCudaPreferenceCombo { get; set; }

    public void Apply(RuntimesPageControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        RuntimesFolderText = controls.RuntimesFolderText;
        RuntimeGrid = controls.RuntimeGrid;
        RuntimePackageGrid = controls.RuntimePackageGrid;
        RuntimeBuildGrid = controls.RuntimeBuildGrid;
        RuntimeJobsGrid = controls.RuntimeJobsGrid;
        RuntimeAdvancedToggleButton = controls.RuntimeAdvancedToggleButton;
        RuntimeCudaPreferenceCombo = controls.RuntimeCudaPreferenceCombo;
    }

    public void FocusRuntimeJobsGrid()
        => RuntimeJobsGrid?.Focus();

    public void RefreshRuntimePackageGrid()
        => RuntimePackageGrid?.Items.Refresh();

    public void RefreshRuntimeBuildGrid()
        => RuntimeBuildGrid?.Items.Refresh();

    public bool ClearSelectedRuntimeIfRowAlreadySelected(DataGridRow? row)
    {
        if (row?.IsSelected != true || RuntimeGrid is null)
            return false;

        RuntimeGrid.SelectedItem = null;
        return true;
    }

    public string SelectedRuntimeJobId()
        => (RuntimeJobsGrid?.SelectedItem as UiRow)?.Data["Id"]?.ToString() ?? "";

    public void RestoreRuntimeSelection(string? selectedId, IReadOnlyList<RuntimeCatalogRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (RuntimeGrid is null) return;

        RuntimeGrid.SelectedItem = string.IsNullOrWhiteSpace(selectedId)
            ? null
            : rows.FirstOrDefault(row => string.Equals(row.Runtime?.Id, selectedId, StringComparison.OrdinalIgnoreCase));
    }

    public void RestoreRuntimeJobSelection(string? selectedJobId, IReadOnlyList<UiRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (RuntimeJobsGrid is null) return;

        RuntimeJobsGrid.SelectedItem = string.IsNullOrWhiteSpace(selectedJobId)
            ? rows.FirstOrDefault()
            : rows.FirstOrDefault(row => string.Equals(row.Data["Id"]?.ToString(), selectedJobId, StringComparison.OrdinalIgnoreCase))
                ?? rows.FirstOrDefault();
        RuntimeJobsGrid.Items.Refresh();
    }
}
