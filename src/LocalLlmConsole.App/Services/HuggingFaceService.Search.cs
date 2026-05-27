namespace LocalLlmConsole.Services;

public sealed record HuggingFaceModelReference(string Repo, string Path = "", string Revision = "")
{
    public bool HasFile => !string.IsNullOrWhiteSpace(Path);
}

public sealed partial class HuggingFaceService
{
    public async Task<IReadOnlyList<HuggingFaceFile>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<HuggingFaceFile>();
        if (TryParseModelReference(query, out var reference))
            return await SearchReferenceAsync(reference, cancellationToken);

        var url = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(query)}&filter=gguf&sort=downloads&direction=-1&limit=10";
        var repos = JsonNode.Parse(await _http.GetStringAsync(url, cancellationToken))?.AsArray() ?? new JsonArray();
        using var repoGate = new SemaphoreSlim(4);
        var repoResults = await Task.WhenAll(repos.Select(async repoNode =>
        {
            await repoGate.WaitAsync(cancellationToken);
            try
            {
                return await SearchRepoFilesAsync(repoNode, cancellationToken);
            }
            finally
            {
                repoGate.Release();
            }
        }));
        var results = repoResults.SelectMany(files => files).ToList();

        var selected = results
            .OrderByDescending(file => ScoreQuant(file.Quant))
            .ThenByDescending(file => file.Downloads)
            .Take(50)
            .ToArray();

        await ResolveMissingSizesAsync(selected, cancellationToken);

        return selected;
    }

    private async Task<IReadOnlyList<HuggingFaceFile>> SearchReferenceAsync(HuggingFaceModelReference reference, CancellationToken cancellationToken)
    {
        var repoNode = new JsonObject { ["modelId"] = reference.Repo };
        var files = (await SearchRepoFilesAsync(repoNode, cancellationToken))
            .OrderByDescending(file => ScoreQuant(file.Quant))
            .ThenByDescending(file => file.Downloads)
            .ToArray();

        if (reference.HasFile)
        {
            var requestedPath = NormalizeRemotePath(reference.Path);
            files = files
                .Where(file => string.Equals(NormalizeRemotePath(file.Path), requestedPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(reference.Revision))
                files = files.Select(file => file with { Revision = reference.Revision }).ToArray();
        }

        var selected = files.Take(50).ToArray();
        await ResolveMissingSizesAsync(selected, cancellationToken);
        return selected;
    }

    private async Task ResolveMissingSizesAsync(HuggingFaceFile[] selected, CancellationToken cancellationToken)
    {
        using var sizeGate = new SemaphoreSlim(8);
        await Task.WhenAll(Enumerable.Range(0, selected.Length).Select(async index =>
        {
            if (selected[index].SizeBytes > 0) return;
            await sizeGate.WaitAsync(cancellationToken);
            try
            {
                var resolvedSize = await ResolveFileSizeAsync(selected[index].Repo, selected[index].Path, selected[index].Revision, cancellationToken);
                if (resolvedSize > 0) selected[index] = selected[index] with { SizeBytes = resolvedSize };
            }
            finally
            {
                sizeGate.Release();
            }
        }));
    }

    public static bool TryParseModelReference(string input, out HuggingFaceModelReference reference)
    {
        reference = new HuggingFaceModelReference("");
        var text = (input ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Any(char.IsWhiteSpace)) return false;

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return TryParseModelReferenceUri(uri, out reference);

        var parts = text.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !IsSafeRepoSegment(parts[0]) || !IsSafeRepoSegment(parts[1])) return false;

        var path = parts.Length > 2 ? NormalizeRemotePath(string.Join("/", parts.Skip(2))) : "";
        if (!string.IsNullOrWhiteSpace(path) && !path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) return false;

        reference = new HuggingFaceModelReference($"{parts[0]}/{parts[1]}", path);
        return true;
    }

    public static bool TryCreateModelCardUrl(string repo, out string url)
    {
        url = "";
        var parts = (repo ?? "").Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IsSafeRepoSegment(parts[0]) || !IsSafeRepoSegment(parts[1])) return false;

        url = $"https://huggingface.co/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}";
        return true;
    }

    private static bool TryParseModelReferenceUri(Uri uri, out HuggingFaceModelReference reference)
    {
        reference = new HuggingFaceModelReference("");
        var host = uri.Host.Trim().ToLowerInvariant();
        if (host is not ("huggingface.co" or "www.huggingface.co" or "hf.co")) return false;

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (parts.Length < 2 || !IsSafeRepoSegment(parts[0]) || !IsSafeRepoSegment(parts[1])) return false;

        var repo = $"{parts[0]}/{parts[1]}";
        var revision = "";
        var path = "";
        if (parts.Length >= 5 && IsFileRoute(parts[2]))
        {
            revision = parts[3];
            path = NormalizeRemotePath(string.Join("/", parts.Skip(4)));
        }
        else if (parts.Length >= 3 && parts[2].EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
        {
            path = NormalizeRemotePath(string.Join("/", parts.Skip(2)));
        }

        if (!string.IsNullOrWhiteSpace(path) && !path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) return false;
        reference = new HuggingFaceModelReference(repo, path, revision);
        return true;
    }

    private static bool IsFileRoute(string value)
        => value.Equals("blob", StringComparison.OrdinalIgnoreCase)
            || value.Equals("resolve", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeRepoSegment(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= 96
            && value.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
            && !value.Contains("..", StringComparison.Ordinal);

    private static string NormalizeRemotePath(string value)
        => string.Join("/", (value ?? "").Replace('\\', '/').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private async Task<IReadOnlyList<HuggingFaceFile>> SearchRepoFilesAsync(JsonNode? repoNode, CancellationToken cancellationToken)
    {
        var repo = repoNode?["modelId"]?.ToString() ?? repoNode?["id"]?.ToString() ?? "";
        if (!repo.Contains('/')) return [];

        var info = await GetRepoInfoAsync(repo, repoNode, cancellationToken);
        var repoHintsText = string.Join(" ", info.Tags.Concat([info.PipelineTag, info.LibraryName, repo]));
        var files = new List<HuggingFaceFile>();
        foreach (var file in info.Detail?["siblings"]?.AsArray() ?? new JsonArray())
        {
            var path = file?["rfilename"]?.ToString() ?? "";
            var name = Path.GetFileName(path);
            if (!name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) continue;
            if (IsVisionProjectorFile(path) || name.Contains("clip", StringComparison.OrdinalIgnoreCase)) continue;
            var size = file?["size"]?.GetValue<long?>() ?? file?["lfs"]?["size"]?.GetValue<long?>() ?? 0;
            var sha256 = file?["lfs"]?["sha256"]?.ToString()
                ?? file?["lfs"]?["oid"]?.ToString()
                ?? "";
            var fileHints = CapabilityHints($"{repoHintsText} {path} {name}", info.HasVisionProjector, info.HasAdapter, info.HasDraftOrMtp);
            files.Add(new HuggingFaceFile(
                repo,
                path,
                name,
                ModelCatalogService.InferQuant(name),
                size,
                info.Downloads,
                info.PipelineTag,
                info.LibraryName,
                info.Tags,
                info.HasVisionProjector,
                info.HasConfig,
                info.HasTokenizer,
                info.HasAdapter,
                info.HasDraftOrMtp,
                MergeHints(info.CapabilityHints, fileHints),
                info.Revision,
                NormalizeSha256(sha256),
                info.License,
                info.VisionProjectorPath,
                info.VisionProjectorName,
                info.VisionProjectorSizeBytes,
                info.VisionProjectorSha256));
        }

        return files;
    }

    private async Task<RepoInfo> GetRepoInfoAsync(string repo, JsonNode? fallback, CancellationToken cancellationToken)
    {
        if (_repoInfoCache.TryGetValue(repo, out var cached)) return cached;

        var detailUrl = $"https://huggingface.co/api/models/{Uri.EscapeDataString(repo).Replace("%2F", "/")}?blobs=true";
        JsonNode? detail = fallback;
        try { detail = JsonNode.Parse(await _http.GetStringAsync(detailUrl, cancellationToken)); } catch { }
        var downloads = detail?["downloads"]?.GetValue<long?>() ?? 0;
        var tags = (detail?["tags"]?.AsArray() ?? new JsonArray())
            .Select(tag => tag?.ToString() ?? "")
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pipelineTag = detail?["pipeline_tag"]?.ToString() ?? "";
        var libraryName = detail?["library_name"]?.ToString() ?? "";
        var siblingNodes = (detail?["siblings"]?.AsArray() ?? new JsonArray()).ToArray();
        var siblings = siblingNodes
            .Select(file => file?["rfilename"]?.ToString() ?? "")
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        var projector = siblingNodes
            .Select(file => new
            {
                Path = file?["rfilename"]?.ToString() ?? "",
                Size = file?["size"]?.GetValue<long?>() ?? file?["lfs"]?["size"]?.GetValue<long?>() ?? 0,
                Sha256 = NormalizeSha256(file?["lfs"]?["sha256"]?.ToString()
                    ?? file?["lfs"]?["oid"]?.ToString()
                    ?? "")
            })
            .Where(file => IsVisionProjectorFile(file.Path))
            .OrderBy(file => Path.GetFileName(file.Path).Contains("f16", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var hasVisionProjector = siblings.Any(IsVisionProjectorFile);
        var hasConfig = siblings.Any(path => string.Equals(Path.GetFileName(path), "config.json", StringComparison.OrdinalIgnoreCase));
        var hasTokenizer = siblings.Any(path => Path.GetFileName(path).StartsWith("tokenizer", StringComparison.OrdinalIgnoreCase));
        var hasAdapter = siblings.Any(path =>
            path.Contains("lora", StringComparison.OrdinalIgnoreCase)
            || path.Contains("adapter", StringComparison.OrdinalIgnoreCase));
        var hasDraftOrMtp = siblings.Any(path =>
            path.Contains("draft", StringComparison.OrdinalIgnoreCase)
            || path.Contains("mtp", StringComparison.OrdinalIgnoreCase)
            || path.Contains("speculative", StringComparison.OrdinalIgnoreCase));
        var revision = detail?["sha"]?.ToString() ?? "";
        var license = ExtractLicense(tags);
        var repoHintsText = string.Join(" ", tags.Concat([pipelineTag, libraryName, repo]));
        var info = new RepoInfo(
            repo,
            downloads,
            pipelineTag,
            libraryName,
            tags,
            siblings,
            hasVisionProjector,
            hasConfig,
            hasTokenizer,
            hasAdapter,
            hasDraftOrMtp,
            CapabilityHints(repoHintsText, hasVisionProjector, hasAdapter, hasDraftOrMtp),
            revision,
            license,
            projector?.Path ?? "",
            string.IsNullOrWhiteSpace(projector?.Path) ? "" : Path.GetFileName(projector.Path),
            projector?.Size ?? 0,
            projector?.Sha256 ?? "",
            detail);
        _repoInfoCache.TryAdd(repo, info);
        return info;
    }

    private async Task<long> ResolveFileSizeAsync(string repo, string path, string revision, CancellationToken cancellationToken)
    {
        var url = ResolveUrl(repo, path, revision);
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await _http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (headResponse.Content.Headers.ContentLength is > 0 and var headLength) return headLength;
        }
        catch { }

        try
        {
            using var range = new HttpRequestMessage(HttpMethod.Get, url);
            range.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var rangeResponse = await _http.SendAsync(range, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (rangeResponse.Content.Headers.ContentRange?.Length is > 0 and var rangeLength) return rangeLength;
            if (rangeResponse.Content.Headers.ContentLength is > 1 and var contentLength) return contentLength;
        }
        catch { }

        return 0;
    }

    private static string ResolveUrl(string repo, string path, string revision)
    {
        var safeRevision = string.IsNullOrWhiteSpace(revision) ? "main" : revision.Trim();
        if (safeRevision.Contains("..", StringComparison.Ordinal) || safeRevision.Contains('/') || safeRevision.Contains('\\'))
            safeRevision = "main";
        var repoPath = string.Join("/", repo.Split('/').Select(Uri.EscapeDataString));
        var filePath = string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        return $"https://huggingface.co/{repoPath}/resolve/{Uri.EscapeDataString(safeRevision)}/{filePath}";
    }
}
