namespace LocalLlmConsole.Services;

public sealed class AdvancedSectionStateController
{
    public bool ShowLaunchSettings { get; private set; }

    public bool ShowRuntimes { get; private set; }

    public void SetLaunchSettings(bool show)
        => ShowLaunchSettings = show;

    public bool ToggleRuntimes()
    {
        ShowRuntimes = !ShowRuntimes;
        return ShowRuntimes;
    }

    public void SetRuntimes(bool show)
        => ShowRuntimes = show;
}
