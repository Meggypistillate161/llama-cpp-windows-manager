namespace LocalLlmConsole.Services;

public enum HuggingFaceSearchApplicationOutcome
{
    Searched
}

public sealed record HuggingFaceSearchApplicationActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    Action ConfigureSearchGrid,
    Func<Task<HuggingFaceInstallInventory>> InstalledInventoryAsync,
    Func<string, Task<IReadOnlyList<HuggingFaceFile>>> SearchAsync,
    Action<IReadOnlyList<HuggingFaceFile>, HuggingFaceInstallInventory, string> ApplySearchResults);

public sealed class HuggingFaceSearchApplicationService
{
    public async Task<HuggingFaceSearchApplicationOutcome> SearchAsync(
        string query,
        AppSettings settings,
        HuggingFaceSearchApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(actions);

        await actions.RunBusyAsync("Searching Hugging Face...", async () =>
        {
            actions.ConfigureSearchGrid();
            var installed = await actions.InstalledInventoryAsync();
            var results = await actions.SearchAsync(query);
            actions.ApplySearchResults(results, installed, settings.ModelsRoot);
        });
        return HuggingFaceSearchApplicationOutcome.Searched;
    }

    private static void Validate(HuggingFaceSearchApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.ConfigureSearchGrid);
        ArgumentNullException.ThrowIfNull(actions.InstalledInventoryAsync);
        ArgumentNullException.ThrowIfNull(actions.SearchAsync);
        ArgumentNullException.ThrowIfNull(actions.ApplySearchResults);
    }
}
