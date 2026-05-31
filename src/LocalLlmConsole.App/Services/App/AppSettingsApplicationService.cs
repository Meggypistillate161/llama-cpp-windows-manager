namespace LocalLlmConsole.Services;

public sealed record AppSettingsSaveApplicationRequest(
    AppSettings CurrentSettings,
    string ThemeMode,
    IReadOnlyDictionary<string, string> Values,
    IEnumerable<LoadedModelSessionSnapshot> Sessions);

public sealed record AppSettingsOpenCodeSyncApplicationRequest(
    AppSettings Settings,
    OpenCodeLaunchProfileReader ReadProfileAsync,
    OpenCodeModelLimitsResolver ResolveLimitsAsync);

public sealed record AppSettingsOpenCodeSyncApplicationActions(
    Action<OpenCodeFileSet> SetFileSet,
    Func<bool> IsOpenCodePageActive,
    Func<Task> RefreshOpenCodeAsync,
    Func<Task> UpdateOpenCodeHealthAsync,
    Func<Exception, Task> WriteLogAsync);

public enum AppSettingsSaveApplicationOutcome
{
    Failed,
    Saved,
    SavedWithGeneratedApiKey
}

public enum AppSettingsOpenCodeSyncApplicationOutcome
{
    Skipped,
    Applied,
    Failed
}

public sealed record AppSettingsSaveApplicationActions(
    Action<AppSettings> ApplySettings,
    Action<string> ApplyTheme,
    Action ApplyLaunchSettingsToControls,
    Func<Task> RestartGatewayAsync,
    Func<AppSettings, Task> SyncOpenCodeAsync,
    Func<bool> IsSettingsPageActive,
    Action RefreshSettingsPage,
    Action<string> SetStatus);

public sealed class AppSettingsApplicationService
{
    private readonly AppSettingsWorkflowService _settingsWorkflow;
    private readonly OpenCodeSettingsSyncService _openCodeSettingsSync;
    private readonly StateStore _stateStore;
    private readonly WindowsStartupRegistrationService _startupRegistration;

    public AppSettingsApplicationService(
        AppSettingsWorkflowService settingsWorkflow,
        OpenCodeSettingsSyncService openCodeSettingsSync,
        StateStore stateStore,
        WindowsStartupRegistrationService startupRegistration)
    {
        _settingsWorkflow = settingsWorkflow ?? throw new ArgumentNullException(nameof(settingsWorkflow));
        _openCodeSettingsSync = openCodeSettingsSync ?? throw new ArgumentNullException(nameof(openCodeSettingsSync));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _startupRegistration = startupRegistration ?? throw new ArgumentNullException(nameof(startupRegistration));
    }

    public Task<AppSettingsUpdateResult> SaveEditedAsync(
        AppSettingsSaveApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Values);
        ArgumentNullException.ThrowIfNull(request.Sessions);

        var runningModelPorts = request.Sessions
            .Where(session => session.IsRunning)
            .Select(session => session.LaunchSettings.Port)
            .ToHashSet();

