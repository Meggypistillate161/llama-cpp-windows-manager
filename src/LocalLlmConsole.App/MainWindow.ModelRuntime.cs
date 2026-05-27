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
    private async Task LoadSelectedModelAsync(bool restart)
    {
        await RunAsync(restart ? "Preparing restart..." : "Preparing model load...", async () =>
        {
            var model = SelectedModel();
            if (model is null) { SetStatus("Select a model first."); return; }
            var selectedModelLoaded = IsModelLoaded(model);
            var selectedModelActive = IsModelActive(model);
            if (!restart && selectedModelActive) { SetStatus("Selected model is already active."); return; }
            if (restart && !selectedModelLoaded) { SetStatus("Load the selected model before restarting it."); return; }
            if (!restart && selectedModelLoaded)
            {
                await SwitchToLoadedModelAsync(model);
                return;
            }
            if (!string.Equals(_launchSettingsModelId, model.Id, StringComparison.OrdinalIgnoreCase))
                await RenderSelectedModelLaunchSettingsAsync();
            var launchSettings = ReadLaunchSettingsFromControls();
            var runtimes = await _stateStore!.ListRuntimesAsync();
            var runtime = ResolveLaunchRuntime(runtimes);
            if (runtime is null) { SetMissingLaunchRuntimeStatus(runtimes, SelectedLaunchRuntimeId()); return; }
            if (restart)
                await StopModelRuntimeAsync(model);
            await StartModelRuntimeAsync(runtime, model, launchSettings);
        });
    }

    private async Task UnloadSelectedModelAsync()
    {
        var model = SelectedModel();
        if (model is null || !IsModelLoaded(model)) { SetStatus("Select the loading or loaded model to unload it."); return; }
        await StopModelRuntimeAsync(model);
    }

    private async Task LoadOverviewSelectedModelAsync()
    {
        await RunAsync("Preparing model load...", async () =>
        {
            var model = SelectedOverviewModel();
            if (model is null) { SetStatus("Choose a model first."); return; }
            await LoadOverviewModelAsync(model);
        });
    }

    private async Task LoadOverviewModelAsync(ModelRecord model)
    {
        if (IsModelActive(model)) { SetStatus("Selected model is already active."); return; }
        if (IsModelLoaded(model))
        {
            await SwitchToLoadedModelAsync(model);
            return;
        }
        if (_stateStore is null) { SetStatus("App is still starting."); return; }

        var profile = await DraftModelLaunchProfileAsync(model);
        var launchSettings = profile.ApplyTo(_settings);
        var runtimes = await _stateStore.ListRuntimesAsync();
        var runtime = ResolveLaunchRuntime(runtimes, profile.RuntimeId);
        if (runtime is null) { SetMissingLaunchRuntimeStatus(runtimes, profile.RuntimeId); return; }

        await StartModelRuntimeAsync(runtime, model, launchSettings);
    }

    private async Task UnloadOverviewSelectedModelAsync()
    {
        var model = SelectedOverviewModel();
        if (model is null || !IsModelLoaded(model)) { SetStatus("Choose the loading or loaded model to unload it."); return; }
        await StopModelRuntimeAsync(model);
    }

}
