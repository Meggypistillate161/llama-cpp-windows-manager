namespace LocalLlmConsole.Services;

public sealed class VisibleCommandLaunchService
{
    private readonly Action<ProcessStartInfo> _startProcess;
    private readonly Func<string> _wslExe;

    public VisibleCommandLaunchService(
        Action<ProcessStartInfo>? startProcess = null,
        Func<string>? wslExe = null)
    {
        _startProcess = startProcess ?? (processStartInfo => Process.Start(processStartInfo));
        _wslExe = wslExe ?? HostExecutableResolver.WslExe;
    }

    public void StartVisibleWindowsCommand(string executable, IEnumerable<string> args, bool elevated)
    {
        var command = string.Join(" ", new[] { CommandLineService.PowerShellQuote(executable) }
            .Concat(args.Select(CommandLineService.PowerShellQuote)));
        StartVisiblePowerShellScript($"& {command}", elevated);
    }

    public void StartVisibleWslBashScript(string distro, string bashScript, bool elevated)
        => StartVisiblePowerShellScript(CommandLineService.PowerShellWslBashScriptCommand(_wslExe(), distro, bashScript), elevated);

    public void StartVisiblePowerShellScript(string command, bool elevated)
        => _startProcess(CommandLineService.VisiblePowerShellStartInfo(command, elevated));
}
