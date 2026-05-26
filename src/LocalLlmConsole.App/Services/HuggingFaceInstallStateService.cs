
namespace LocalLlmConsole.Services;

public sealed record HuggingFaceInstallInventory(HashSet<string> Keys, HashSet<string> FileNames);

public static class HuggingFaceInstallStateService
{
    public static HuggingFaceInstallInventory BuildInventory(IEnumerable<ModelRecord> models)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in models)
        {
            if (!string.IsNullOrWhiteSpace(model.ModelPath))
                fileNames.Add(Path.GetFileName(model.ModelPath));

            foreach (var key in KeysFromMetadata(model.MetadataJson))
                keys.Add(key);

            var legacySource = ModelCatalogService.TryReadLegacySourceReference(model.ModelPath);
            if (legacySource is not null)
                keys.Add(Key(legacySource.Value.Repo, legacySource.Value.Path));
        }

        return new HuggingFaceInstallInventory(keys, fileNames);
    }

    public static bool IsInstalled(HuggingFaceFile file, HuggingFaceInstallInventory installed, string modelsRoot)
    {
        if (installed.Keys.Contains(Key(file))) return true;
        if (installed.FileNames.Contains(file.Name)) return true;
        if (File.Exists(ExpectedDestination(file, modelsRoot))) return true;

        var fileName = Path.GetFileName(file.Path);
        return installed.Keys.Any(key =>
        {
            var split = key.IndexOf('|');
            if (split <= 0 || split == key.Length - 1) return false;
            var repo = key[..split];
            var path = key[(split + 1)..].Replace('\\', '/');
            return string.Equals(repo, file.Repo, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(path, file.Path, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));
        });
    }

    public static string ExpectedDestination(HuggingFaceFile file, string modelsRoot)
    {
        var modelId = ModelCatalogService.SafeId($"{file.Repo.Split('/').Last()}-{Path.GetFileNameWithoutExtension(file.Name)}");
        return Path.Combine(modelsRoot, modelId, file.Name);
    }

    public static IReadOnlyList<string> KeysFromMetadata(string metadataJson)
    {
        try
        {
            var node = JsonNode.Parse(metadataJson);
            return KeysFromNode(node).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static string FormatDownloadProgress(DownloadJobPayload? payload)
    {
        if (payload is null) return "";
        if (payload.TotalBytes <= 0) return payload.DownloadedBytes > 0 ? DisplayFormatService.Bytes(payload.DownloadedBytes) : "";
        var percent = Math.Clamp(payload.DownloadedBytes / (double)payload.TotalBytes, 0, 1);
        return $"{percent:P0} ({DisplayFormatService.Bytes(payload.DownloadedBytes)})";
    }

    public static string DownloadStartLabel(JobStatus status) => status switch
    {
        JobStatus.Completed => "Done",
        JobStatus.Running => "Active",
        JobStatus.Queued => "Queued",
        JobStatus.Failed => "Retry",
        _ => "Resume"
    };

    public static bool CanStartDownload(JobStatus status) => status is JobStatus.Paused or JobStatus.Cancelled or JobStatus.Failed or JobStatus.Interrupted;

    public static bool CanPauseDownload(JobStatus status) => status is JobStatus.Queued or JobStatus.Running;

    public static bool CanStopDownload(JobStatus status) => status is JobStatus.Queued or JobStatus.Running or JobStatus.Paused;

    public static string Key(HuggingFaceFile file) => Key(file.Repo, file.Path);

    public static string Key(string repo, string path) => $"{repo}|{path.Replace('\\', '/')}";

    private static IEnumerable<string> KeysFromNode(JsonNode? node)
    {
        var repo = node?["Repo"]?.ToString()
            ?? node?["repo"]?.ToString()
            ?? node?["sourceRepo"]?.ToString()
            ?? node?["file"]?["Repo"]?.ToString()
            ?? node?["file"]?["repo"]?.ToString()
            ?? node?["file"]?["sourceRepo"]?.ToString();
        var path = node?["Path"]?.ToString()
            ?? node?["path"]?.ToString()
            ?? node?["sourceFile"]?.ToString()
            ?? node?["file"]?["Path"]?.ToString()
            ?? node?["file"]?["path"]?.ToString()
            ?? node?["file"]?["sourceFile"]?.ToString();

        if (!string.IsNullOrWhiteSpace(repo) && !string.IsNullOrWhiteSpace(path))
            yield return Key(repo, path);
    }

}
