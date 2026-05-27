
namespace LocalLlmConsole.Services;

public static class GpuStatusService
{
    public static async Task<VramMemorySnapshot?> MemoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo(HostExecutableResolver.NvidiaSmiExe())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("--query-gpu=memory.free,memory.total");
            psi.ArgumentList.Add("--format=csv,noheader,nounits");
            using var process = Process.Start(psi);
            if (process is null) return null;
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (TimeoutException)
            {
                TryKillProcessTree(process);
                return null;
            }
            var output = await outputTask;
            if (process.ExitCode != 0) return null;
            var best = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseMemoryLine)
                .Where(snapshot => snapshot is not null)
                .Select(snapshot => snapshot!)
                .OrderByDescending(snapshot => snapshot.FreeGiB)
                .FirstOrDefault();
            return best;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> SummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo(HostExecutableResolver.NvidiaSmiExe())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("--query-gpu=index,name,utilization.gpu,temperature.gpu,memory.used,memory.total");
            psi.ArgumentList.Add("--format=csv,noheader,nounits");
            using var process = Process.Start(psi);
            if (process is null) return "Unavailable";
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (TimeoutException)
            {
                TryKillProcessTree(process);
                return "Unavailable";
            }
            var output = await outputTask;
            if (process.ExitCode != 0) return "Unavailable";
            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(FormatNvidiaSmiCsvLine)
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

    public static string FormatNvidiaSmiCsvLine(string line)
    {
        var parts = line.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length < 6) return "";
        var index = parts[0];
        var utilization = parts[2];
        var temperature = parts[3];
        var used = double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var usedMb) ? usedMb / 1024 : 0;
        var total = double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var totalMb) ? totalMb / 1024 : 0;
        var memory = total > 0 ? $"{used:0.0}/{total:0.0} GiB" : $"{parts[4]}/{parts[5]} MiB";
        return $"GPU {index}: {utilization}% | {temperature}C | {memory}";
    }

    public static string FormatIntelArcStatus(string? syclLsLine)
    {
        if (string.IsNullOrWhiteSpace(syclLsLine))
            return "Intel Arc GPU";

        var text = syclLsLine.Trim();
        var lastBracket = text.LastIndexOf(']');
        if (lastBracket >= 0 && lastBracket + 1 < text.Length)
            text = text[(lastBracket + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(text)) return "Intel Arc GPU";
        return text.Length > 96 ? $"{text[..93]}..." : text;
    }

    public static async Task<string> WindowsIntelArcSummaryAsync(CancellationToken cancellationToken = default)
    {
        var syclLs = FindWindowsSyclLs();
        if (string.IsNullOrWhiteSpace(syclLs)) return "Unavailable";
        try
        {
            var output = await RunProcessOutputAsync(syclLs, [], TimeSpan.FromSeconds(3), cancellationToken);
            var line = FirstSyclGpuLine(output);
            return string.IsNullOrWhiteSpace(line) ? "Unavailable" : FormatIntelArcStatus(line);
        }
        catch
        {
            return "Unavailable";
        }
    }

    public static async Task<string> WslIntelArcSummaryAsync(string wslExe, string wslDistro, CancellationToken cancellationToken = default)
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
            return string.IsNullOrWhiteSpace(line) ? "Unavailable" : FormatIntelArcStatus(line);
        }
        catch
        {
            return "Unavailable";
        }
    }

    private static VramMemorySnapshot? ParseMemoryLine(string line)
    {
        var parts = line.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length < 2) return null;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var freeMb)) return null;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var totalMb)) return null;
        return new VramMemorySnapshot(freeMb / 1024, totalMb / 1024);
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

    private static string FirstSyclGpuLine(string output)
        => (output ?? "")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Contains("level_zero", StringComparison.OrdinalIgnoreCase)
                && line.Contains("gpu", StringComparison.OrdinalIgnoreCase)) ?? "";

    private static async Task<string> RunProcessOutputAsync(string fileName, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        if (!process.Start()) return "";
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            TryKillProcessTree(process);
            return "";
        }
        return process.ExitCode == 0 ? await outputTask : "";
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}

