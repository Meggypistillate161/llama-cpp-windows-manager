namespace LocalLlmConsole.Services;

public sealed record WslRuntimeStopRequest(
    AppSettings Settings,
    string ExecutablePath,
    string ProcessMarker);

public sealed class WslRuntimeStopService
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);
    private readonly IProcessRunner _processRunner;
    private readonly Func<string> _wslExe;

    public WslRuntimeStopService(
        IProcessRunner processRunner,
        Func<string>? wslExe = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _wslExe = wslExe ?? HostExecutableResolver.WslExe;
    }

    public void Stop(WslRuntimeStopRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Settings.WslDistro))
            return;

        try
        {
            var command = BuildStopCommand(request.ExecutablePath, request.Settings.Port, request.ProcessMarker);
            if (string.IsNullOrWhiteSpace(command)) return;

            _ = _processRunner.RunAsync(
                    BuildStopStartInfo(_wslExe(), request.Settings.WslDistro, command),
                    StopTimeout)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // Best-effort cleanup only; the Windows parent process is already stopped or stopping.
        }
    }

    public static ProcessStartInfo BuildStopStartInfo(string wslExe, string distro, string command)
    {
        var psi = new ProcessStartInfo(wslExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var arg in new[] { "-d", distro, "--", "bash", "-lc", command })
            psi.ArgumentList.Add(arg);
        return psi;
    }

    public static string BuildStopCommand(string executablePath, int port, string processMarker)
        => !string.IsNullOrWhiteSpace(processMarker)
            ? WslKillByMarkerCommand(processMarker)
            : WslKillByExecutableAndPortCommand(executablePath, port);

    public static string WslKillByExecutableAndPortCommand(string executablePath, int port)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return "";
        var executable = executablePath.Replace('\\', '/');
        return string.Join(" ", new[]
        {
            "for pid in $(pgrep -f -- llama-server 2>/dev/null); do",
            "cmd=$(tr '\\0' ' ' < /proc/$pid/cmdline 2>/dev/null || true);",
            $"case \"$cmd\" in *{CommandLineService.BashQuote(executable)}*\"--port\"*{CommandLineService.BashQuote(port.ToString(CultureInfo.InvariantCulture))}*) kill \"$pid\" 2>/dev/null || true;; esac;",
            "done"
        });
    }

    public static string WslKillByMarkerCommand(string processMarker)
    {
        var marker = CommandLineService.BashQuote(processMarker);
        return string.Join(" ", new[]
        {
            $"marker={marker};",
            "for cmdline in /proc/[0-9]*/cmdline; do",
            "test -r \"$cmdline\" || continue;",
            "cmd=$(tr '\\0' ' ' < \"$cmdline\" 2>/dev/null || true);",
            "case \"$cmd\" in *\"$marker\"*) pid=${cmdline#/proc/}; pid=${pid%/cmdline}; kill \"$pid\" 2>/dev/null || true;; esac;",
            "done"
        });
    }
}
