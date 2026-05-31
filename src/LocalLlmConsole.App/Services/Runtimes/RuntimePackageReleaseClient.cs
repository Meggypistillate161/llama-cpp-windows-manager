using System.Net.Http.Headers;

namespace LocalLlmConsole.Services;

public static class RuntimePackageReleaseClient
{
    public static async Task<RuntimePackageRelease> FetchLatestReleaseAsync(HttpClient client, CancellationToken cancellationToken = default)
        => await FetchLatestReleaseAsync(client, null, cancellationToken);

    public static async Task<RuntimePackageRelease> FetchLatestReleaseAsync(HttpClient client, RuntimePackagePreset? preset, CancellationToken cancellationToken = default)
    {
        var apiUrl = RuntimePackageSourceCatalog.ReleaseApiUrlFor(preset);
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("LocalLlmConsole", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(IsHuggingFaceApiUrl(apiUrl) ? "application/json" : "application/vnd.github+json"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return IsHuggingFaceApiUrl(apiUrl) ? ParseHuggingFaceModelJson(json, preset) : ParseReleaseJson(json);
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
            assets.Add(new RuntimePackageAsset(name, url, LongValue(assetNode["size"]), AssetSha256(assetNode)));
        }

        if (assets.Count == 0)
            throw new InvalidOperationException($"Release {tag} did not include usable downloadable assets.");

        var verifiedAssets = AttachChecksumCompanions(assets);
        return new RuntimePackageRelease(
            tag,
            root["target_commitish"]?.ToString() ?? "",
            root["html_url"]?.ToString() ?? $"{RuntimePackageSourceCatalog.ReleasesUrl}/tag/{tag}",
            DateTimeOffset.TryParse(root["published_at"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var publishedAt)
                ? publishedAt
                : DateTimeOffset.MinValue,
            verifiedAssets);
    }

    public static RuntimePackageRelease ParseHuggingFaceModelJson(string json, RuntimePackagePreset? preset = null)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Hugging Face model response was empty.");
        var modelId = root["id"]?.ToString() ?? root["modelId"]?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(modelId))
            throw new InvalidOperationException("Hugging Face model response did not include a model id.");

        var sha = root["sha"]?.ToString() ?? "";
        var revision = string.IsNullOrWhiteSpace(sha) ? "main" : sha;
        var tag = string.IsNullOrWhiteSpace(sha) ? "hf-latest" : $"hf-{sha[..Math.Min(12, sha.Length)]}";
        var assetsNode = root["siblings"] as JsonArray;
        if (assetsNode is null || assetsNode.Count == 0)
            throw new InvalidOperationException($"Hugging Face model {modelId} did not include downloadable files.");

        var assets = new List<RuntimePackageAsset>();
        foreach (var assetNode in assetsNode.OfType<JsonObject>())
        {
            var name = assetNode["rfilename"]?.ToString() ?? "";
            if (!IsDownloadableHuggingFaceFile(name)) continue;
            assets.Add(new RuntimePackageAsset(
                name,
                $"{HuggingFaceModelPageUrl(modelId)}/resolve/{Uri.EscapeDataString(revision)}/{EscapeHuggingFacePath(name)}?download=true",
                LongValue(assetNode["size"]),
                AssetSha256(assetNode)));
        }

        if (assets.Count == 0)
            throw new InvalidOperationException($"Hugging Face model {modelId} did not include usable downloadable files.");

        var pageUrl = preset is null || string.IsNullOrWhiteSpace(preset.ReleasePageUrl)
            ? HuggingFaceModelPageUrl(modelId)
            : preset.ReleasePageUrl;
        return new RuntimePackageRelease(
            tag,
            sha,
            pageUrl,
            DateTimeOffset.TryParse(root["lastModified"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var publishedAt)
                ? publishedAt
                : DateTimeOffset.MinValue,
            assets);
    }

    private static IReadOnlyList<RuntimePackageAsset> AttachChecksumCompanions(IReadOnlyList<RuntimePackageAsset> assets)
        => assets
            .Select(asset => string.IsNullOrWhiteSpace(asset.ChecksumUrl)
                ? asset with { ChecksumUrl = ChecksumUrlFor(assets, asset.Name) }
                : asset)
            .ToArray();

    private static string ChecksumUrlFor(IReadOnlyList<RuntimePackageAsset> assets, string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName)) return "";
        var expectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            assetName + ".sha256",
            assetName + ".sha256.txt",
            assetName + ".sha256sum",
            Path.ChangeExtension(assetName, ".sha256")
        };
        return assets.FirstOrDefault(asset => expectedNames.Contains(asset.Name))?.DownloadUrl ?? "";
    }

    private static string AssetSha256(JsonObject assetNode)
    {
        foreach (var value in new[]
        {
            assetNode["digest"]?.ToString(),
            assetNode["sha256"]?.ToString(),
            assetNode["checksum"]?.ToString(),
            assetNode["lfs"]?["sha256"]?.ToString()
        })
        {
            var sha256 = RuntimePackageAssetVerifier.NormalizeSha256(value ?? "");
            if (!string.IsNullOrWhiteSpace(sha256)) return sha256;
        }

        return "";
    }

    private static bool IsHuggingFaceApiUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/api/models/", StringComparison.OrdinalIgnoreCase);

    private static string HuggingFaceModelPageUrl(string modelId)
        => $"https://huggingface.co/{modelId.Trim('/')}";

    private static string EscapeHuggingFacePath(string path)
        => string.Join("/", (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    private static bool IsDownloadableHuggingFaceFile(string name)
        => !string.IsNullOrWhiteSpace(name)
            && !name.Equals(".gitattributes", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("README.md", StringComparison.OrdinalIgnoreCase);

    private static long LongValue(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<long>(out var result)) return result;
        return long.TryParse(node?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
