namespace LocalLlmConsole.Services;

public sealed record WslPageRefreshResult(
    WslEnvironmentReport Report,
    WslToolSnapshot Tools,
    AppSettings Settings,
    bool SettingsChanged);

public sealed class WslPageWorkflowService
{
    private static readonly TimeSpan ToolProbeTimeout = TimeSpan.FromSeconds(15);

    private readonly Func<CancellationToken, Task<WslEnvironmentReport>> _detectWsl;
    private readonly IProcessRunner _processRunner;
    private readonly Func<string> _wslExe;

    public WslPageWorkflowService(
        WslEnvironmentService wslEnvironment,
        IProcessRunner processRunner)
        : this(wslEnvironment.DetectAsync, processRunner, HostExecutableResolver.WslExe)
    {
    }

    public WslPageWorkflowService(
        Func<CancellationToken, Task<WslEnvironmentReport>> detectWsl,
        IProcessRunner processRunner,
        Func<string>? wslExe = null)
    {
        _detectWsl = detectWsl ?? throw new ArgumentNullException(nameof(detectWsl));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _wslExe = wslExe ?? HostExecutableResolver.WslExe;
    }

    public async Task<WslPageRefreshResult> RefreshAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var report = await _detectWsl(cancellationToken);
        var updatedSettings = ApplyDetectedDistro(settings, report);
        var tools = await DetectSelectedToolsAsync(report, updatedSettings, cancellationToken);
        return new WslPageRefreshResult(
            report,
            tools,
            updatedSettings,
            !string.Equals(settings.WslDistro, updatedSettings.WslDistro, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<(WslEnvironmentReport Report, AppSettings Settings, bool SettingsChanged)> DetectRecommendedDistroAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var report = await _detectWsl(cancellationToken);
        var updatedSettings = ApplyDetectedDistro(settings, report);
        return (
            report,
            updatedSettings,
            !string.Equals(settings.WslDistro, updatedSettings.WslDistro, StringComparison.OrdinalIgnoreCase));
    }

    public static AppSettings ApplyDetectedDistro(AppSettings settings, WslEnvironmentReport report)
    {
        if (report.Distros.Count == 0)
            return settings;
        if (report.Distros.Any(distro => distro.Name.Equals(settings.WslDistro, StringComparison.OrdinalIgnoreCase)))
            return settings;

        var detected = report.RecommendedDistro;
        if (string.IsNullOrWhiteSpace(detected))
            return settings;
        if (!report.Distros.Any(distro => distro.Name.Equals(detected, StringComparison.OrdinalIgnoreCase)))
            return settings;

        return settings with { WslDistro = detected };
    }

    private async Task<WslToolSnapshot> DetectSelectedToolsAsync(
        WslEnvironmentReport report,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var distro = WslEnvironmentService.SelectedUbuntuDistroName(report, settings.WslDistro);
        if (!report.WslExeFound || !report.WslWorking || string.IsNullOrWhiteSpace(distro))
            return WslEnvironmentService.UnknownToolSnapshot();

        var psi = new ProcessStartInfo(_wslExe())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] { "-d", distro, "--", "bash", "-s" })
            psi.ArgumentList.Add(arg);

        try
        {
            var result = await _processRunner.RunAsync(
                psi,
                ToolProbeTimeout,
                cancellationToken,
                standardInput: WslSetupCommands.ToolProbeCommand);
            if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.Output))
                return WslEnvironmentService.UnknownToolSnapshot();

            return WslEnvironmentService.ParseToolProbeOutput(result.Output);
        }
        catch
        {
            return WslEnvironmentService.UnknownToolSnapshot();
        }
    }
}
