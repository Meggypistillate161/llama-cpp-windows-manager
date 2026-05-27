using System.IO.Compression;
using System.Net.Http.Headers;

namespace LocalLlmConsole.Services;

public sealed record AppUpdateInfo(
    bool IsAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseName,
    string ReleaseNotes,
    string HtmlUrl,
    string AssetName,
    string AssetUrl,
    long AssetSize,
    string ChecksumAssetName = "",
    string ChecksumAssetUrl = "",
    string ExpectedSha256 = "");

public sealed record AppUpdateInstallPlan(string ScriptPath, string SourceExe, string TargetExe, string NoticePath);

public sealed record InstalledUpdateNotice(string Version, string ReleaseName, string ReleaseNotes, DateTimeOffset InstalledAt);

public sealed class AppUpdateService
{
    public const string RepositoryUrl = "https://github.com/alekk89/llama.cpp-Console";
    public const string PortableExeName = "LlamaCppConsole.exe";

    private const string UserAgent = "llama-cpp-console-updater";
    private readonly HttpClient _http;

    public AppUpdateService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, CurrentVersionLabel().TrimStart('v')));
        if (!_http.DefaultRequestHeaders.Accept.Any())
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static string CurrentVersionLabel()
    {
        var value = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "v0.0.0";
        return value.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? value : $"v{value}";
    }

    public async Task<AppUpdateInfo> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        var releaseUrl = $"{RepositoryUrl.TrimEnd('/')}/releases/latest";
        if (TryParseGitHubRepository(RepositoryUrl, out var owner, out var repo))
            releaseUrl = $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/releases/latest";

        using var request = new HttpRequestMessage(HttpMethod.Get, releaseUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return NoUpdateAvailable(CurrentVersionLabel(), "No GitHub release feed is published yet.");
        response.EnsureSuccessStatusCode();

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException("GitHub did not return a release object.");
        return ParseLatestRelease(json, CurrentVersionLabel());
    }

    public static AppUpdateInfo ParseLatestRelease(JsonObject release, string currentVersion)
    {
        var latestVersion = FirstNonBlank(
            release["tag_name"]?.ToString(),
            release["name"]?.ToString());
        if (string.IsNullOrWhiteSpace(latestVersion))
            throw new InvalidOperationException("The GitHub release has no tag name.");

        var assets = release["assets"]?.AsArray();
        var asset = SelectPortableAsset(assets);
        var checksum = SelectChecksumAsset(assets, asset.Name);
        var latest = NormalizeVersion(latestVersion);
        var current = NormalizeVersion(currentVersion);
        return new AppUpdateInfo(
            IsVersionNewer(latest, current),
            VersionLabel(currentVersion),
            VersionLabel(latestVersion),
            FirstNonBlank(release["name"]?.ToString(), VersionLabel(latestVersion)),
            release["body"]?.ToString() ?? "",
            release["html_url"]?.ToString() ?? RepositoryUrl,
            asset.Name,
            asset.Url,
            asset.Size,
            checksum.Name,
            checksum.Url);
    }

    public static AppUpdateInfo NoUpdateAvailable(string currentVersion, string message = "No updates are available.")
        => new(false, VersionLabel(currentVersion), VersionLabel(currentVersion), message, message, RepositoryUrl, "", "", 0);

    public async Task<AppUpdateInstallPlan> StageInstallAsync(AppUpdateInfo update, string workspaceRoot, string? currentExecutablePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.AssetUrl))
            throw new InvalidOperationException("The latest GitHub release does not include a portable llama.cpp Console asset.");
        var hasInlineChecksum = !string.IsNullOrWhiteSpace(update.ExpectedSha256);
        if (hasInlineChecksum && string.IsNullOrWhiteSpace(NormalizeSha256(update.ExpectedSha256)))
            throw new InvalidOperationException("The latest GitHub release includes an invalid SHA-256 checksum. Refusing to stage an unverifiable update.");
        if (!hasInlineChecksum && string.IsNullOrWhiteSpace(update.ChecksumAssetUrl))
            throw new InvalidOperationException("The latest GitHub release asset is missing a SHA-256 companion file. Refusing to stage an unverifiable update.");

        var targetExe = string.IsNullOrWhiteSpace(currentExecutablePath)
            ? Path.Combine(AppContext.BaseDirectory, PortableExeName)
            : Path.GetFullPath(currentExecutablePath);
        if (!Path.GetFileName(targetExe).Equals(PortableExeName, StringComparison.OrdinalIgnoreCase))
            targetExe = Path.Combine(Path.GetDirectoryName(targetExe) ?? AppContext.BaseDirectory, PortableExeName);

        var safeVersion = RegexSafeFileName(update.LatestVersion);
        var updateRoot = Path.Combine(workspaceRoot, "cache", "app-updates");
        var stageRoot = Path.Combine(updateRoot, safeVersion);
        Directory.CreateDirectory(stageRoot);

        var assetPath = Path.Combine(stageRoot, RegexSafeFileName(update.AssetName));
        await DownloadAssetAsync(update.AssetUrl, assetPath, cancellationToken);
        await VerifyChecksumAssetAsync(update, assetPath, cancellationToken);
        var stagedExe = PreparePortableExe(assetPath, stageRoot);
        ValidateUpdateSignature(stagedExe, targetExe);

        var pendingNotice = Path.Combine(stageRoot, "installed-update.json");
        await File.WriteAllTextAsync(pendingNotice, JsonSerializer.Serialize(new InstalledUpdateNotice(
            update.LatestVersion,
            update.ReleaseName,
            TrimReleaseNotes(update.ReleaseNotes),
            DateTimeOffset.UtcNow)), cancellationToken);

        var noticePath = PendingNoticePath(workspaceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(noticePath)!);
        var scriptPath = Path.Combine(stageRoot, "Install-LlamaCppConsoleUpdate.ps1");
        await File.WriteAllTextAsync(scriptPath, UpdaterScript(), new UTF8Encoding(false), cancellationToken);
        return new AppUpdateInstallPlan(scriptPath, stagedExe, targetExe, noticePath);
    }

    public void StartInstaller(AppUpdateInstallPlan plan, int currentProcessId)
    {
        var psi = new ProcessStartInfo(HostExecutableResolver.WindowsPowerShellExe())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(plan.TargetExe) ?? AppContext.BaseDirectory
        };
        foreach (var arg in new[]
        {
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", plan.ScriptPath,
            "-ParentPid", currentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-SourceExe", plan.SourceExe,
            "-TargetExe", plan.TargetExe,
            "-NoticeSource", Path.Combine(Path.GetDirectoryName(plan.ScriptPath) ?? "", "installed-update.json"),
            "-NoticeTarget", plan.NoticePath,
            "-WorkingDirectory", Path.GetDirectoryName(plan.TargetExe) ?? AppContext.BaseDirectory
        })
        {
            psi.ArgumentList.Add(arg);
        }

        Process.Start(psi);
    }

    public static async Task<InstalledUpdateNotice?> TryConsumeInstalledNoticeAsync(string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var path = PendingNoticePath(workspaceRoot);
        if (!File.Exists(path)) return null;
        try
        {
            var notice = JsonSerializer.Deserialize<InstalledUpdateNotice>(await File.ReadAllTextAsync(path, cancellationToken));
            File.Delete(path);
            return notice;
        }
        catch
        {
            try { File.Delete(path); } catch { }
            return null;
        }
    }

    private async Task DownloadAssetAsync(string assetUrl, string destination, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, assetUrl);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken);
    }

    private async Task VerifyChecksumAssetAsync(AppUpdateInfo update, string assetPath, CancellationToken cancellationToken)
    {
        var expected = NormalizeSha256(update.ExpectedSha256);
        if (!string.IsNullOrWhiteSpace(update.ExpectedSha256) && string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException("The latest GitHub release includes an invalid SHA-256 checksum. Refusing to install without verification.");
        if (string.IsNullOrWhiteSpace(expected) && !string.IsNullOrWhiteSpace(update.ChecksumAssetUrl))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, update.ChecksumAssetUrl);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            expected = ExtractSha256(await response.Content.ReadAsStringAsync(cancellationToken), update.AssetName);
            if (string.IsNullOrWhiteSpace(expected))
                throw new InvalidOperationException($"The SHA-256 companion file does not contain a checksum for {update.AssetName}. Refusing to install without verification.");
        }

        if (string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException("The latest GitHub release asset could not be verified.");

        var actual = ComputeSha256(assetPath);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual)))
            throw new InvalidOperationException($"Update checksum mismatch. Expected SHA-256 {expected}, found {actual}.");
    }

    private static string PreparePortableExe(string assetPath, string stageRoot)
    {
        if (Path.GetExtension(assetPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            ValidateStagedExe(assetPath);
            return assetPath;
        }

        if (!Path.GetExtension(assetPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The update asset must be a portable .exe or .zip release artifact.");

        var extractRoot = Path.Combine(stageRoot, "extracted");
        if (Directory.Exists(extractRoot)) Directory.Delete(extractRoot, recursive: true);
        Directory.CreateDirectory(extractRoot);
        ZipFile.ExtractToDirectory(assetPath, extractRoot);
        var stagedExe = Directory.EnumerateFiles(extractRoot, PortableExeName, SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException($"The update archive does not contain {PortableExeName}.");
        ValidateStagedExe(stagedExe);
        return stagedExe;
    }

    private static void ValidateStagedExe(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length < 1024 * 1024)
            throw new InvalidOperationException("The downloaded update does not look like a valid app executable.");
    }

    private static void ValidateUpdateSignature(string stagedExe, string targetExe)
    {
        var current = TryReadSigningCertificate(targetExe);
        if (current is null) return;

        var staged = TryReadSigningCertificate(stagedExe)
            ?? throw new InvalidOperationException("The installed app is signed, but the downloaded update is not signed.");
        if (!string.Equals(current.Thumbprint, staged.Thumbprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The downloaded update is not signed by the same certificate as the installed app.");
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2? TryReadSigningCertificate(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var certificate = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
            return new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate);
        }
        catch
        {
            return null;
        }
    }

    private static (string Name, string Url, long Size) SelectPortableAsset(JsonArray? assets)
    {
        if (assets is null) return ("", "", 0);
        var candidates = assets
            .OfType<JsonObject>()
            .Select(asset => (
                Name: asset["name"]?.ToString() ?? "",
                Url: FirstNonBlank(asset["browser_download_url"]?.ToString(), asset["url"]?.ToString()),
                Size: JsonLong(asset["size"])))
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name) && !string.IsNullOrWhiteSpace(asset.Url))
            .ToList();

        return candidates.FirstOrDefault(asset => asset.Name.Equals(PortableExeName, StringComparison.OrdinalIgnoreCase))
            is var exact && !string.IsNullOrWhiteSpace(exact.Name) ? exact
            : candidates.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                is var zip && !string.IsNullOrWhiteSpace(zip.Name) ? zip
            : candidates.FirstOrDefault(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    private static (string Name, string Url) SelectChecksumAsset(JsonArray? assets, string assetName)
    {
        if (assets is null || string.IsNullOrWhiteSpace(assetName)) return ("", "");
        var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            assetName + ".sha256",
            assetName + ".sha256.txt",
            Path.ChangeExtension(assetName, ".sha256")
        };
        return assets
            .OfType<JsonObject>()
            .Select(asset => (
                Name: asset["name"]?.ToString() ?? "",
                Url: FirstNonBlank(asset["browser_download_url"]?.ToString(), asset["url"]?.ToString())))
            .FirstOrDefault(asset => expectedNames.Contains(asset.Name));
    }

    public static string ExtractSha256(string checksumText, string assetName = "")
    {
        foreach (var line in (checksumText ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"(?i)\b[a-f0-9]{64}\b");
            if (!match.Success) continue;
            if (string.IsNullOrWhiteSpace(assetName) || line.Contains(assetName, StringComparison.OrdinalIgnoreCase))
                return match.Value.ToLowerInvariant();
        }

        return "";
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string NormalizeSha256(string value)
    {
        var normalized = new string((value ?? "").Trim().Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
        return normalized.Length == 64 ? normalized : "";
    }

    private static bool TryParseGitHubRepository(string url, out string owner, out string repo)
    {
        owner = "";
        repo = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        owner = parts[0];
        repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
    }

    private static Version NormalizeVersion(string value)
    {
        var text = (value ?? "").Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text[1..];
        var prerelease = text.IndexOfAny(['-', '+']);
        if (prerelease >= 0) text = text[..prerelease];
        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) text += ".0.0";
        else if (parts.Length == 2) text += ".0";
        return Version.TryParse(text, out var version) ? version : new Version(0, 0, 0);
    }

    private static bool IsVersionNewer(Version latest, Version current) => latest.CompareTo(current) > 0;

    private static string VersionLabel(string value)
    {
        var text = (value ?? "").Trim();
        return text.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? text : $"v{text}";
    }

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private static long JsonLong(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<long>(out var number) ? number : 0;

    private static string RegexSafeFileName(string value)
        => string.Join("_", (value ?? "update").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string TrimReleaseNotes(string notes)
        => string.IsNullOrWhiteSpace(notes) ? "No release notes were provided." : notes.Trim().Length <= 4000 ? notes.Trim() : notes.Trim()[..4000] + "\n\n...";

    private static string PendingNoticePath(string workspaceRoot)
        => Path.Combine(workspaceRoot, "cache", "app-updates", "installed-update.json");

    private static string UpdaterScript() => """
param(
  [int] $ParentPid,
  [string] $SourceExe,
  [string] $TargetExe,
  [string] $NoticeSource,
  [string] $NoticeTarget,
  [string] $WorkingDirectory
)
$ErrorActionPreference = "Stop"
try { Wait-Process -Id $ParentPid -Timeout 90 } catch {}
Start-Sleep -Milliseconds 500
Copy-Item -LiteralPath $SourceExe -Destination $TargetExe -Force
if (Test-Path -LiteralPath $NoticeSource) {
  New-Item -ItemType Directory -Path (Split-Path -Parent $NoticeTarget) -Force | Out-Null
  Copy-Item -LiteralPath $NoticeSource -Destination $NoticeTarget -Force
}
Start-Process -FilePath $TargetExe -WorkingDirectory $WorkingDirectory | Out-Null
""";
}
