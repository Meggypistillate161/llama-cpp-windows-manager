
namespace LocalLlmConsole.Services;

public sealed class RuntimeRegistryService
{
    private sealed record RuntimeCandidate(string Folder, string ExecutablePath);

    private readonly StateStore _store;

    public RuntimeRegistryService(StateStore store) => _store = store;

    public async Task<int> ScanAsync(string runtimeRoot)
    {
        Directory.CreateDirectory(runtimeRoot);
        var candidates = await Task.Run(() => CandidateRuntimeFolders(runtimeRoot).Take(1000).ToArray());
        var count = 0;
        foreach (var candidate in candidates)
        {
            await RegisterFolderAsync(candidate.Folder, candidate.ExecutablePath);
            count++;
        }
        return count;
    }

    public async Task<RuntimeRecord> RegisterFolderAsync(string folder)
        => await RegisterFolderAsync(folder, executableHint: "");

    private async Task<RuntimeRecord> RegisterFolderAsync(string folder, string executableHint)
    {
        var record = await Task.Run(() => CreateRuntimeRecord(folder, executableHint));
        await _store.UpsertRuntimeAsync(record);
        return record;
    }

    private static RuntimeRecord CreateRuntimeRecord(string folder, string executableHint)
    {
        var full = NormalizeRuntimeFolder(Path.GetFullPath(folder));
        var executable = IsUsableExecutableHint(full, executableHint)
            ? Path.GetFullPath(executableHint)
            : FindLlamaServer(full) ?? throw new InvalidOperationException("No llama-server or llama-server.exe was found in that folder or its bin folder.");
        var packaged = ReadPackagedMetadata(full);
        var backend = InferBackend(full, executable, packaged);
        var managedPresetId = InferManagedPresetId(full, backend, packaged);
        var metadataRuntime = packaged?["runtime"]?.ToString();
        var mode = string.Equals(metadataRuntime, "native", StringComparison.OrdinalIgnoreCase)
            ? RuntimeMode.Native
            : executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? RuntimeMode.Native : RuntimeMode.Wsl;
        var id = ModelCatalogService.SafeId($"llama-cpp-{Path.GetFileName(full)}-{backend}");
        var metadata = new JsonObject
        {
            ["folder"] = full,
            ["mode"] = mode.ToString(),
            ["registeredAt"] = DateTimeOffset.UtcNow.ToString("O")
        };
        var packagedName = packaged?["name"]?.ToString();
        if (!string.IsNullOrWhiteSpace(managedPresetId)) metadata["managedPresetId"] = managedPresetId;
        if (packaged is not null) metadata["runtimeMetadata"] = packaged.DeepClone();

        var displayName = string.IsNullOrWhiteSpace(packagedName)
            ? $"llama.cpp {Path.GetFileName(full)} {mode} {backend}"
            : $"{packagedName} ({Path.GetFileName(full)})";
        return new RuntimeRecord(id, displayName, mode, backend, executable, metadata.ToJsonString(), DateTimeOffset.UtcNow);
    }

    private static string NormalizeRuntimeFolder(string folder)
    {
        if (!Path.GetFileName(folder).Equals("bin", StringComparison.OrdinalIgnoreCase)) return folder;
        var parent = Path.GetDirectoryName(folder);
        return string.IsNullOrWhiteSpace(parent) ? folder : parent;
    }

