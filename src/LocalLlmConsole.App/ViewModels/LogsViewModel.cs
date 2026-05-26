using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class LogsViewModel
{
    public ObservableCollection<UiRow> Rows { get; } = new();

    public void ReplaceLogs(
        IEnumerable<FileInfo> files,
        IReadOnlyDictionary<string, JobRecord> jobsByLogPath,
        string activeLogPath,
        string activeModel)
    {
        Rows.Clear();
        var normalizedActiveLogPath = LogFileService.NormalizePath(activeLogPath);
        foreach (var file in files.OrderByDescending(file => file.LastWriteTimeUtc))
        {
            var path = LogFileService.NormalizePath(file.FullName);
            jobsByLogPath.TryGetValue(path, out var job);
            var (type, related) = LogFileService.Describe(file.FullName, job, path == normalizedActiveLogPath, activeModel);
            Rows.Add(new UiRow
            {
                C1 = type,
                C2 = file.Name,
                C3 = related,
                C4 = file.LastWriteTime.ToString("g"),
                C5 = DisplayFormatService.Bytes(file.Length),
                C6 = "Open",
                C7 = "Delete",
                T1 = "Open this log file in the default Windows editor.",
                T2 = path == normalizedActiveLogPath ? "Stop the active runtime before deleting its current log." : "Delete this log file.",
                B1 = true,
                B2 = true,
                Data = new JsonObject
                {
                    ["Path"] = file.FullName,
                    ["Type"] = type,
                    ["Related"] = related,
                    ["Updated"] = file.LastWriteTime.ToString("g"),
                    ["Size"] = DisplayFormatService.Bytes(file.Length)
                }
            });
        }
    }
}
