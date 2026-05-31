using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfBinding = System.Windows.Data.Binding;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfTextBox = System.Windows.Controls.TextBox;
namespace LocalLlmConsole;

public partial class MainWindow
{
    private async Task<ModelLaunchSettings?> ReadModelLaunchProfileAsync(ModelRecord model)
    {
        var launchProfiles = ModelServices.LaunchProfiles;
        return launchProfiles is null ? null : await launchProfiles.ReadAsync(model);
    }

    private async Task<ModelLaunchSettings> DraftModelLaunchProfileAsync(ModelRecord model)
    {
        var launchProfiles = ModelServices.LaunchProfiles;
        Require(launchProfiles);
        return await launchProfiles!.DraftAsync(model, _settings);
    }

    private async Task<ModelLaunchSettings?> EnsureModelLaunchProfileAsync(ModelRecord model)
    {
        var launchSettings = ModelServices.ModelLaunchSettingsWorkflow;
        if (launchSettings is null) return null;
        return await launchSettings.EnsureAsync(model, _settings);
    }

}
