using System.Windows.Controls;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace LocalLlmConsole;

public sealed class SettingsPageState
{
    public DataGrid? SettingsGrid { get; private set; }

    private WpfComboBox? ThemeCombo { get; set; }

    public string SelectedThemeValue
        => ThemeCombo?.SelectedItem?.ToString() ?? ThemeCombo?.Text ?? "";

    public void Apply(SettingsPageControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        ThemeCombo = controls.ThemeCombo;
        SettingsGrid = controls.SettingsGrid;
    }
}
