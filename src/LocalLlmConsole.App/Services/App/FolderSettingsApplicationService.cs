namespace LocalLlmConsole.Services;

public enum FolderSettingsApplicationOutcome
{
    Cancelled,
    Applied
}

public sealed record FolderSettingsApplicationResult(
    FolderSettingsApplicationOutcome Outcome,
    AppSettings Settings);

public sealed record FolderSettingsApplicationActions(
    Func<string, string?> PickFolder,
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<AppSettings, Task<AppSettings>> PersistSettingsAsync,
    Func<string, Task> ScanModelsAsync,
    Func<string, Task> ScanRuntimesAsync,
    Func<Task> RefreshAllAsync,
    Func<bool> IsModelsPage,
    Func<bool> IsRuntimesPage,
    Func<bool> IsSettingsPage,
    Action ShowModels,
    Action ShowRuntimes,
    Action ShowSettings,
    Action<string> SetStatus);

public sealed class FolderSettingsApplicationService
{
    public async Task<FolderSettingsApplicationResult> ChooseModelsFolderAsync(
        AppSettings settings,
        bool scanAfter,
        FolderSettingsApplicationActions actions)
    {
        Validate(actions);
        var folder = actions.PickFolder(settings.ModelsRoot);
        if (folder is null)
            return new FolderSettingsApplicationResult(FolderSettingsApplicationOutcome.Cancelled, settings);

        var next = settings with { ModelsRoot = Path.GetFullPath(folder) };
        await actions.RunBusyAsync("Changing models folder...", async () =>
        {
            next = await actions.PersistSettingsAsync(next);
            if (scanAfter)
                await actions.ScanModelsAsync(next.ModelsRoot);

            await actions.RefreshAllAsync();
            if (actions.IsModelsPage()) actions.ShowModels();
            if (actions.IsSettingsPage()) actions.ShowSettings();
            actions.SetStatus($"Models folder set to {next.ModelsRoot}");
        });

        return new FolderSettingsApplicationResult(FolderSettingsApplicationOutcome.Applied, next);
    }

    public async Task<FolderSettingsApplicationResult> ChooseRuntimeFolderAsync(
        AppSettings settings,
        bool scanAfter,
        FolderSettingsApplicationActions actions)
    {
        Validate(actions);
        var folder = actions.PickFolder(settings.RuntimeRoot);
        if (folder is null)
            return new FolderSettingsApplicationResult(FolderSettingsApplicationOutcome.Cancelled, settings);

        var next = settings with { RuntimeRoot = Path.GetFullPath(folder) };
        await actions.RunBusyAsync("Changing runtimes folder...", async () =>
        {
            next = await actions.PersistSettingsAsync(next);
            if (scanAfter)
                await actions.ScanRuntimesAsync(next.RuntimeRoot);

            await actions.RefreshAllAsync();
            if (actions.IsRuntimesPage()) actions.ShowRuntimes();
            if (actions.IsSettingsPage()) actions.ShowSettings();
            actions.SetStatus($"Runtimes folder set to {next.RuntimeRoot}");
        });

        return new FolderSettingsApplicationResult(FolderSettingsApplicationOutcome.Applied, next);
    }

    private static void Validate(FolderSettingsApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.PickFolder);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.PersistSettingsAsync);
        ArgumentNullException.ThrowIfNull(actions.ScanModelsAsync);
        ArgumentNullException.ThrowIfNull(actions.ScanRuntimesAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshAllAsync);
        ArgumentNullException.ThrowIfNull(actions.IsModelsPage);
        ArgumentNullException.ThrowIfNull(actions.IsRuntimesPage);
        ArgumentNullException.ThrowIfNull(actions.IsSettingsPage);
        ArgumentNullException.ThrowIfNull(actions.ShowModels);
        ArgumentNullException.ThrowIfNull(actions.ShowRuntimes);
        ArgumentNullException.ThrowIfNull(actions.ShowSettings);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
