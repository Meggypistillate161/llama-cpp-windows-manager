namespace LocalLlmConsole.Services;

public sealed record ModelLaunchSettingsViewState(
    string ModelId,
    ModelLaunchSettings? SavedProfile,
    bool HasSavedProfile,
    string RuntimeId,
    AppSettings LaunchSettings);

public sealed record ModelLaunchSettingsSaveResult(
    ModelLaunchSettings SavedSettings,
    string StatusMessage);

public sealed record LaunchDefaultsSaveResult(
    AppSettings Settings,
    string StatusMessage);

public sealed class ModelLaunchSettingsWorkflowService
{
    private readonly ModelLaunchProfileService _profiles;

    public ModelLaunchSettingsWorkflowService(ModelLaunchProfileService profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public async Task<ModelLaunchSettingsViewState> BuildAsync(
        ModelRecord model,
        AppSettings defaults,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        var profile = await _profiles.ReadAsync(model);
        cancellationToken.ThrowIfCancellationRequested();

        var effective = profile ?? await _profiles.DraftAsync(model, defaults);
        cancellationToken.ThrowIfCancellationRequested();

        return new ModelLaunchSettingsViewState(
            model.Id,
            profile,
            profile is not null,
            effective.RuntimeId,
            effective.ApplyTo(defaults));
    }

    public async Task<ModelLaunchSettings?> EnsureAsync(
        ModelRecord model,
        AppSettings defaults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _profiles.EnsureAsync(model, defaults);
    }

    public async Task<ModelLaunchSettings> SaveForModelAsync(
        ModelRecord model,
        AppSettings launchSettings,
        string runtimeId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var saved = ModelLaunchSettings.FromAppSettings(launchSettings, runtimeId);
        await _profiles.SaveAsync(model, saved);
        cancellationToken.ThrowIfCancellationRequested();
        return saved;
    }

    public async Task<ModelLaunchSettingsSaveResult> SaveProfileAsync(
        ModelRecord model,
        AppSettings launchSettings,
        string runtimeId,
        CancellationToken cancellationToken = default)
    {
        var saved = await SaveForModelAsync(model, launchSettings, runtimeId, cancellationToken);
        return new ModelLaunchSettingsSaveResult(
            saved,
            $"Launch profile saved for {model.Name}.");
    }

    public static AppSettings ApplyLaunchDefaults(AppSettings currentSettings, AppSettings launchDefaults)
        => launchDefaults with { Port = currentSettings.Port };

    public static LaunchDefaultsSaveResult SaveLaunchDefaults(AppSettings currentSettings, AppSettings launchDefaults)
        => new(
            ApplyLaunchDefaults(currentSettings, launchDefaults),
            "Launch defaults saved. Model ports stay per-model.");
}
