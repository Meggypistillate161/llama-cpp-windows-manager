using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace LocalLlmConsole.Services;

public sealed record RuntimePackagePreset(
    string Id,
    string Label,
    RuntimeBackend Backend,
    RuntimeMode Mode,
    string SourcePresetId);

public sealed record RuntimePackageAsset(string Name, string DownloadUrl, long SizeBytes);

public sealed record RuntimePackageRelease(
    string TagName,
    string TargetCommit,
    string HtmlUrl,
    DateTimeOffset PublishedAt,
    IReadOnlyList<RuntimePackageAsset> Assets);

public sealed record RuntimePackageSelection(
    RuntimePackagePreset Preset,
    string ReleaseTag,
    string ReleaseUrl,
    DateTimeOffset PublishedAt,
    RuntimePackageAsset PrimaryAsset,
    IReadOnlyList<RuntimePackageAsset> AdditionalAssets)
{
    public IReadOnlyList<RuntimePackageAsset> AllAssets => [PrimaryAsset, .. AdditionalAssets];
    public string AssetSummary => string.Join(", ", AllAssets.Select(asset => asset.Name));
}

public sealed record RuntimePackageUpdateState(
    bool HasUpdate,
    string LocalTag,
    string LatestTag,
    string ReleaseUrl,
    string AssetSummary,
    DateTimeOffset CheckedAt,
    string LocalIdentity = "",
    string TargetCommit = "",
    bool IsAvailable = true);

public sealed class RuntimePackageAssetUnavailableException : InvalidOperationException
{
    public RuntimePackageAssetUnavailableException(string message) : base(message)
    {
    }
}

public static class RuntimePackageCatalogService
{
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/ggml-org/llama.cpp/releases/latest";
    public const string ReleasesUrl = "https://github.com/ggml-org/llama.cpp/releases";

    public static readonly RuntimePackagePreset[] DefaultPresets =
    [
        new("official-prebuilt-windows-cuda", "Official llama.cpp CUDA Windows", RuntimeBackend.Cuda, RuntimeMode.Native, "official-windows-cuda"),
        new("official-prebuilt-cuda", "Official llama.cpp CUDA WSL", RuntimeBackend.Cuda, RuntimeMode.Wsl, "official-cuda"),
        new("official-prebuilt-windows-vulkan", "Official llama.cpp Vulkan Windows", RuntimeBackend.Vulkan, RuntimeMode.Native, "official-windows-vulkan"),
        new("official-prebuilt-vulkan", "Official llama.cpp Vulkan WSL", RuntimeBackend.Vulkan, RuntimeMode.Wsl, "official-vulkan"),
        new("official-prebuilt-windows-sycl", "Official llama.cpp SYCL Windows (Intel Arc)", RuntimeBackend.Sycl, RuntimeMode.Native, "official-windows-sycl"),
        new("official-prebuilt-sycl", "Official llama.cpp SYCL WSL (Intel Arc)", RuntimeBackend.Sycl, RuntimeMode.Wsl, "official-sycl"),
        new("official-prebuilt-windows-cpu", "Official llama.cpp CPU Windows", RuntimeBackend.Cpu, RuntimeMode.Native, "official-windows-cpu"),
        new("official-prebuilt-cpu", "Official llama.cpp CPU WSL", RuntimeBackend.Cpu, RuntimeMode.Wsl, "official-cpu")
    ];

    public static IReadOnlyList<RuntimePackagePreset> PresetRows() => DefaultPresets;

    public static async Task<RuntimePackageRelease> FetchLatestReleaseAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("LocalLlmConsole", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseReleaseJson(json);
    }

    public static RuntimePackageRelease ParseReleaseJson(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("GitHub release response was empty.");
        var tag = root["tag_name"]?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(tag))
            throw new InvalidOperationException("GitHub release response did not include a release tag.");

        var assetsNode = root["assets"] as JsonArray;
        if (assetsNode is null || assetsNode.Count == 0)
            throw new InvalidOperationException($"Release {tag} did not include downloadable assets.");

