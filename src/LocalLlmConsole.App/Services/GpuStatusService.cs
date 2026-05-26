
namespace LocalLlmConsole.Services;

public static class GpuStatusService
{
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

