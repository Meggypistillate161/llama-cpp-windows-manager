
namespace LocalLlmConsole.ViewModels;

public sealed class UpdatesPageViewModel
{
    public AppUpdateInfo? LatestUpdate { get; private set; }
    public bool CheckInFlight { get; set; }

    public bool HasAvailableUpdate => LatestUpdate is { IsAvailable: true };
    public string ActionText => HasAvailableUpdate ? "Install Update" : "Check For Updates";
    public string NavigationText => ActionText;

    public string StatusText => LatestUpdate is null
        ? "No update check has run in this session yet."
        : LatestUpdate.IsAvailable
            ? $"Update available: {LatestUpdate.CurrentVersion} -> {LatestUpdate.LatestVersion}"
            : $"No updates available. Current version: {LatestUpdate.CurrentVersion}";

    public string StatusDetails => $"{StatusText}\nRepository: {AppUpdateService.RepositoryUrl}";

    public string LatestReleaseText => LatestUpdate is { IsAvailable: true } update
        ? $"{update.ReleaseName}\n{update.HtmlUrl}\n\n{DisplayFormatService.TrimForDisplay(update.ReleaseNotes, 1800)}"
        : "";

    public void SetLatestUpdate(AppUpdateInfo update) => LatestUpdate = update;
}
