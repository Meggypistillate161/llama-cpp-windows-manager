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
    private async Task DetectAndRefreshRuntimesAsync()
    {
        var runtimeCatalog = RuntimeServices.RuntimeCatalogApplication;
        if (runtimeCatalog is null) return;
        await runtimeCatalog.DetectAndRefreshAsync(_settings, _runtimeCatalogState, RuntimeCatalogScanActions());
    }

    private async Task EnsureRuntimeRootScannedAsync()
    {
        var runtimeCatalog = RuntimeServices.RuntimeCatalogApplication;
        if (runtimeCatalog is not null)
            await runtimeCatalog.EnsureRuntimeRootScannedAsync(_settings, _runtimeCatalogState);
    }

    private async Task ChangeRuntimeCudaPackagePreferenceAsync()
    {
        var runtimeCatalogCommands = RuntimeServices.RuntimeCatalogCommands;
        if (runtimeCatalogCommands is null) return;

        var result = await runtimeCatalogCommands.ChangeCudaPackagePreferenceAsync(
            _settings,
            _runtimesPage.SelectedCudaPackagePreference,
            RuntimeCatalogPreferenceActions());
        _settings = result.Settings;
    }

    private async Task AddCustomRuntimeRepositoryAsync()
    {
        var runtimeCatalogCommands = RuntimeServices.RuntimeCatalogCommands;
        if (runtimeCatalogCommands is null) return;

        var draft = ShowCustomRuntimeRepositoryDialog();
        await runtimeCatalogCommands.AddCustomRepositoryAsync(
            _settings.RuntimeRoot,
            draft,
            RuntimeCatalogCustomRepositoryActions(message =>
                _coreServices.App.Dialogs.Notify(this, message, "Custom repository", MessageBoxImage.Information)));
    }

    private async Task AddCustomRuntimeRepositoryFromRowAsync(RuntimeBuildPresetRow row)
    {
        var runtimeCatalogCommands = RuntimeServices.RuntimeCatalogCommands;
        if (runtimeCatalogCommands is null) return;

        await runtimeCatalogCommands.AddCustomRepositoryAsync(
            _settings.RuntimeRoot,
            new RuntimeCustomRepositoryDraft(row.Label, row.Source, row.LatestLocal, row.Backend),
            RuntimeCatalogCustomRepositoryActions(SetStatus));
    }

    private RuntimeCustomRepositoryDraft? ShowCustomRuntimeRepositoryDialog()
    {
        var customRuntimeRepositories = RuntimeServices.CustomRuntimeRepositories;
        Require(customRuntimeRepositories);
        return RuntimeCustomRepositoryDialogFactory.Show(new RuntimeCustomRepositoryDialogRequest(
            this,
            draft => customRuntimeRepositories!.BuildPreset(draft),
            (owner, message) => _coreServices.App.Dialogs.Notify(owner, message, "Custom repository", MessageBoxImage.Warning)));
    }

    private RuntimeCatalogScanApplicationActions RuntimeCatalogScanActions()
        => new(
            RunAsync,
            RefreshRuntimesAsync,
            RefreshJobsAsync,
            RefreshOverviewAsync);

    private RuntimeCatalogPreferenceApplicationActions RuntimeCatalogPreferenceActions()
        => new(
            PersistSettingsAsync,
            _runtimeCatalogState.ClearRuntimePackageUpdateStates,
            RefreshRuntimesAsync,
            SetStatus);

    private RuntimeCatalogCustomRepositoryApplicationActions RuntimeCatalogCustomRepositoryActions(Action<string> reportFailure)
        => new(
            RefreshRuntimesAsync,
            SetStatus,
            reportFailure);
}