        return _settingsWorkflow.SaveEditedAsync(new AppSettingsSaveWorkflowRequest(
            request.CurrentSettings,
            request.ThemeMode,
            request.Values,
            runningModelPorts), cancellationToken);
    }

    public async Task<AppSettingsSaveApplicationOutcome> SaveEditedAndApplyAsync(
        AppSettingsSaveApplicationRequest request,
        AppSettingsSaveApplicationActions actions,
        CancellationToken cancellationToken = default)
    {
        Validate(actions);

        var result = await SaveEditedAsync(request, cancellationToken);
        if (!result.Success)
        {
            actions.SetStatus(result.StatusMessage);
            return AppSettingsSaveApplicationOutcome.Failed;
        }

        var startupRegistration = _startupRegistration.Apply(result.Settings.StartWithWindows);
        actions.ApplySettings(result.Settings);
        actions.ApplyTheme(result.Settings.ThemeMode);
        actions.ApplyLaunchSettingsToControls();
        await actions.RestartGatewayAsync();
        await actions.SyncOpenCodeAsync(result.Settings);
        var status = result.GeneratedApiKey ? "Settings saved. A model API key was generated." : "Settings saved.";
        if (!startupRegistration.Success)
            status = $"{status} {startupRegistration.StatusMessage}";
        actions.SetStatus(status);
        if (actions.IsSettingsPageActive())
            actions.RefreshSettingsPage();

        return result.GeneratedApiKey
            ? AppSettingsSaveApplicationOutcome.SavedWithGeneratedApiKey
            : AppSettingsSaveApplicationOutcome.Saved;
    }

    public Task<AppSettingsEnsureApiKeyResult> EnsureModelApiKeyAsync(
        AppSettings persistedSettings,
        AppSettings targetSettings,
        CancellationToken cancellationToken = default)
        => _settingsWorkflow.EnsureModelApiKeyAsync(persistedSettings, targetSettings, cancellationToken);

    public Task<AppSettings> PersistAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => _settingsWorkflow.PersistAsync(settings, cancellationToken);

    public async Task<OpenCodeSettingsSyncResult> SyncOpenCodeLocalProviderAsync(
        AppSettingsOpenCodeSyncApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ReadProfileAsync);
        ArgumentNullException.ThrowIfNull(request.ResolveLimitsAsync);

        var gatewayModels = request.Settings.AutoLoadGatewayEnabled
            ? await _stateStore.ListModelsAsync()
            : null;
        return await _openCodeSettingsSync.SyncAsync(new OpenCodeSettingsSyncRequest(
            request.Settings,
            gatewayModels,
            request.ReadProfileAsync,
            request.ResolveLimitsAsync), cancellationToken);
    }

    public async Task<AppSettingsOpenCodeSyncApplicationOutcome> SyncOpenCodeLocalProviderAndApplyAsync(
        AppSettingsOpenCodeSyncApplicationRequest request,
        AppSettingsOpenCodeSyncApplicationActions actions,
        CancellationToken cancellationToken = default)
    {
        Validate(actions);

        try
        {
            var result = await SyncOpenCodeLocalProviderAsync(request, cancellationToken);
            if (!result.Completed || result.FileSet is null)
                return AppSettingsOpenCodeSyncApplicationOutcome.Skipped;

            actions.SetFileSet(result.FileSet);
            if (actions.IsOpenCodePageActive())
                await actions.RefreshOpenCodeAsync();
            else
                await actions.UpdateOpenCodeHealthAsync();

            return AppSettingsOpenCodeSyncApplicationOutcome.Applied;
        }
        catch (Exception ex)
        {
            await actions.WriteLogAsync(ex);
            return AppSettingsOpenCodeSyncApplicationOutcome.Failed;
        }
    }

    private static void Validate(AppSettingsSaveApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ApplySettings);
        ArgumentNullException.ThrowIfNull(actions.ApplyTheme);
        ArgumentNullException.ThrowIfNull(actions.ApplyLaunchSettingsToControls);
        ArgumentNullException.ThrowIfNull(actions.RestartGatewayAsync);
        ArgumentNullException.ThrowIfNull(actions.SyncOpenCodeAsync);
        ArgumentNullException.ThrowIfNull(actions.IsSettingsPageActive);
        ArgumentNullException.ThrowIfNull(actions.RefreshSettingsPage);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }

    private static void Validate(AppSettingsOpenCodeSyncApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.SetFileSet);
        ArgumentNullException.ThrowIfNull(actions.IsOpenCodePageActive);
        ArgumentNullException.ThrowIfNull(actions.RefreshOpenCodeAsync);
        ArgumentNullException.ThrowIfNull(actions.UpdateOpenCodeHealthAsync);
        ArgumentNullException.ThrowIfNull(actions.WriteLogAsync);
    }
}
