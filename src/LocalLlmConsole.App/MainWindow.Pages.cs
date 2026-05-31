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
    private void ShowOverview()
    {
        SetPage("Overview", "Windows WPF shell supervising llama.cpp in Ubuntu/WSL.");
        var overview = OverviewPageFactory.Create(new OverviewPageRequest(
            _viewModel,
            _pageControllers.Overview.Build(),
            SetRuntimeMetricsGridColumnSizing));

        _overviewPage.Apply(overview);
        _runtimeDashboardPage.Apply(overview);

        PageHost.Content = overview.Root;
        RunBackground(RefreshOverviewAsync, "Overview refresh failed");
        RunBackground(RefreshOverviewModelSelectorAsync, "Overview model refresh failed");
        RunBackground(RefreshRuntimeMetricsAsync, "Runtime metrics refresh failed");
        StartRuntimeDashboardRefreshTimer();
    }

    private void ShowModels()
    {
        SetPage("Models", "Scan, import, download, configure, and safely remove models.");

        var modelsPage = ModelsPageFactory.Create(new ModelsPageRequest(
            _viewModel,
            _settings.ModelsRoot,
            CreateLaunchSettingsPanel(),
            _pageControllers.Models.Build()));

        _modelsPage.Apply(modelsPage);
        ConfigureHfSearchGrid();
        PageHost.Content = modelsPage.Root;
        RunBackground(RefreshModelsAsync, "Models refresh failed");
    }

    private void ShowRuntimes()
    {
        SetPage("Runtimes", "Register Windows or Ubuntu/WSL llama.cpp builds and run them without visible command prompts.");
        var runtimesPage = RuntimesPageFactory.Create(new RuntimesPageRequest(
            _viewModel,
            _settings.RuntimeRoot,
            _coreServices.Ui.AdvancedSections.ShowRuntimes,
            _settings.CudaPackagePreference,
            _pageControllers.Runtimes.Build()));

        _runtimesPage.Apply(runtimesPage);
        PageHost.Content = runtimesPage.Root;
        RunBackground(DetectAndRefreshRuntimesAsync, "Runtime refresh failed");
    }

    private async Task ScanModelsFolderAsync()
    {
        await RunAsync("Scanning models...", async () =>
        {
            var catalog = ModelServices.Catalog;
            Require(catalog);
            await catalog!.ScanAsync(_settings.ModelsRoot);
            await RefreshModelsAsync();
            await RefreshOverviewAsync();
        });
    }

    private void ToggleAdvancedRuntimes()
    {
        _coreServices.Ui.AdvancedSections.ToggleRuntimes();
        ShowRuntimes();
    }
}