        var assets = new List<RuntimePackageAsset>();
        foreach (var assetNode in assetsNode.OfType<JsonObject>())
        {
            var name = assetNode["name"]?.ToString() ?? "";
            var url = assetNode["browser_download_url"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
            assets.Add(new RuntimePackageAsset(name, url, LongValue(assetNode["size"])));
        }

        if (assets.Count == 0)
            throw new InvalidOperationException($"Release {tag} did not include usable downloadable assets.");

        return new RuntimePackageRelease(
            tag,
            root["target_commitish"]?.ToString() ?? "",
            root["html_url"]?.ToString() ?? $"{ReleasesUrl}/tag/{tag}",
            DateTimeOffset.TryParse(root["published_at"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var publishedAt)
                ? publishedAt
                : DateTimeOffset.MinValue,
            assets);
    }

    public static RuntimePackageSelection SelectAssets(RuntimePackagePreset preset, RuntimePackageRelease release)
        => preset.Id switch
        {
            "official-prebuilt-windows-cuda" => SelectWindowsCudaAssets(preset, release),
            "official-prebuilt-cuda" => SelectLinuxCudaAssets(preset, release),
            "official-prebuilt-windows-vulkan" => SelectSingleAsset(preset, release, @"^llama-.+-bin-win-vulkan-x64\.zip$"),
            "official-prebuilt-vulkan" => SelectSingleAsset(preset, release, @"^llama-.+-bin-ubuntu-vulkan-x64\.tar\.gz$"),
            "official-prebuilt-windows-sycl" => SelectSingleAsset(preset, release, @"^llama-.+-bin-win-sycl-x64\.zip$"),
            "official-prebuilt-sycl" => SelectSingleAsset(preset, release, @"^llama-.+-bin-(?:ubuntu|linux)(?:-24)?-sycl(?:-(?:fp16|f16|fp32))?-x64\.(?:tar\.gz|zip)$"),
            "official-prebuilt-windows-cpu" => SelectSingleAsset(preset, release, @"^llama-.+-bin-win-cpu-x64\.zip$"),
            "official-prebuilt-cpu" => SelectSingleAsset(preset, release, @"^llama-.+-bin-ubuntu-x64\.tar\.gz$"),
            _ => throw new InvalidOperationException($"No official prebuilt asset rule exists for {preset.Label}.")
        };

    public static IReadOnlyList<RuntimeRecord> InstalledPackages(IReadOnlyList<RuntimeRecord> runtimes, RuntimePackagePreset preset)
        => runtimes
            .Where(runtime => string.Equals(RuntimeMetadataService.ManagedPackageId(runtime), preset.Id, StringComparison.OrdinalIgnoreCase)
                || RuntimeMetadataService.EquivalentPackageIds(runtime).Contains(preset.Id, StringComparer.OrdinalIgnoreCase))
            .GroupBy(runtime => RuntimeMetadataService.Folder(runtime), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(runtime => runtime.UpdatedAt).First())
            .OrderByDescending(runtime => runtime.UpdatedAt)
            .ToList();

    public static IReadOnlyList<RuntimeRecord> MatchingSourceBuilds(IReadOnlyList<RuntimeRecord> runtimes, RuntimePackagePreset preset)
        => runtimes
            .Where(runtime => string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), preset.SourcePresetId, StringComparison.OrdinalIgnoreCase))
            .GroupBy(runtime => RuntimeMetadataService.Folder(runtime), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(runtime => runtime.UpdatedAt).First())
            .OrderByDescending(runtime => runtime.UpdatedAt)
            .ToList();

    public static string LatestInstalledTag(IReadOnlyList<RuntimeRecord> installed)
        => installed
            .Select(RuntimeMetadataService.PackageTag)
            .FirstOrDefault(tag => !string.IsNullOrWhiteSpace(tag)) ?? "";

    public static string LatestSourceCommit(IReadOnlyList<RuntimeRecord> sourceBuilds)
        => sourceBuilds
            .Select(RuntimeMetadataService.Commit)
            .FirstOrDefault(commit => !string.IsNullOrWhiteSpace(commit)) ?? "";

    public static string LocalIdentity(IReadOnlyList<RuntimeRecord> installed, IReadOnlyList<RuntimeRecord> sourceBuilds)
    {
        var packageTag = LatestInstalledTag(installed);
        if (!string.IsNullOrWhiteSpace(packageTag)) return $"package:{packageTag}";
        var sourceCommit = LatestSourceCommit(sourceBuilds);
        if (!string.IsNullOrWhiteSpace(sourceCommit)) return $"source:{sourceCommit}";
        return "";
    }

    public static bool CanInstallPackage(IReadOnlyList<RuntimeRecord> installed, IReadOnlyList<RuntimeRecord> sourceBuilds, RuntimePackageUpdateState? updateState)
        => (updateState is null || updateState.IsAvailable)
            && (installed.Count == 0 || updateState?.HasUpdate == true);

    public static bool CanInstallPackage(IReadOnlyList<RuntimeRecord> installed, RuntimePackageUpdateState? updateState)
        => CanInstallPackage(installed, [], updateState);

    public static string InstallButtonLabel(IReadOnlyList<RuntimeRecord> installed, IReadOnlyList<RuntimeRecord> sourceBuilds, RuntimePackageUpdateState? updateState)
    {
        if (updateState?.HasUpdate == true) return "Update";
        return installed.Count > 0 ? "Installed" : "Install";
    }

    public static string InstallButtonLabel(IReadOnlyList<RuntimeRecord> installed, RuntimePackageUpdateState? updateState)
        => InstallButtonLabel(installed, [], updateState);

    public static string LocalStatusLabel(IReadOnlyList<RuntimeRecord> installed, IReadOnlyList<RuntimeRecord> sourceBuilds, RuntimePackageUpdateState? updateState = null)
    {
        if (installed.Count > 0)
        {
            if (installed.Any(runtime => RuntimeMetadataService.EquivalentPackageIds(runtime).Count > 0))
                return installed.Count == 1 ? "Exact source match" : $"{installed.Count} exact matches";
            return installed.Count == 1 ? "Installed" : $"{installed.Count} installed";
        }

        if (updateState is { IsAvailable: false })
            return "Not published";

        if (sourceBuilds.Count > 0)
            return sourceBuilds.Count == 1 ? "Built from source" : $"{sourceBuilds.Count} source builds";

        return "Not installed";
    }

    public static string LocalStatusLabel(IReadOnlyList<RuntimeRecord> installed)
        => installed.Count switch
        {
            0 => "Not installed",
            1 => "Installed",
            _ => $"{installed.Count} installed"
        };

    public static string LatestLocalLabel(IReadOnlyList<RuntimeRecord> installed, IReadOnlyList<RuntimeRecord> sourceBuilds, RuntimePackageUpdateState? updateState)
    {
        var localTag = LatestInstalledTag(installed);
        var sourceCommit = LatestSourceCommit(sourceBuilds);
        if (updateState is not null)
        {
            if (!updateState.IsAvailable)
                return $"not published in {DisplayTag(updateState.LatestTag)} - checked {updateState.CheckedAt.ToLocalTime():g}";

            if (updateState.HasUpdate)
            {
                var local = !string.IsNullOrWhiteSpace(localTag)
                    ? DisplayTag(localTag)
                    : RuntimeMetadataService.DisplayCommit(sourceCommit);
                var latest = !string.IsNullOrWhiteSpace(updateState.TargetCommit)
                    ? $"{DisplayTag(updateState.LatestTag)} {RuntimeMetadataService.DisplayCommit(updateState.TargetCommit)}"
                    : DisplayTag(updateState.LatestTag);
                return $"update available {local} -> {latest}";
            }

            return string.IsNullOrWhiteSpace(localTag)
                ? !string.IsNullOrWhiteSpace(sourceCommit)
                    ? $"source matches {DisplayTag(updateState.LatestTag)} - checked {updateState.CheckedAt.ToLocalTime():g}"
                    : $"latest {DisplayTag(updateState.LatestTag)} - checked {updateState.CheckedAt.ToLocalTime():g}"
                : $"current {DisplayTag(localTag)} - checked {updateState.CheckedAt.ToLocalTime():g}";
        }

        if (!string.IsNullOrWhiteSpace(sourceCommit))
            return $"source built {RuntimeMetadataService.DisplayCommit(sourceCommit)} - {sourceBuilds[0].UpdatedAt.ToLocalTime():g}";
        if (installed.Count == 0) return "";
        return string.IsNullOrWhiteSpace(localTag)
            ? $"installed - {installed[0].UpdatedAt.ToLocalTime():g}"
            : $"installed {DisplayTag(localTag)} - {installed[0].UpdatedAt.ToLocalTime():g}";
    }

    public static string BackendLabel(RuntimePackagePreset preset)
        => $"{BackendName(preset.Backend)} {RuntimeBuildCatalogService.ModeLabel(preset.Mode)}";

    public static string InstallDir(string runtimeRoot, RuntimePackageSelection selection)
        => Path.Combine(runtimeRoot, $"{selection.Preset.Id}-{ModelCatalogService.SafeId(selection.ReleaseTag)}");

    public static string DownloadCacheDir(string cacheRoot, RuntimePackageSelection selection)
        => Path.Combine(cacheRoot, "runtime-packages", selection.Preset.Id, ModelCatalogService.SafeId(selection.ReleaseTag));

    public static async Task DownloadAssetAsync(HttpClient client, RuntimePackageAsset asset, string destination, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("LocalLlmConsole", "1.0"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
    }

    public static void ExtractArchive(string archivePath, string destination)
    {
        Directory.CreateDirectory(destination);
        var staging = Path.Combine(Path.GetTempPath(), $"llama-runtime-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            ExtractArchiveToDirectory(archivePath, staging);
            FlattenSingleTopLevelRuntimeDirectory(staging);
            MergeDirectory(staging, destination);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only; extraction failure is reported by the caller.
            }
        }
    }

    private static void ExtractArchiveToDirectory(string archivePath, string destination)
    {
        var name = Path.GetFileName(archivePath);
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destination, overwriteFiles: true);
            return;
        }

        if (name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, destination, overwriteFiles: true);
            return;
        }

        throw new InvalidOperationException($"Unsupported runtime archive format: {name}");
    }

    public static string FindRuntimeExecutable(string installDir, RuntimeMode mode)
    {
        var preferredName = mode == RuntimeMode.Native ? "llama-server.exe" : "llama-server";
        var fallbackName = mode == RuntimeMode.Native ? "llama-server" : "llama-server.exe";
        var preferred = FindExecutableByName(installDir, preferredName);
        if (!string.IsNullOrWhiteSpace(preferred)) return preferred;
        var fallback = FindExecutableByName(installDir, fallbackName);
        if (!string.IsNullOrWhiteSpace(fallback)) return fallback;
        throw new InvalidOperationException("No llama-server executable was found in the extracted runtime package.");
    }

    public static string RuntimeFolderFromExecutable(string executablePath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(executablePath)) ?? "";
        if (Path.GetFileName(folder).Equals("bin", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(folder) ?? folder;
        return folder;
    }

    public static async Task StampManagedMetadataAsync(string runtimeFolder, string installRoot, RuntimePackageSelection selection, CancellationToken cancellationToken = default)
    {
        var metadata = new JsonObject
        {
            ["name"] = selection.Preset.Label,
            ["runtime"] = RuntimeBuildCatalogService.ModeKey(selection.Preset.Mode),
            ["backend"] = RuntimeBuildCatalogService.BackendKey(selection.Preset.Backend),
            ["managedPackageId"] = selection.Preset.Id,
            ["managedPresetId"] = selection.Preset.Id,
            ["sourcePresetId"] = selection.Preset.SourcePresetId,
            ["source"] = "official-prebuilt",
            ["repoUrl"] = "https://github.com/ggml-org/llama.cpp",
            ["releaseTag"] = selection.ReleaseTag,
            ["releaseUrl"] = selection.ReleaseUrl,
            ["installRoot"] = Path.GetFullPath(installRoot),
            ["managedInstalledAt"] = DateTimeOffset.UtcNow.ToString("O")
        };

        var assets = new JsonArray();
        foreach (var asset in selection.AllAssets)
        {
            assets.Add(new JsonObject
            {
                ["name"] = asset.Name,
                ["downloadUrl"] = asset.DownloadUrl,
                ["sizeBytes"] = asset.SizeBytes
            });
        }
        metadata["assets"] = assets;

        Directory.CreateDirectory(runtimeFolder);
        await File.WriteAllTextAsync(
            Path.Combine(runtimeFolder, "local-llm-runtime.json"),
            metadata.ToJsonString(),
            cancellationToken);
    }

    public static string DisplayTag(string tag)
        => string.IsNullOrWhiteSpace(tag) ? "n/a" : tag;

    private static RuntimePackageSelection SelectSingleAsset(RuntimePackagePreset preset, RuntimePackageRelease release, string pattern)
    {
        var asset = release.Assets.FirstOrDefault(asset => Regex.IsMatch(asset.Name, pattern, RegexOptions.IgnoreCase));
        if (asset is null)
            throw new RuntimePackageAssetUnavailableException($"Release {release.TagName} does not include a matching asset for {preset.Label}.");
        return new RuntimePackageSelection(preset, release.TagName, release.HtmlUrl, release.PublishedAt, asset, []);
    }

    private static RuntimePackageSelection SelectWindowsCudaAssets(RuntimePackagePreset preset, RuntimePackageRelease release)
    {
        var binaries = release.Assets
            .Select(asset => new { Asset = asset, Version = MatchGroup(asset.Name, @"^llama-.+-bin-win-cuda-(?<version>[0-9.]+)-x64\.zip$", "version") })
            .Where(match => !string.IsNullOrWhiteSpace(match.Version))
            .ToList();
        var runtimeDlls = release.Assets
            .Select(asset => new { Asset = asset, Version = MatchGroup(asset.Name, @"^cudart-llama-bin-win-cuda-(?<version>[0-9.]+)-x64\.zip$", "version") })
            .Where(match => !string.IsNullOrWhiteSpace(match.Version))
            .ToDictionary(match => match.Version, match => match.Asset, StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in binaries.OrderByDescending(match => CudaVersionPreference(match.Version)))
        {
            if (runtimeDlls.TryGetValue(candidate.Version, out var dllAsset))
                return new RuntimePackageSelection(preset, release.TagName, release.HtmlUrl, release.PublishedAt, candidate.Asset, [dllAsset]);
        }

        throw new InvalidOperationException($"Release {release.TagName} does not include matching CUDA binaries and runtime DLLs for Windows.");
    }

    private static RuntimePackageSelection SelectLinuxCudaAssets(RuntimePackagePreset preset, RuntimePackageRelease release)
    {
        var binaries = release.Assets
            .Select(asset => new
            {
                Asset = asset,
                Version = MatchGroup(asset.Name, @"^llama-.+-bin-(?:ubuntu|linux)-cuda(?:-(?<version>[0-9.]+))?-x64\.tar\.gz$", "version")
            })
            .Where(match => Regex.IsMatch(match.Asset.Name, @"^llama-.+-bin-(?:ubuntu|linux)-cuda(?:-[0-9.]+)?-x64\.tar\.gz$", RegexOptions.IgnoreCase))
            .ToList();
        if (binaries.Count == 0)
            throw new RuntimePackageAssetUnavailableException($"Release {release.TagName} does not publish an official CUDA WSL/Linux x64 prebuilt runtime.");

        var runtimeDlls = release.Assets
            .Select(asset => new
            {
                Asset = asset,
                Version = MatchGroup(asset.Name, @"^cudart-llama-bin-(?:ubuntu|linux)-cuda(?:-(?<version>[0-9.]+))?-x64\.tar\.gz$", "version")
            })
            .Where(match => Regex.IsMatch(match.Asset.Name, @"^cudart-llama-bin-(?:ubuntu|linux)-cuda(?:-[0-9.]+)?-x64\.tar\.gz$", RegexOptions.IgnoreCase))
            .GroupBy(match => match.Version ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Asset, StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in binaries.OrderByDescending(match => CudaVersionPreference(match.Version)))
        {
            if (!string.IsNullOrWhiteSpace(candidate.Version) && runtimeDlls.TryGetValue(candidate.Version, out var dllAsset))
                return new RuntimePackageSelection(preset, release.TagName, release.HtmlUrl, release.PublishedAt, candidate.Asset, [dllAsset]);
        }

        var primary = binaries.OrderByDescending(match => CudaVersionPreference(match.Version)).First().Asset;
        return new RuntimePackageSelection(preset, release.TagName, release.HtmlUrl, release.PublishedAt, primary, []);
    }

    private static (int FamilyScore, Version Version) CudaVersionPreference(string value)
    {
        var normalized = value.Trim();
        var familyScore = normalized.StartsWith("12.", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        return Version.TryParse(normalized, out var version)
            ? (familyScore, version)
            : (familyScore, new Version(0, 0));
    }

    private static string MatchGroup(string text, string pattern, string group)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[group].Value : "";
    }

    private static string? FindExecutableByName(string folder, string name)
    {
        var direct = Path.Combine(folder, name);
        if (File.Exists(direct)) return Path.GetFullPath(direct);
        var bin = Path.Combine(folder, "bin", name);
        if (File.Exists(bin)) return Path.GetFullPath(bin);
        return Directory.EnumerateFiles(folder, "llama-server*", SafeRecursiveEnumeration())
            .FirstOrDefault(file => Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static void FlattenSingleTopLevelRuntimeDirectory(string destination)
    {
        var files = Directory.EnumerateFiles(destination, "*", SearchOption.TopDirectoryOnly).ToArray();
        var directories = Directory.EnumerateDirectories(destination, "*", SearchOption.TopDirectoryOnly).ToArray();
        if (files.Length > 0 || directories.Length != 1) return;
        var child = directories[0];

        foreach (var childFile in Directory.EnumerateFiles(child, "*", SearchOption.TopDirectoryOnly))
        {
            var target = Path.Combine(destination, Path.GetFileName(childFile));
            if (File.Exists(target)) File.Delete(target);
            File.Move(childFile, target);
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(child, "*", SearchOption.TopDirectoryOnly))
        {
            var target = Path.Combine(destination, Path.GetFileName(childDirectory));
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            Directory.Move(childDirectory, target);
        }

        Directory.Delete(child, recursive: true);
    }

    private static void MergeDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            if (relative == ".") continue;
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string? FindDirectOrBinExecutableByName(string folder, string name)
    {
        var direct = Path.Combine(folder, name);
        if (File.Exists(direct)) return Path.GetFullPath(direct);
        var bin = Path.Combine(folder, "bin", name);
        return File.Exists(bin) ? Path.GetFullPath(bin) : null;
    }

    private static long LongValue(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<long>(out var result)) return result;
        return long.TryParse(node?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static string BackendName(RuntimeBackend backend) => backend switch
    {
        RuntimeBackend.Cuda => "CUDA",
        RuntimeBackend.Vulkan => "Vulkan",
        RuntimeBackend.Sycl => "SYCL",
        _ => "CPU"
    };

    private static EnumerationOptions SafeRecursiveEnumeration() => new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System
    };
}
