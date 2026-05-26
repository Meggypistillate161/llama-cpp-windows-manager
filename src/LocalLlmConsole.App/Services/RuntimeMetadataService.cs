
namespace LocalLlmConsole.Services;

public static class RuntimeMetadataService
{
    public static string ManagedPresetId(RuntimeRecord runtime)
    {
        try
        {
            var metadata = JsonNode.Parse(runtime.MetadataJson);
            var explicitId = metadata?["managedPresetId"]?.ToString()
                ?? metadata?["runtimeMetadata"]?["managedPresetId"]?.ToString()
                ?? metadata?["packaged"]?["managedPresetId"]?.ToString()
                ?? "";
            if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId;

            var text = string.Join(" ", new[]
            {
                runtime.Name,
                runtime.ExecutablePath,
                metadata?["folder"]?.ToString() ?? "",
                metadata?["repoUrl"]?.ToString() ?? "",
                metadata?["runtimeMetadata"]?["repoUrl"]?.ToString() ?? "",
                metadata?["runtimeMetadata"]?["sourcePath"]?.ToString() ?? "",
                metadata?["runtimeMetadata"]?["build"]?.ToString() ?? "",
                metadata?["runtimeMetadata"]?["name"]?.ToString() ?? "",
                PackagedMetadataText(Folder(runtime))
            }).Replace('\\', '/');

            if (text.Contains("AtomicBot-ai/atomic-llama-cpp-turboquant", StringComparison.OrdinalIgnoreCase)
                || text.Contains("atomic-llama-cpp-turboquant", StringComparison.OrdinalIgnoreCase))
                return "atomic-turboquant-cuda";
            if (text.Contains("TheTom/llama-cpp-turboquant", StringComparison.OrdinalIgnoreCase))
                return "thetom-turboquant-cuda";
            if (text.Contains("ikawrakow/ik_llama.cpp", StringComparison.OrdinalIgnoreCase)
                || text.Contains("ik_llama.cpp", StringComparison.OrdinalIgnoreCase)
                || text.Contains("ik-llama", StringComparison.OrdinalIgnoreCase))
                return "ik-llama-cuda";
            if (text.Contains("ggml-org/llama.cpp", StringComparison.OrdinalIgnoreCase)
                || text.Contains("ggerganov/llama.cpp", StringComparison.OrdinalIgnoreCase)
                || text.Contains("llama.cpp", StringComparison.OrdinalIgnoreCase))
            {
                if (runtime.Backend == RuntimeBackend.Cuda || text.Contains("cuda", StringComparison.OrdinalIgnoreCase)) return "official-cuda";
                if (runtime.Backend == RuntimeBackend.Vulkan || text.Contains("vulkan", StringComparison.OrdinalIgnoreCase)) return "official-vulkan";
                return "official-cpu";
            }

            return "";
        }
        catch
        {
            return "";
        }
    }

    public static string Commit(RuntimeRecord runtime)
    {
        try
        {
            var metadata = JsonNode.Parse(runtime.MetadataJson);
            var commit = metadata?["commit"]?.ToString()
                ?? metadata?["runtimeMetadata"]?["commit"]?.ToString()
                ?? "";
            if (!string.IsNullOrWhiteSpace(commit)) return commit;
        }
        catch
        {
            // Try packaged metadata below.
        }

        var folder = Folder(runtime);
        try
        {
            var metadataPath = Path.Combine(folder, "local-llm-runtime.json");
            if (File.Exists(metadataPath))
            {
                var commit = JsonNode.Parse(File.ReadAllText(metadataPath))?["commit"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(commit)) return commit;
            }
        }
        catch
        {
            // Infer from paths below.
        }

        return InferCommitFromText($"{runtime.Name} {runtime.ExecutablePath} {folder}");
    }

    public static string Folder(RuntimeRecord runtime)
    {
        try
        {
            var metadata = JsonNode.Parse(runtime.MetadataJson);
            var folder = metadata?["folder"]?.ToString();
            if (!string.IsNullOrWhiteSpace(folder)) return NormalizeFolder(folder);
        }
        catch
        {
            // Fall back to executable location below.
        }

        var parent = Path.GetDirectoryName(runtime.ExecutablePath) ?? "";
        return NormalizeFolder(parent);
    }

    public static string PackagedMetadataText(string folder)
    {
        try
        {
            var metadataPath = Path.Combine(folder, "local-llm-runtime.json");
            if (!File.Exists(metadataPath)) return "";
            var metadata = JsonNode.Parse(File.ReadAllText(metadataPath));
            return string.Join(" ", new[]
            {
                metadata?["managedPresetId"]?.ToString() ?? "",
                metadata?["repoUrl"]?.ToString() ?? "",
                metadata?["sourcePath"]?.ToString() ?? "",
                metadata?["build"]?.ToString() ?? "",
                metadata?["name"]?.ToString() ?? ""
            });
        }
        catch
        {
            return "";
        }
    }

    public static bool CommitsMatch(string localCommit, string remoteCommit)
    {
        if (string.IsNullOrWhiteSpace(localCommit) || string.IsNullOrWhiteSpace(remoteCommit)) return false;
        return remoteCommit.StartsWith(localCommit, StringComparison.OrdinalIgnoreCase)
            || localCommit.StartsWith(remoteCommit, StringComparison.OrdinalIgnoreCase);
    }

    public static string ShortCommit(string commit)
        => string.IsNullOrWhiteSpace(commit) ? "n/a" : commit[..Math.Min(12, commit.Length)];

    public static string DisplayCommit(string commit)
        => string.IsNullOrWhiteSpace(commit) ? "commit unavailable" : ShortCommit(commit);

    public static string TryReadGitHeadCommit(string sourceDir)
    {
        try
        {
            var headPath = Path.Combine(sourceDir, ".git", "HEAD");
            if (!File.Exists(headPath)) return "";
            var head = File.ReadAllText(headPath).Trim();
            if (head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
            {
                var refPath = Path.Combine(sourceDir, ".git", head[4..].Trim().Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(refPath) ? File.ReadAllText(refPath).Trim() : "";
            }
            return head;
        }
        catch
        {
            return "";
        }
    }

    public static string InferCommitFromText(string text)
    {
        var matches = Regex.Matches(text, "[0-9a-fA-F]{7,40}");
        return matches.Count == 0
            ? ""
            : matches.Cast<Match>().OrderByDescending(match => match.Value.Length).First().Value;
    }

    public static string NormalizeFolder(string folder)
    {
        if (!Path.GetFileName(folder).Equals("bin", StringComparison.OrdinalIgnoreCase)) return folder;
        return Path.GetDirectoryName(folder) ?? folder;
    }
}