    private static JsonObject? ReadPackagedMetadata(string folder)
    {
        var metadataPath = Path.Combine(folder, "local-llm-runtime.json");
        if (!File.Exists(metadataPath)) return null;
        try
        {
            return JsonNode.Parse(File.ReadAllText(metadataPath)) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static RuntimeBackend InferBackend(string folder, string executablePath, JsonObject? metadata)
    {
        var text = $"{RuntimeMetadataText(metadata)} {folder} {executablePath}";
        if (text.Contains("cuda", StringComparison.OrdinalIgnoreCase)) return RuntimeBackend.Cuda;
        if (text.Contains("vulkan", StringComparison.OrdinalIgnoreCase)) return RuntimeBackend.Vulkan;
        return HasNearbyCudaMarker(folder)
            ? RuntimeBackend.Cuda
            : RuntimeBackend.Cpu;
    }

    private static string RuntimeMetadataText(JsonObject? metadata)
    {
        if (metadata is null) return "";
        var values = new List<string>
        {
            metadata["build"]?.ToString() ?? "",
            metadata["backend"]?.ToString() ?? "",
            metadata["name"]?.ToString() ?? "",
            metadata["repoUrl"]?.ToString() ?? "",
            metadata["sourcePath"]?.ToString() ?? ""
        };
        if (metadata["tags"] is JsonArray tags)
        {
            values.AddRange(tags.Select(tag => tag?.ToString() ?? ""));
        }
        return string.Join(" ", values);
    }

    private static string InferManagedPresetId(string folder, RuntimeBackend backend, JsonObject? metadata)
    {
        var explicitId = metadata?["managedPresetId"]?.ToString();
        if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId;

        var text = $"{RuntimeMetadataText(metadata)} {folder}".Replace('\\', '/');
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
            if (backend == RuntimeBackend.Cuda) return "official-cuda";
            if (backend == RuntimeBackend.Vulkan) return "official-vulkan";
            return "official-cpu";
        }

        return "";
    }

    private static IEnumerable<RuntimeCandidate> CandidateRuntimeFolders(string root)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fullRoot = Path.GetFullPath(root);
        var directRootExecutable = FindLlamaServer(fullRoot, recursive: false);
        if (!string.IsNullOrWhiteSpace(directRootExecutable))
        {
            var folder = NormalizeRuntimeFolder(Path.GetDirectoryName(directRootExecutable) ?? fullRoot);
            if (seen.Add(folder)) yield return new RuntimeCandidate(folder, directRootExecutable);
        }

        foreach (var executable in Directory.EnumerateFiles(root, "llama-server*", SafeRecursiveEnumeration())
                     .Where(file => Path.GetFileName(file).Equals("llama-server", StringComparison.OrdinalIgnoreCase)
                         || Path.GetFileName(file).Equals("llama-server.exe", StringComparison.OrdinalIgnoreCase))
                     .Take(1000))
        {
            var folder = Path.GetDirectoryName(executable);
            if (folder is null) continue;
            folder = NormalizeRuntimeFolder(folder);
            if (!string.IsNullOrWhiteSpace(folder) && seen.Add(Path.GetFullPath(folder)))
                yield return new RuntimeCandidate(Path.GetFullPath(folder), Path.GetFullPath(executable));
        }
    }

    private static string? FindLlamaServer(string folder, bool recursive = true)
    {
        var direct = Path.Combine(folder, "llama-server.exe");
        if (File.Exists(direct)) return direct;
        var bin = Path.Combine(folder, "bin", "llama-server.exe");
        if (File.Exists(bin)) return bin;
        var wslDirect = Path.Combine(folder, "llama-server");
        if (File.Exists(wslDirect)) return wslDirect;
        var wslBin = Path.Combine(folder, "bin", "llama-server");
        if (File.Exists(wslBin)) return wslBin;
        if (!recursive) return null;
        return Directory.EnumerateFiles(folder, "llama-server*", SafeRecursiveEnumeration())
            .FirstOrDefault(file => Path.GetFileName(file).Equals("llama-server.exe", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(file).Equals("llama-server", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUsableExecutableHint(string runtimeFolder, string executableHint)
    {
        if (string.IsNullOrWhiteSpace(executableHint) || !File.Exists(executableHint)) return false;
        var name = Path.GetFileName(executableHint);
        if (!name.Equals("llama-server", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("llama-server.exe", StringComparison.OrdinalIgnoreCase))
            return false;

        var executableFolder = NormalizeRuntimeFolder(Path.GetDirectoryName(Path.GetFullPath(executableHint)) ?? "");
        return string.Equals(Path.GetFullPath(runtimeFolder), Path.GetFullPath(executableFolder), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasNearbyCudaMarker(string folder)
    {
        foreach (var candidate in new[] { folder, Path.Combine(folder, "bin"), Path.Combine(folder, "lib") })
        {
            if (!Directory.Exists(candidate)) continue;
            if (Directory.EnumerateFiles(candidate, "*cuda*", SearchOption.TopDirectoryOnly).Any())
                return true;
        }

        return false;
    }

    private static EnumerationOptions SafeRecursiveEnumeration() => new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System
    };
}
