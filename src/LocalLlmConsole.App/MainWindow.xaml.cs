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

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = $"{AppDisplayName} {AppVersionLabel}";
        AppVersionText.Text = AppVersionLabel;
        ApplyStaticButtonToolTips();
        StateChanged += Window_StateChanged;
        _workspaceRoot = WorkspaceRootResolver.Resolve();
        _settings = AppSettings.CreateDefault(_workspaceRoot);
        _activeSessions = new ActiveRuntimeSessionStore(_workspaceRoot);
        _openCode = new OpenCodeConfigService(_workspaceRoot);
        InitializeTrayIcon();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RunAsync("Starting app...", async () =>
        {
            Directory.CreateDirectory(_workspaceRoot);
            _settings = await InitializeStateStoreAsync();
            ApplyTheme(_settings.ThemeMode);
            Directory.CreateDirectory(_settings.ModelsRoot);
            Directory.CreateDirectory(_settings.RuntimeRoot);
            Directory.CreateDirectory(_settings.CacheRoot);
            var stateStore = _stateStore ?? throw new InvalidOperationException("State store did not initialize.");
            _jobs = new JobEngine(stateStore, Path.Combine(_workspaceRoot, "logs"));
            _catalog = new ModelCatalogService(stateStore);
            _runtimes = new RuntimeRegistryService(stateStore);
            _huggingFace = new HuggingFaceService(stateStore, _jobs, _catalog);
            _service = await StartLocalAppServiceAsync(stateStore, _jobs);
            await CleanupInterruptedRuntimeBuildJobsAsync();
            await _huggingFace.RecoverInterruptedDownloadsAsync(_settings);
            RunBackground(SeedSuggestedLaunchProfilesInBackgroundAsync, "Launch profile seeding failed");
            ShowOverview();
            await RefreshAllAsync();
            await RecoverActiveRuntimeSessionAsync();
            RunBackground(AutoSelectDetectedWslDistroAsync, "WSL distro auto-select failed");
        });
        await ShowCompletedAppUpdateNoticeAsync();
        RunBackground(CheckForAppUpdatesOnStartupAsync, "App update check failed");
    }

    private async Task<AppSettings> InitializeStateStoreAsync()
    {
        var databasePath = Path.Combine(_workspaceRoot, "state", "local-llm-console.db");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            _stateStore = new StateStore(databasePath);
            try
            {
                await _stateStore.InitializeAsync();
                var loaded = await _stateStore.GetAppSettingsAsync(_workspaceRoot);
                var settings = loaded with { WorkspaceRoot = _workspaceRoot };
                if (!string.Equals(loaded.WorkspaceRoot, _workspaceRoot, StringComparison.OrdinalIgnoreCase))
                    await _stateStore.SaveAppSettingsAsync(settings);
                return settings;
            }
            catch (SqliteException) when (attempt == 0)
            {
                await _stateStore.DisposeAsync();
                _stateStore = null;
                StateStore.QuarantineDatabaseFiles(databasePath);
            }
        }

        throw new InvalidOperationException("Unable to initialize the application state database.");
    }

    private async Task SeedSuggestedLaunchProfilesInBackgroundAsync()
    {
        try
        {
            if (_huggingFace is null) return;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var seeded = await _huggingFace.SeedSuggestedLaunchProfilesAsync(_settings, cts.Token);
            if (seeded > 0)
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    SetStatus($"Applied Hugging Face suggested launch defaults for {seeded} model{(seeded == 1 ? "" : "s")}.");
                    await RenderSelectedModelLaunchSettingsAsync();
                });
            }
        }
        catch
        {
            // Suggested settings are opportunistic; offline startup should stay quiet.
        }
    }

    private async Task<LocalAppService> StartLocalAppServiceAsync(StateStore stateStore, JobEngine jobs)
    {
        const int preferredPort = 8090;
        const int maxFallbackPort = preferredPort + 20;

        for (var port = preferredPort; port <= maxFallbackPort; port++)
        {
            var service = new LocalAppService(stateStore, jobs, port);
            try
            {
                await service.StartAsync();
                if (port != preferredPort)
                    SetStatus($"Local app service moved to 127.0.0.1:{port} because port {preferredPort} was busy.");
                return service;
            }
            catch (HttpListenerException) when (port < maxFallbackPort)
            {
                await service.DisposeAsync();
            }
            catch (SocketException) when (port < maxFallbackPort)
            {
                await service.DisposeAsync();
            }
        }

        throw new InvalidOperationException($"Could not start the local app service on 127.0.0.1:{preferredPort}-{maxFallbackPort}.");
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (_shutdownCleanupComplete) return;
            e.Cancel = true;
            if (_shutdownRequested) return;
            _shutdownRequested = true;

            if (_llama.IsRunning)
            {
                var (modelName, runtimeName) = await ActiveRuntimeLabelsAsync();
                var result = ThemedMessageBox.Show(
                    this,
                    $"{modelName}\n{runtimeName}\n\nClosing the app will stop the running model and free its runtime resources.\n\nClose and stop the model?",
                    "Model is running",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    _shutdownRequested = false;
                    return;
                }
            }

            if (_huggingFace?.ActiveDownloadCount > 0)
            {
                var downloadText = _huggingFace.ActiveDownloadCount == 1 ? "1 model download is" : $"{_huggingFace.ActiveDownloadCount} model downloads are";
                var result = ThemedMessageBox.Show(
                    this,
                    $"{downloadText} still running.\n\nClosing the app will pause active downloads and save the partial files so they can be resumed from History next time.\n\nClose and pause downloads?",
                    "Downloads in progress",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    _shutdownRequested = false;
                    return;
                }
            }

            IsEnabled = false;
            var closingStatus = _llama.IsRunning
                ? "Stopping runtime and closing..."
                : _huggingFace?.ActiveDownloadCount > 0
                    ? "Pausing active downloads and closing..."
                    : "Closing...";
            SetStatus(closingStatus);
            await ShutdownAsync();
            _shutdownCleanupComplete = true;
            Close();
        }
        catch (Exception ex)
        {
            _shutdownRequested = false;
            IsEnabled = true;
            SetStatus($"Shutdown failed: {ex.Message}");
            await WriteAppLogAsync(ex);
            ThemedMessageBox.Show(this, ex.Message, "Shutdown failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ShutdownAsync()
    {
        _downloadHistoryTimer?.Stop();
        _runtimeDashboardTimer?.Stop();
        CancelLaunchSettingsRefresh();
        StopRuntimeReadinessMonitor();
        DisposeTrayIcon();
        if (_huggingFace is not null) await _huggingFace.PauseActiveDownloadsAsync(TimeSpan.FromSeconds(10));
        _processRunner.KillTrackedProcesses();
        await CleanupActiveWslBuildsAsync();
        _llama.Dispose();
        _activeRuntimeSettings = null;
        ClearActiveRuntimeSession();
        if (_service is not null)
        {
            await _service.DisposeAsync();
            _service = null;
        }
        if (_stateStore is not null)
        {
            await _stateStore.DisposeAsync();
            _stateStore = null;
        }
    }

}
