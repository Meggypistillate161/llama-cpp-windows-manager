
namespace LocalLlmConsole.Services;

public sealed partial class LlamaProcessSupervisor : IDisposable
{
    private static void TryStopWslLlama(AppSettings settings, string executablePath, string processMarker)
    {
        try
        {
            var command = !string.IsNullOrWhiteSpace(processMarker)
                ? WslKillByMarkerCommand(processMarker)
                : WslKillByExecutableAndPortCommand(executablePath, settings.Port);
            if (string.IsNullOrWhiteSpace(command)) return;

            using var stop = Process.Start(new ProcessStartInfo(HostExecutableResolver.WslExe())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                ArgumentList = { "-d", settings.WslDistro, "--", "bash", "-lc", command }
            });
            if (stop is not null && !stop.WaitForExit(3000))
            {
                try { stop.Kill(entireProcessTree: true); } catch { }
            }
        }
        catch { }
    }

    private static string WslKillByExecutableAndPortCommand(string executablePath, int port)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return "";
        var executable = executablePath.Replace('\\', '/');
        return string.Join(" ", new[]
        {
            "for pid in $(pgrep -f -- llama-server 2>/dev/null); do",
            "cmd=$(tr '\\0' ' ' < /proc/$pid/cmdline 2>/dev/null || true);",
            $"case \"$cmd\" in *{BashQuote(executable)}*\"--port\"*{BashQuote(port.ToString(System.Globalization.CultureInfo.InvariantCulture))}*) kill \"$pid\" 2>/dev/null || true;; esac;",
            "done"
        });
    }

    private static string WslKillByMarkerCommand(string processMarker)
    {
        var marker = BashQuote(processMarker);
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

    private static string ToWslPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (value.StartsWith('/')) return value.Replace('\\', '/');
        var full = Path.GetFullPath(value);
        if (full.Length >= 3 && full[1] == ':' && (full[2] == '\\' || full[2] == '/'))
        {
            var drive = char.ToLowerInvariant(full[0]);
            var rest = full[3..].Replace('\\', '/');
            return $"/mnt/{drive}/{rest}";
        }
        return full.Replace('\\', '/');
    }

    private static string WslDirectoryName(string path)
    {
        var normalized = (path ?? "").Replace('\\', '/').TrimEnd('/');
        var split = normalized.LastIndexOf('/');
        return split <= 0 ? "" : normalized[..split];
    }

    private static string WslSiblingDirectory(string path, string sibling)
    {
        var parent = WslDirectoryName(path);
        return string.IsNullOrWhiteSpace(parent) ? sibling : $"{parent.TrimEnd('/')}/{sibling}";
    }

    private static string BashQuote(string value)
        => "'" + (value ?? "").Replace("'", "'\"'\"'") + "'";
}
