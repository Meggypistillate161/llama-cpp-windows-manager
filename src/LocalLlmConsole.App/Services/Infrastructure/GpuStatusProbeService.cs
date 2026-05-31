namespace LocalLlmConsole.Services;

public sealed class GpuStatusProbeService
{
    private readonly IProcessRunner _processRunner;
    private readonly Func<string> _findWindowsSyclLs;

    public GpuStatusProbeService(
        IProcessRunner processRunner,
        Func<string>? findWindowsSyclLs = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _findWindowsSyclLs = findWindowsSyclLs ?? FindWindowsSyclLs;
    }

    public async Task<VramMemorySnapshot?> MemoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processRunner.RunAsync(
                NvidiaSmiStartInfo(
                    "--query-gpu=memory.free,memory.total",
                    "--format=csv,noheader,nounits"),
                TimeSpan.FromSeconds(2),
                cancellationToken);
            if (result.ExitCode != 0) return null;

            return result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(GpuStatusService.ParseMemoryLine)
                .Where(snapshot => snapshot is not null)
                .Select(snapshot => snapshot!)
                .OrderByDescending(snapshot => snapshot.FreeGiB)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> SummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processRunner.RunAsync(
                NvidiaSmiStartInfo(
                    "--query-gpu=index,name,utilization.gpu,temperature.gpu,memory.used,memory.total",
                    "--format=csv,noheader,nounits"),
                TimeSpan.FromSeconds(2),
                cancellationToken);
            if (result.ExitCode != 0) return "Unavailable";

            var lines = result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(GpuStatusService.FormatNvidiaSmiCsvLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(4)
                .ToArray();
            return lines.Length == 0 ? "Unavailable" : string.Join(Environment.NewLine, lines);
        }
        catch
        {
            return "Unavailable";
        }
    }

    public async Task<string> WindowsIntelArcSummaryAsync(CancellationToken cancellationToken = default)
    {
        var syclLs = _findWindowsSyclLs();
        if (string.IsNullOrWhiteSpace(syclLs)) return "Unavailable";
        try
        {
            var output = await RunProcessOutputAsync(syclLs, [], TimeSpan.FromSeconds(3), cancellationToken);
            var line = GpuStatusService.FirstSyclGpuLine(output);
            return string.IsNullOrWhiteSpace(line) ? "Unavailable" : GpuStatusService.FormatIntelArcStatus(line);
        }
        catch
        {
            return "Unavailable";
        }
    }

    public async Task<string> WslIntelArcSummaryAsync(string wslExe, string wslDistro, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wslExe) || string.IsNullOrWhiteSpace(wslDistro)) return "Unavailable";
        try
        {
            var output = await RunProcessOutputAsync(
                wslExe,
                ["-d", wslDistro, "--", "bash", "-lc", "source /opt/intel/oneapi/setvars.sh --force >/dev/null 2>&1 || true; sycl-ls 2>/dev/null | grep -i 'level_zero.*gpu' | head -n 1"],
                TimeSpan.FromSeconds(3),
                cancellationToken);
            var line = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
            return string.IsNullOrWhiteSpace(line) ? "Unavailable" : GpuStatusService.FormatIntelArcStatus(line);
        }
        catch
        {
            return "Unavailable";
        }
    }

    private async Task<string> RunProcessOutputAsync(
        string fileName,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(fileName);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        var result = await _processRunner.RunAsync(psi, timeout, cancellationToken);
        return result.ExitCode == 0 ? result.Output : "";
    }

    private static ProcessStartInfo NvidiaSmiStartInfo(params string[] args)
    {
        var psi = new ProcessStartInfo(HostExecutableResolver.NvidiaSmiExe());
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        return psi;
    }

    private static string FindWindowsSyclLs()
    {
        foreach (var directory in WindowsEnvironmentService.OneApiPathEntries().Concat(PathEntries()))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var candidate = Path.Combine(directory, "sycl-ls.exe");
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }

        return "";
    }

    private static IEnumerable<string> PathEntries()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var part in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var expanded = Environment.ExpandEnvironmentVariables(part.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(expanded) || !Directory.Exists(expanded)) continue;
            yield return Path.GetFullPath(expanded);
        }
    }
}
