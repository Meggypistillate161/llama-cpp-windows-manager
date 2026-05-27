namespace LocalLlmConsole.ViewModels;

public sealed class MainWindowViewModel
{
    public OverviewPageViewModel Overview { get; } = new();
    public ModelsPageViewModel Models { get; } = new();
    public RuntimesPageViewModel Runtimes { get; } = new();
    public RuntimePackagesPageViewModel RuntimePackages { get; } = new();
    public RuntimeBuildsPageViewModel RuntimeBuilds { get; } = new();
    public LifetimeMetricsViewModel LifetimeMetrics { get; } = new();
    public WindowsPageViewModel Windows { get; } = new();
    public WslLinuxPageViewModel WslLinux { get; } = new();
    public HuggingFacePageViewModel HuggingFace { get; } = new();
    public JobsViewModel Jobs { get; } = new();
    public LogsViewModel Logs { get; } = new();
    public RuntimeMetricsViewModel RuntimeMetrics { get; } = new();
    public SettingsPageViewModel Settings { get; } = new();
    public OpenCodePageViewModel OpenCode { get; } = new();
    public LaunchSettingsViewModel LaunchSettings { get; } = new();
    public UpdatesPageViewModel Updates { get; } = new();

    public string CurrentPage { get; set; } = "Overview";
    public string StatusText { get; private set; } = "Starting...";
    public bool IsBusy { get; private set; }

    public string DisplayStatusText => string.IsNullOrWhiteSpace(StatusText) ? "Ready" : StatusText;

    public void SetStatus(string text) => StatusText = text;

    public bool TryBeginBusy(out string busyMessage)
    {
        busyMessage = "";
        if (IsBusy)
        {
            busyMessage = string.IsNullOrWhiteSpace(StatusText)
                ? "Please wait for the current action to finish."
                : $"Please wait: {StatusText}";
            return false;
        }

        IsBusy = true;
        return true;
    }

    public bool EndBusy()
    {
        if (!IsBusy) return false;
        IsBusy = false;
        return true;
    }
}
