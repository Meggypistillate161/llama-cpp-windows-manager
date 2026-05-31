
namespace LocalLlmConsole.Services;

public static class GpuStatusService
{
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
        return NormalizeMetricSeparators($"GPU {index}: {utilization}% | {temperature}C | {memory}");
    }

    public static string NormalizeMetricSeparators(string text)
        => Regex.Replace(text.Trim(), @"\s*\|\s*", " | ");

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

    public static VramMemorySnapshot? ParseMemoryLine(string line)
    {
        var parts = line.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length < 2) return null;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var freeMb)) return null;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var totalMb)) return null;
        return new VramMemorySnapshot(freeMb / 1024, totalMb / 1024);
    }

    public static string FirstSyclGpuLine(string output)
        => (output ?? "")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Contains("level_zero", StringComparison.OrdinalIgnoreCase)
                && line.Contains("gpu", StringComparison.OrdinalIgnoreCase)) ?? "";
}
