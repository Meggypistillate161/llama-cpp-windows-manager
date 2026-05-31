namespace LocalLlmConsole.Services;

public sealed record AppStartupApplicationRequest(
    string WorkspaceRoot,
    string DatabasePath,
    Func<StateStore> CreateStateStore,
    Func<StateStore, MainWindowLoadedServices> CreateLoadedServices,
    Func<StateStore, JobEngine, int, ILocalAppServiceHost> CreateLocalService,
    int PreferredLocalServicePort = 8090,
    int MaxLocalServiceFallbackPort = 8110);

public sealed record AppStartupApplicationActions(
    Action<StateStore> ApplyStateStore,
    Action<AppSettings> ApplySettings,
    Action<MainWindowLoadedServices> ApplyLoadedServices,
    Action<ILocalAppServiceHost> ApplyLocalService,
    Action<string> SetStatus);

public sealed record AppStartupApplicationResult(
    StateStore StateStore,
    AppSettings Settings,
    MainWindowLoadedServices LoadedServices,
    ILocalAppServiceHost LocalService,
    int LocalServicePort,
    string LocalServiceStatusMessage);

public sealed class AppStartupApplicationService
{
    private readonly StateStoreInitializationService _stateStoreInitialization;
    private readonly LocalAppServiceStartupService _localAppStartup;
    private readonly RuntimeBuildMarkerService _runtimeBuildMarkers;
    private readonly WindowsStartupRegistrationService _startupRegistration;

    public AppStartupApplicationService(
        StateStoreInitializationService stateStoreInitialization,
        LocalAppServiceStartupService localAppStartup,
        RuntimeBuildMarkerService runtimeBuildMarkers,
        WindowsStartupRegistrationService startupRegistration)
    {
        _stateStoreInitialization = stateStoreInitialization ?? throw new ArgumentNullException(nameof(stateStoreInitialization));
        _localAppStartup = localAppStartup ?? throw new ArgumentNullException(nameof(localAppStartup));
        _runtimeBuildMarkers = runtimeBuildMarkers ?? throw new ArgumentNullException(nameof(runtimeBuildMarkers));
        _startupRegistration = startupRegistration ?? throw new ArgumentNullException(nameof(startupRegistration));
    }

    public async Task<AppStartupApplicationResult> StartAsync(
        AppStartupApplicationRequest request,
        AppStartupApplicationActions actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.CreateStateStore);
        ArgumentNullException.ThrowIfNull(request.CreateLoadedServices);
        ArgumentNullException.ThrowIfNull(request.CreateLocalService);
        ArgumentNullException.ThrowIfNull(actions.ApplyStateStore);
        ArgumentNullException.ThrowIfNull(actions.ApplySettings);
        ArgumentNullException.ThrowIfNull(actions.ApplyLoadedServices);
        ArgumentNullException.ThrowIfNull(actions.ApplyLocalService);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(request.WorkspaceRoot);
        var initialized = await _stateStoreInitialization.InitializeAsync(new StateStoreInitializationRequest(
            request.WorkspaceRoot,
            request.DatabasePath,
            request.CreateStateStore));
        actions.ApplyStateStore(initialized.StateStore);
        var settings = _startupRegistration.Reconcile(initialized.Settings);
        if (settings.StartWithWindows != initialized.Settings.StartWithWindows)
            await initialized.StateStore.SaveAppSettingsAsync(settings);
        actions.ApplySettings(settings);

        Directory.CreateDirectory(settings.ModelsRoot);
        Directory.CreateDirectory(settings.RuntimeRoot);
        Directory.CreateDirectory(settings.CacheRoot);

        cancellationToken.ThrowIfCancellationRequested();
        var loadedServices = request.CreateLoadedServices(initialized.StateStore);
        actions.ApplyLoadedServices(loadedServices);

        var localServiceStartup = await _localAppStartup.StartAsync(new LocalAppServiceStartupRequest(
            request.PreferredLocalServicePort,
            request.MaxLocalServiceFallbackPort,
            port => request.CreateLocalService(initialized.StateStore, loadedServices.App.Jobs, port)));
        actions.ApplyLocalService(localServiceStartup.Service);
        if (!string.IsNullOrWhiteSpace(localServiceStartup.StatusMessage))
            actions.SetStatus(localServiceStartup.StatusMessage);

        cancellationToken.ThrowIfCancellationRequested();
        await _runtimeBuildMarkers.CleanupInterruptedJobsAsync(
            await initialized.StateStore.ListJobsAsync(),
            settings.WslDistro);
        await loadedServices.App.HuggingFace.RecoverInterruptedDownloadsAsync(settings);

        return new AppStartupApplicationResult(
            initialized.StateStore,
            settings,
            loadedServices,
            localServiceStartup.Service,
            localServiceStartup.Port,
            localServiceStartup.StatusMessage);
    }
}
