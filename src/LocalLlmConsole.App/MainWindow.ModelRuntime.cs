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
            if (_llama.IsRunning && !selectedModelActive)
            {
                var currentModel = await ActiveModelDisplayNameAsync(_llama.ActiveModelId);
                var result = ThemedMessageBox.Show(
                    this,
                    $"A model is already loaded:\n\n{currentModel}\n\nLoading {model.Name} will unload the current model first.\n\nUnload current model and load the selected one?",
                    "Replace loaded model",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                await StopLoadedRuntimeAsync();
            }

            var runtimes = await _stateStore!.ListRuntimesAsync();
            var runtime = ResolveLaunchRuntime(runtimes);
            if (runtime is null) { SetStatus("Register a llama.cpp runtime first."); return; }
            if (!string.Equals(_launchSettingsModelId, model.Id, StringComparison.OrdinalIgnoreCase))
                await RenderSelectedModelLaunchSettingsAsync();
            var launchSettings = ReadLaunchSettingsFromControls();
            await SaveLaunchSettingsForModelAsync(model, launchSettings);
            await StartModelRuntimeAsync(runtime, model, launchSettings);
        });
    }

    private async Task UnloadSelectedModelAsync()
    {
        var model = SelectedModel();
        if (!IsModelActive(model)) { SetStatus("Select the loading or loaded model to unload it."); return; }
        await StopLoadedRuntimeAsync();
    }

    private async Task LoadOverviewSelectedModelAsync()
    {
        await RunAsync("Preparing model load...", async () =>
        {
            var model = SelectedOverviewModel();
            if (model is null) { SetStatus("Choose a model first."); return; }
            if (IsModelActive(model)) { SetStatus("Selected model is already active."); return; }
            if (_stateStore is null) { SetStatus("App is still starting."); return; }

            if (_llama.IsRunning)
            {
                var currentModel = await ActiveModelDisplayNameAsync(_llama.ActiveModelId);
                var result = ThemedMessageBox.Show(
                    this,
                    $"A model is already loaded:\n\n{currentModel}\n\nLoading {model.Name} will unload the current model first.\n\nUnload current model and load the selected one?",
                    "Replace loaded model",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                await StopLoadedRuntimeAsync();
            }

            var profile = await _stateStore.GetModelLaunchSettingsAsync(model.Id);
            var launchSettings = profile?.ApplyTo(_settings) ?? _settings;
            var runtimes = await _stateStore.ListRuntimesAsync();
            var runtime = ResolveLaunchRuntime(runtimes, profile?.RuntimeId);
            if (runtime is null) { SetStatus("Register a llama.cpp runtime first."); return; }

            await StartModelRuntimeAsync(runtime, model, launchSettings);
        });
    }

    private async Task UnloadOverviewSelectedModelAsync()
    {
        var model = SelectedOverviewModel();
        if (!IsModelActive(model)) { SetStatus("Choose the loading or loaded model to unload it."); return; }
        await StopLoadedRuntimeAsync();
    }

}
