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
    private void OpenModelFolderRow_Click(object sender, RoutedEventArgs e)
    {
        var model = ModelFromRowButton(sender);
        if (model is null) return;

        var folder = Path.GetDirectoryName(model.ModelPath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            SetStatus("Model folder is unavailable.");
            return;
        }

        OpenFolder(folder);
    }

    private async void DeleteModelRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var model = ModelFromRowButton(sender);
            if (model is not null) await DeleteModelAsync(model);
        });
    }

    private async Task DeleteModelAsync(ModelRecord model)
    {
        if (IsModelActive(model)) { SetStatus("Unload the selected model before deleting it."); return; }
        var action = model.Ownership == OwnershipKind.AppOwned ? "delete the downloaded model files" : "remove the model registration only";
        if (ThemedMessageBox.Show(this, $"This will {action} for:\n\n{model.Name}", "Remove model", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync("Removing model...", async () =>
        {
            await _catalog!.DeleteAsync(model, _settings.ModelsRoot);
            await RefreshModelsAsync();
            await RefreshOverviewAsync();
            UpdateModelActionButtons();
        });
    }
}
