using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public void ModelCatalogFindsAdjacentDraftModel()
    {
        var root = CreateTempRoot();
        var models = Path.Combine(root, "models");
        Directory.CreateDirectory(models);
        var main = Path.Combine(models, "Qwen3-main.gguf");
        var draft = Path.Combine(models, "Qwen3-MTP-draft.gguf");
        var projector = Path.Combine(models, "mmproj-model.gguf");
        File.WriteAllText(main, "main");
        File.WriteAllText(draft, "draft");
        File.WriteAllText(projector, "projector");

        var found = ModelCatalogService.FindDraftModel(main);

        Assert.Equal(Path.GetFullPath(draft), Path.GetFullPath(found!));
    }


    [Fact]
    public async Task DownloadRecoveryRejectsDestinationOutsideModelsRoot()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var jobs = new JobEngine(store, Path.Combine(root, "logs"));
        var catalog = new ModelCatalogService(store);
        var huggingFace = new HuggingFaceService(store, jobs, catalog);
        var settings = AppSettings.CreateDefault(root);
        var outside = Path.Combine(root, "outside", "model.gguf");
        Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
        await File.WriteAllTextAsync(outside, "external file must not become app-owned", TestContext.Current.CancellationToken);

        var file = new HuggingFaceFile("owner/repo", "model.gguf", "model.gguf", "", 0, 0);
        var payload = new DownloadJobPayload(file, outside);
        var now = DateTimeOffset.UtcNow;
        await store.UpsertJobAsync(new JobRecord(
            "huggingface-download-test",
            "huggingface-download",
            JobStatus.Running,
            System.Text.Json.JsonSerializer.Serialize(payload),
            Path.Combine(root, "logs", "job.log"),
            now,
            now));

        await huggingFace.RecoverInterruptedDownloadsAsync(settings);

        var job = Assert.Single(await store.ListJobsAsync());
        Assert.Equal(JobStatus.Failed, job.Status);
        var recoveredPayload = HuggingFaceService.ParseDownloadPayload(job.PayloadJson);
        Assert.NotNull(recoveredPayload);
        Assert.Contains("outside", recoveredPayload.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(outside));
        Assert.Empty(await store.ListModelsAsync());
    }


    [Fact]
    public async Task DownloadRecoveryRejectsUnsafeWindowsFilenames()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var jobs = new JobEngine(store, Path.Combine(root, "logs"));
        var catalog = new ModelCatalogService(store);
        var huggingFace = new HuggingFaceService(store, jobs, catalog);
        var settings = AppSettings.CreateDefault(root);

        var file = new HuggingFaceFile("owner/repo", "bad:name.gguf", "bad:name.gguf", "", 1, 0);
        var payload = new DownloadJobPayload(file, Path.Combine(settings.ModelsRoot, "repo-bad", "bad:name.gguf"));
        var now = DateTimeOffset.UtcNow;
        await store.UpsertJobAsync(new JobRecord(
            "huggingface-download-unsafe-name",
            "huggingface-download",
            JobStatus.Running,
            System.Text.Json.JsonSerializer.Serialize(payload),
            Path.Combine(root, "logs", "job.log"),
            now,
            now));

        await huggingFace.RecoverInterruptedDownloadsAsync(settings);

        var job = Assert.Single(await store.ListJobsAsync());
        Assert.Equal(JobStatus.Failed, job.Status);
        var recoveredPayload = HuggingFaceService.ParseDownloadPayload(job.PayloadJson);
        Assert.NotNull(recoveredPayload);
        Assert.Contains("filename", recoveredPayload.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await store.ListModelsAsync());
    }


    [Fact]
    public async Task HuggingFaceDownloadRejectsUnsafeVisionProjectorMetadata()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var jobs = new JobEngine(store, Path.Combine(root, "logs"));
        var catalog = new ModelCatalogService(store);
        var huggingFace = new HuggingFaceService(store, jobs, catalog);
        var settings = AppSettings.CreateDefault(root);
        var file = new HuggingFaceFile("owner/repo", "model.gguf", "model.gguf", "", 1, 0)
        {
            HasVisionProjector = true,
            VisionProjectorPath = "projector.txt",
            VisionProjectorName = "projector.txt"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => huggingFace.StartDownloadAsync(file, settings, TestContext.Current.CancellationToken));

        Assert.Contains("vision projector", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await store.ListJobsAsync());
    }


    [Fact]
    public async Task DownloadRecoveryRejectsCompletedFileWithoutSizeOrChecksum()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var jobs = new JobEngine(store, Path.Combine(root, "logs"));
        var catalog = new ModelCatalogService(store);
        var huggingFace = new HuggingFaceService(store, jobs, catalog);
        var settings = AppSettings.CreateDefault(root);
        var destination = Path.Combine(settings.ModelsRoot, "repo-model", "model.gguf");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(destination, "untrusted complete file", TestContext.Current.CancellationToken);

        var file = new HuggingFaceFile("owner/repo", "model.gguf", "model.gguf", "", 0, 0);
        var payload = new DownloadJobPayload(file, destination);
        var now = DateTimeOffset.UtcNow;
        await store.UpsertJobAsync(new JobRecord(
            "huggingface-download-no-integrity",
            "huggingface-download",
            JobStatus.Running,
            System.Text.Json.JsonSerializer.Serialize(payload),
            Path.Combine(root, "logs", "job.log"),
            now,
            now));

        await huggingFace.RecoverInterruptedDownloadsAsync(settings);

        var job = Assert.Single(await store.ListJobsAsync());
        Assert.Equal(JobStatus.Failed, job.Status);
        var recoveredPayload = HuggingFaceService.ParseDownloadPayload(job.PayloadJson);
        Assert.NotNull(recoveredPayload);
        Assert.Contains("expected size", recoveredPayload.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(destination));
        Assert.Empty(await store.ListModelsAsync());
    }


    [Fact]
    public void HuggingFaceInstallStateDetectsInstalledFilesFromMetadataAndPaths()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var file = new HuggingFaceFile("owner/repo", "folder/model.gguf", "model.gguf", "Q4", 2048, 0);
        var expected = HuggingFaceInstallStateService.ExpectedDestination(file, settings.ModelsRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
        File.WriteAllText(expected, "installed");
        var now = DateTimeOffset.UtcNow;
        var byMetadata = new ModelRecord(
            "model-1",
            "Model",
            Path.Combine(settings.ModelsRoot, "other", "other.gguf"),
            OwnershipKind.AppOwned,
            """{"file":{"repo":"owner/repo","path":"folder/model.gguf"}}""",
            now);
        var byFileName = byMetadata with
        {
            Id = "model-2",
            ModelPath = Path.Combine(settings.ModelsRoot, "another", "model.gguf"),
            MetadataJson = "{}"
        };

        var metadataInventory = HuggingFaceInstallStateService.BuildInventory([byMetadata]);
        var fileNameInventory = HuggingFaceInstallStateService.BuildInventory([byFileName]);
        var emptyInventory = HuggingFaceInstallStateService.BuildInventory([]);

        Assert.Contains("owner/repo|folder/model.gguf", metadataInventory.Keys);
        Assert.True(HuggingFaceInstallStateService.IsInstalled(file, metadataInventory, settings.ModelsRoot));
        Assert.True(HuggingFaceInstallStateService.IsInstalled(file, fileNameInventory, settings.ModelsRoot));
        Assert.True(HuggingFaceInstallStateService.IsInstalled(file, emptyInventory, settings.ModelsRoot));
        Assert.Equal("50% (1 KB)", HuggingFaceInstallStateService.FormatDownloadProgress(new DownloadJobPayload(file, expected, 1024, 2048)));
        Assert.Equal("Retry", HuggingFaceInstallStateService.DownloadStartLabel(JobStatus.Failed));
        Assert.True(HuggingFaceInstallStateService.CanStartDownload(JobStatus.Cancelled));
        Assert.True(HuggingFaceInstallStateService.CanPauseDownload(JobStatus.Running));
        Assert.True(HuggingFaceInstallStateService.CanStopDownload(JobStatus.Paused));

        var pairedFile = file with
        {
            HasVisionProjector = true,
            VisionProjectorPath = "mmproj/model-mmproj.gguf",
            VisionProjectorName = "model-mmproj.gguf",
            VisionProjectorSizeBytes = 4096,
            VisionProjectorSha256 = new string('a', 64)
        };
        var pairedPayload = HuggingFaceService.ParseDownloadPayload(System.Text.Json.JsonSerializer.Serialize(new DownloadJobPayload(pairedFile, expected)));

        Assert.NotNull(pairedPayload);
        Assert.Equal("mmproj/model-mmproj.gguf", pairedPayload.File.VisionProjectorPath);
        Assert.Equal(4096, pairedPayload.File.VisionProjectorSizeBytes);
    }


    [Fact]
    public void CompletedDownloadsRegisterAndRefreshBeforeOptionalEnrichmentFinishes()
    {
        var serviceSource = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "HuggingFaceService.Safety.cs"));
        var downloadHistorySource = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.DownloadHistory.cs"));
        var registerIndex = serviceSource.IndexOf("await RegisterDownloadedHuggingFaceModelAsync(settings, file, destination, timestamp, recovered, new VisionProjectorDownloadResult(\"\", \"\"));", StringComparison.Ordinal);
        var completedIndex = serviceSource.IndexOf("await _jobs.UpdateAsync(job, JobStatus.Completed, JsonSerializer.Serialize(new DownloadJobPayload(file, destination, completedBytes, completedBytes), JsonOptions), cancellationToken);", StringComparison.Ordinal);
        var projectorIndex = serviceSource.IndexOf("var projector = await TryDownloadVisionProjectorAsync(settings, file, destination, cancellationToken);", StringComparison.Ordinal);

        Assert.Contains("CompleteVerifiedPrimaryModelAsync", serviceSource, StringComparison.Ordinal);
        Assert.True(registerIndex >= 0);
        Assert.True(completedIndex > registerIndex);
        Assert.True(projectorIndex > completedIndex);
        Assert.Contains("Optional post-download setup skipped", serviceSource, StringComparison.Ordinal);
        Assert.Contains("while (_huggingFace?.IsDownloadActive(jobId) == true && !await DownloadJobReachedTerminalStatusAsync(jobId))", downloadHistorySource, StringComparison.Ordinal);
        Assert.Contains("var refresh = await Dispatcher.InvokeAsync(RefreshCompletedDownloadAsync);", downloadHistorySource, StringComparison.Ordinal);
        Assert.Contains("await _catalog.ScanAsync(_settings.ModelsRoot);", downloadHistorySource, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("owner/repo", "owner/repo", "", "")]
    [InlineData("owner/repo/folder/model-q4.gguf", "owner/repo", "folder/model-q4.gguf", "")]
    [InlineData("https://huggingface.co/owner/repo", "owner/repo", "", "")]
    [InlineData("https://huggingface.co/owner/repo/blob/main/folder/model%20q4.gguf", "owner/repo", "folder/model q4.gguf", "main")]
    [InlineData("https://hf.co/owner/repo/resolve/main/model.gguf?download=true", "owner/repo", "model.gguf", "main")]
    public void HuggingFaceSearchParsesDirectRepoAndFileReferences(string input, string repo, string path, string revision)
    {
        Assert.True(HuggingFaceService.TryParseModelReference(input, out var reference));
        Assert.Equal(repo, reference.Repo);
        Assert.Equal(path, reference.Path);
        Assert.Equal(revision, reference.Revision);
    }


    [Theory]
    [InlineData("")]
    [InlineData("plain search text")]
    [InlineData("https://example.com/owner/repo")]
    [InlineData("owner/repo/config.json")]
    [InlineData("owner/../repo/model.gguf")]
    public void HuggingFaceSearchRejectsNonDirectReferences(string input)
    {
        Assert.False(HuggingFaceService.TryParseModelReference(input, out _));
    }


    [Theory]
    [InlineData("owner/repo", "https://huggingface.co/owner/repo")]
    [InlineData(" owner.name/repo_name ", "https://huggingface.co/owner.name/repo_name")]
    public void HuggingFaceModelCardUrlsRequireSafeRepoIds(string repo, string expected)
    {
        Assert.True(HuggingFaceService.TryCreateModelCardUrl(repo, out var url));
        Assert.Equal(expected, url);

        Assert.False(HuggingFaceService.TryCreateModelCardUrl("owner/../repo", out _));
        Assert.False(HuggingFaceService.TryCreateModelCardUrl("https://example.com/owner/repo", out _));
    }


    [Fact]
    public void ModelCapabilityServiceInfersCapabilitiesFromModelMetadata()
    {
        var root = CreateTempRoot();
        var modelPath = Path.Combine(root, "Qwen3-VL-Q4_K_M.gguf");
        var model = new ModelRecord(
            "qwen3-vl",
            "Qwen3 VL Reasoning MoE Embed FIM",
            modelPath,
            OwnershipKind.External,
            """{"tags":["image-text-to-text","reasoning","feature-extraction","fim","moe"],"HasVisionProjector":true}""",
            DateTimeOffset.UtcNow);

        var capabilities = ModelCapabilityService.Inspect(model);
        var summary = ModelCapabilityService.SummaryText(capabilities);
        var context = ModelCapabilityService.ContextLength(
            new Dictionary<string, object?> { ["qwen3.context_length"] = "32768" },
            "qwen3");

        Assert.Equal("Q4_K_M", capabilities.Quantization);
        Assert.True(capabilities.HasVisionProjector);
        Assert.True(capabilities.LikelyVision);
        Assert.True(capabilities.LikelyReasoning);
        Assert.True(capabilities.IsEmbedding);
        Assert.True(capabilities.IsFim);
        Assert.True(capabilities.IsMoe);
        Assert.False(capabilities.HasMetadata);
        Assert.Equal(32768, context);
        Assert.Contains("Vision: mmproj found", summary, StringComparison.Ordinal);
        Assert.Contains("GGUF metadata: unavailable", summary, StringComparison.Ordinal);
        Assert.True(ModelCapabilityService.LooksVisionCapable("llama-3.2-vision"));
    }


    [Fact]
    public void ModelCapabilityCacheKeyTracksVisionProjectorPairing()
    {
        var root = CreateTempRoot();
        var modelPath = Path.Combine(root, "Qwen3-VL-Q4_K_M.gguf");
        File.WriteAllText(modelPath, "fake model");
        var model = new ModelRecord(
            "qwen3-vl",
            "Qwen3 VL",
            modelPath,
            OwnershipKind.External,
            """{"CapabilityHints":"vision"}""",
            DateTimeOffset.UtcNow);

        var beforeKey = ModelCapabilityService.CacheKey(model);
        var before = ModelCapabilityService.Inspect(model);
        Assert.True(before.LikelyVision);
        Assert.False(before.HasVisionProjector);
        Assert.Contains("projector not found", ModelCapabilityService.SummaryText(before), StringComparison.OrdinalIgnoreCase);

        File.WriteAllText(Path.Combine(root, "mmproj-qwen3-vl.gguf"), "projector");
        var afterKey = ModelCapabilityService.CacheKey(model);
        var after = ModelCapabilityService.Inspect(model);

        Assert.NotEqual(beforeKey, afterKey);
        Assert.True(after.HasVisionProjector);
        Assert.Contains("mmproj found", ModelCapabilityService.SummaryText(after), StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task DownloadRegistrationRejectsAppOwnedPathOutsideModelsRoot()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var catalog = new ModelCatalogService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.RegisterDownloadedAsync(Path.Combine(root, "models"), "Model", Path.Combine(root, "outside", "model.gguf"), "{}"));
    }


    [Fact]
    public async Task ModelCatalogAddsGgufManifestToRegisteredModels()
    {
        var root = CreateTempRoot();
        var modelsRoot = Path.Combine(root, "models");
        Directory.CreateDirectory(modelsRoot);
        var modelPath = Path.Combine(modelsRoot, "Qwen3-Q4_K_M.gguf");
        WriteMinimalGguf(modelPath);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var catalog = new ModelCatalogService(store);

        var record = await catalog.RegisterDownloadedAsync(modelsRoot, "Qwen3", modelPath, """{"source":"test"}""");
        var metadata = System.Text.Json.Nodes.JsonNode.Parse(record.MetadataJson)!;

        Assert.Equal("test", metadata["source"]?.ToString());
        Assert.Equal("true", metadata["ggufMetadataAvailable"]?.ToString().ToLowerInvariant());
        Assert.Equal("qwen3", metadata["ggufArchitecture"]?.ToString());
        Assert.Equal("Q4_K_M", metadata["ggufQuantization"]?.ToString());
        Assert.Equal("32768", metadata["ggufContextLength"]?.ToString());
        Assert.Equal("true", metadata["ggufHasChatTemplate"]?.ToString().ToLowerInvariant());
    }


    [Fact]
    public async Task DownloadRegistrationCollapsesExternalDuplicateForSameModelPath()
    {
        var root = CreateTempRoot();
        var modelsRoot = Path.Combine(root, "models");
        var modelPath = Path.Combine(modelsRoot, "repo-model", "Model-Q4_K_M.gguf");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        WriteMinimalGguf(modelPath);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var catalog = new ModelCatalogService(store);
        var external = new ModelRecord(
            "external-model",
            "External Model",
            modelPath,
            OwnershipKind.External,
            "{}",
            DateTimeOffset.UtcNow);
        var settings = ModelLaunchSettings.FromAppSettings(AppSettings.CreateDefault(root) with { Port = 8099 });
        await store.UpsertModelAsync(external);
        await store.SaveModelLaunchSettingsAsync(external.Id, settings);

        var appOwned = await catalog.RegisterDownloadedAsync(modelsRoot, "Model-Q4_K_M.gguf", modelPath, """{"source":"download"}""");
        var models = await store.ListModelsAsync();

        Assert.Equal("Model Q4 K M", appOwned.Name);
        Assert.Single(models);
        Assert.Equal(appOwned.Id, models[0].Id);
        Assert.Equal("Model Q4 K M", models[0].Name);
        Assert.Equal(OwnershipKind.AppOwned, models[0].Ownership);
        Assert.Equal(Path.GetFullPath(modelPath), Path.GetFullPath(models[0].ModelPath));
        Assert.Null(await store.GetModelLaunchSettingsAsync(external.Id));
        Assert.Equal(8099, (await store.GetModelLaunchSettingsAsync(appOwned.Id))?.Port);
    }


    [Fact]
    public async Task ModelCatalogScanCollapsesExistingAppOwnedDuplicateForSameModelPath()
    {
        var root = CreateTempRoot();
        var modelsRoot = Path.Combine(root, "models");
        var modelPath = Path.Combine(modelsRoot, "repo-model", "Model-Q4_K_M.gguf");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        WriteMinimalGguf(modelPath);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var catalog = new ModelCatalogService(store);
        var appOwned = new ModelRecord(
            "app-owned-model",
            "App Model",
            modelPath,
            OwnershipKind.AppOwned,
            "{}",
            DateTimeOffset.UtcNow);
        var external = new ModelRecord(
            "external-model",
            "External Model",
            modelPath,
            OwnershipKind.External,
            "{}",
            DateTimeOffset.UtcNow);
        var settings = ModelLaunchSettings.FromAppSettings(AppSettings.CreateDefault(root) with { Port = 8098 });
        await store.UpsertModelAsync(external);
        await store.SaveModelLaunchSettingsAsync(external.Id, settings);
        await store.UpsertModelAsync(appOwned);

        await catalog.ScanAsync(modelsRoot);
        var models = await store.ListModelsAsync();

        Assert.Single(models);
        Assert.Equal(appOwned.Id, models[0].Id);
        Assert.Equal(OwnershipKind.AppOwned, models[0].Ownership);
        Assert.Null(await store.GetModelLaunchSettingsAsync(external.Id));
        Assert.Equal(8098, (await store.GetModelLaunchSettingsAsync(appOwned.Id))?.Port);
    }


    [Fact]
    public async Task ModelCatalogCleanupCollapsesDuplicateRecordsWithoutFilesystemScan()
    {
        var root = CreateTempRoot();
        var modelsRoot = Path.Combine(root, "models");
        var modelPath = Path.Combine(modelsRoot, "repo-model", "Model-Q4_K_M.gguf");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        WriteMinimalGguf(modelPath);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var catalog = new ModelCatalogService(store);
        var appOwned = new ModelRecord("app-owned-model", "App Model", modelPath, OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);
        var external = new ModelRecord("external-model", "External Model", modelPath, OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        await store.UpsertModelAsync(external);
        await store.UpsertModelAsync(appOwned);

        var removed = await catalog.CleanupDuplicateModelRecordsAsync();
        var models = await store.ListModelsAsync();

        Assert.Equal(1, removed);
        Assert.Single(models);
        Assert.Equal(appOwned.Id, models[0].Id);
    }


    [Fact]
    public async Task ModelCatalogCleanupRemovesGgufExtensionFromDisplayNames()
    {
        var root = CreateTempRoot();
        var modelsRoot = Path.Combine(root, "models");
        var modelPath = Path.Combine(modelsRoot, "repo-model", "Qwen3.5-9B-Q4_K_M.gguf");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        WriteMinimalGguf(modelPath);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var catalog = new ModelCatalogService(store);
        await store.UpsertModelAsync(new ModelRecord(
            "app-owned-model",
            "Qwen3.5-9B-Q4_K_M.gguf",
            modelPath,
            OwnershipKind.AppOwned,
            "{}",
            DateTimeOffset.UtcNow));

        var changed = await catalog.CleanupModelRecordsAsync();
        var model = Assert.Single(await store.ListModelsAsync());

        Assert.Equal(1, changed);
        Assert.Equal("Qwen3.5 9B Q4 K M", model.Name);
        Assert.DoesNotContain(".gguf", model.Name, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void HuggingFaceSuggestedLaunchSettingsPreferLlamaServerCommand()
    {
        var defaults = AppSettings.CreateDefault(CreateTempRoot());
        const string readme = """
        ## Start the server

        ```bash
        llama-server -m Qwen3.6-27B-Q8_0-mtp.gguf \
          --spec-type draft-mtp --spec-draft-n-max 3 \
          --cache-type-k q8_0 --cache-type-v q8_0 \
          -np 1 -c 262144 --temp 0.7 --top-k 20 -ngl 99 --port 8081
        ```

        ## Direct CLI usage

        ```bash
        llama-cli -m Qwen3.6-27B-Q8_0-mtp.gguf -c 4096 -n 2048 --temp 0.2
        ```
        """;

        var settings = HuggingFaceLaunchSettingsSuggester.TryCreate(defaults, readme, """{"temperature":0.3,"top_p":0.8}""");

        Assert.NotNull(settings);
        Assert.Equal("draft-mtp", settings.SpeculativeType);
        Assert.Equal(3, settings.SpecDraftMaxTokens);
        Assert.Equal("q8_0", settings.CacheTypeK);
        Assert.Equal("q8_0", settings.CacheTypeV);
        Assert.Equal(1, settings.ParallelSlots);
        Assert.Equal(262_144, settings.ContextSize);
        Assert.Equal(0.7, settings.Temperature);
        Assert.Equal(20, settings.TopK);
        Assert.Equal(0.8, settings.TopP);
        Assert.Equal(99, settings.GpuLayers);
        Assert.Equal(-1, settings.MaxTokens);
    }


    [Fact]
    public void HuggingFaceSuggestedLaunchSettingsParseInlineEqualsQuotedPathsAndDraftOptions()
    {
        var defaults = AppSettings.CreateDefault(CreateTempRoot());
        const string readme = """
        ```bash
        llama-server --ctx-size=32768 --top-p=0.92 --min-p=0.05 \
          --repeat-penalty=1.08 --presence-penalty=-0.2 --frequency-penalty=0.1 \
          --image-min-tokens=256 --image-max-tokens=1024 \
          --flash-attn=on --rope-scaling=yarn --rope-scale=2 --rope-freq-base=1000000 --rope-freq-scale=0.5 \
          --spec-type=draft-simple --model-draft "D:\models\draft model.gguf" --spec-draft-ngl=10 \
          --spec-draft-n-min=1 --draft-p-split=0.45 --draft-p-min=0.12 \
          --cache-type-k-draft=q4_0 --cache-type-v-draft=q5_1
        ```
        """;

        var settings = HuggingFaceLaunchSettingsSuggester.TryCreate(defaults, readme);

        Assert.NotNull(settings);
        Assert.Equal(32_768, settings.ContextSize);
        Assert.Equal(0.92, settings.TopP);
        Assert.Equal(0.05, settings.MinP);
        Assert.Equal(1.08, settings.RepeatPenalty);
        Assert.Equal(-0.2, settings.PresencePenalty);
        Assert.Equal(0.1, settings.FrequencyPenalty);
        Assert.Equal(256, settings.VisionImageMinTokens);
        Assert.Equal(1024, settings.VisionImageMaxTokens);
        Assert.Equal("on", settings.FlashAttention);
        Assert.Equal("yarn", settings.RopeScaling);
        Assert.Equal(2, settings.RopeScale);
        Assert.Equal(1_000_000, settings.RopeFreqBase);
        Assert.Equal(0.5, settings.RopeFreqScale);
        Assert.Equal("draft-simple", settings.SpeculativeType);
        Assert.Equal(@"D:\models\draft model.gguf", settings.SpecDraftModelPath);
        Assert.Equal(10, settings.SpecDraftGpuLayers);
        Assert.Equal(1, settings.SpecDraftMinTokens);
        Assert.Equal(0.45, settings.SpecDraftPSplit);
        Assert.Equal(0.12, settings.SpecDraftPMin);
        Assert.Equal("q4_0", settings.SpecDraftCacheTypeK);
        Assert.Equal("q5_1", settings.SpecDraftCacheTypeV);
    }


    [Fact]
    public void HuggingFaceSuggestedLaunchSettingsApplyConfigJsonAndFallbackToCli()
    {
        var defaults = AppSettings.CreateDefault(CreateTempRoot());
        const string readme = """
        Direct run:
            llama-cli -m model.gguf -c 8192 -n 256 --temp=0.4
        """;

        var settings = HuggingFaceLaunchSettingsSuggester.TryCreate(
            defaults,
            readme,
            """{"top_k":"33","max_new_tokens":128}""",
            """{"max_position_embeddings":65536}""");

        Assert.NotNull(settings);
        Assert.Equal(8_192, settings.ContextSize);
        Assert.Equal(256, settings.MaxTokens);
        Assert.Equal(0.4, settings.Temperature);
        Assert.Equal(33, settings.TopK);
    }


    [Fact]
    public void HuggingFaceSuggestedLaunchSettingsIgnoreOutOfRangeContextConfig()
    {
        var defaults = AppSettings.CreateDefault(CreateTempRoot());

        var settings = HuggingFaceLaunchSettingsSuggester.TryCreate(
            defaults,
            "",
            """{"top_k":44}""",
            """{"max_position_embeddings":524288}""");

        Assert.NotNull(settings);
        Assert.Equal(AppSettings.DefaultContextSize, settings.ContextSize);
        Assert.Equal(44, settings.TopK);
    }


    [Fact]
    public void GgufMetadataReaderRejectsHugeMetadataArraysQuickly()
    {
        var root = CreateTempRoot();
        var path = Path.Combine(root, "huge-array.gguf");
        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("GGUF"));
            writer.Write((uint)3);
            writer.Write((ulong)0);
            writer.Write((ulong)1);
            WriteGgufString(writer, "tokenizer.ggml.tokens");
            writer.Write((uint)9);
            writer.Write((uint)0);
            writer.Write(1_000_001UL);
        }

        var metadata = GgufMetadataReader.TryRead(path);

        Assert.Empty(metadata);
    }

}
