using System.Diagnostics;
using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public void RuntimePackageAssetSelectorSelectsOfficialReleaseAssets()
    {
        var release = RuntimePackageReleaseClient.ParseReleaseJson("""
        {
          "tag_name": "b9354",
          "html_url": "https://github.com/ggml-org/llama.cpp/releases/tag/b9354",
          "published_at": "2026-05-27T08:00:00Z",
          "assets": [
            { "name": "llama-b9354-bin-win-cuda-13.1-x64.zip", "browser_download_url": "https://example.com/cuda13.zip", "size": 13 },
            { "name": "cudart-llama-bin-win-cuda-13.1-x64.zip", "browser_download_url": "https://example.com/cudart13.zip", "size": 3 },
            { "name": "llama-b9354-bin-win-cuda-12.4-x64.zip", "browser_download_url": "https://example.com/cuda12.zip", "size": 12 },
            { "name": "cudart-llama-bin-win-cuda-12.4-x64.zip", "browser_download_url": "https://example.com/cudart12.zip", "size": 2 },
            { "name": "llama-b9354-bin-win-vulkan-x64.zip", "browser_download_url": "https://example.com/win-vulkan.zip", "size": 4 },
            { "name": "llama-b9354-bin-win-sycl-x64.zip", "browser_download_url": "https://example.com/win-sycl.zip", "size": 9 },
            { "name": "llama-b9354-bin-win-cpu-x64.zip", "browser_download_url": "https://example.com/win-cpu.zip", "size": 5 },
            { "name": "llama-b9354-bin-ubuntu-cuda-13.1-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-cuda13.tar.gz", "size": 11 },
            { "name": "llama-b9354-bin-ubuntu-cuda-12.4-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-cuda.tar.gz", "size": 8 },
            { "name": "llama-b9354-bin-ubuntu-vulkan-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-vulkan.tar.gz", "size": 6 },
            { "name": "llama-b9354-bin-ubuntu-sycl-f16-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-sycl.tar.gz", "size": 10 },
            { "name": "llama-b9354-bin-ubuntu-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-cpu.tar.gz", "size": 7 }
          ]
        }
        """);

        var presets = RuntimePackageSourceCatalog.PresetRows();
        var cuda = RuntimePackageAssetSelector.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-windows-cuda"), release);
        var cudaCompatibility = RuntimePackageAssetSelector.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-windows-cuda"), release, "compatibility");
        var cudaWsl = RuntimePackageAssetSelector.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-cuda"), release);
        var cudaWslCompatibility = RuntimePackageAssetSelector.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-cuda"), release, "compatibility");
        var vulkanWsl = RuntimePackageAssetSelector.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-vulkan"), release);
        var sycl = RuntimePackageAssetSelector.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-windows-sycl"), release);
        var syclWsl = RuntimePackageAssetSelector.SelectAssets(presets.Single(preset => preset.Id == "official-prebuilt-sycl"), release);
        var atomicWindows = presets.Single(preset => preset.Id == "atomic-prebuilt-windows-cuda");

        Assert.Equal(
            ["official-prebuilt-windows-cuda", "official-prebuilt-cuda", "atomic-prebuilt-windows-cuda", "atomic-prebuilt-cuda", "official-prebuilt-windows-vulkan", "official-prebuilt-vulkan", "official-prebuilt-windows-sycl", "official-prebuilt-sycl", "official-prebuilt-windows-cpu", "official-prebuilt-cpu"],
            presets.Select(preset => preset.Id).ToArray());
        Assert.Equal("b9354", release.TagName);
        Assert.Equal("llama-b9354-bin-win-cuda-13.1-x64.zip", cuda.PrimaryAsset.Name);
        Assert.Equal("cudart-llama-bin-win-cuda-13.1-x64.zip", Assert.Single(cuda.AdditionalAssets).Name);
        Assert.Contains("cudart-llama-bin-win-cuda-13.1-x64.zip", cuda.AssetSummary, StringComparison.Ordinal);
        Assert.True(RuntimePackageAssetSelector.AssetSummariesMatch("cudart-llama-bin-win-cuda-13.1-x64.zip, llama-b9354-bin-win-cuda-13.1-x64.zip", cuda.AssetSummary));
        Assert.Equal("llama-b9354-bin-win-cuda-12.4-x64.zip", cudaCompatibility.PrimaryAsset.Name);
        Assert.Equal("cudart-llama-bin-win-cuda-12.4-x64.zip", Assert.Single(cudaCompatibility.AdditionalAssets).Name);
        Assert.Equal("llama-b9354-bin-ubuntu-cuda-13.1-x64.tar.gz", cudaWsl.PrimaryAsset.Name);
        Assert.Equal("llama-b9354-bin-ubuntu-cuda-12.4-x64.tar.gz", cudaWslCompatibility.PrimaryAsset.Name);
        Assert.Equal("CUDA WSL", RuntimePackageSourceCatalog.BackendLabel(cudaWsl.Preset));
        Assert.Equal("llama-b9354-bin-ubuntu-vulkan-x64.tar.gz", vulkanWsl.PrimaryAsset.Name);
        Assert.Equal("Vulkan WSL", RuntimePackageSourceCatalog.BackendLabel(vulkanWsl.Preset));
        Assert.Equal("llama-b9354-bin-win-sycl-x64.zip", sycl.PrimaryAsset.Name);
        Assert.Equal("SYCL Windows", RuntimePackageSourceCatalog.BackendLabel(sycl.Preset));
        Assert.Equal("llama-b9354-bin-ubuntu-sycl-f16-x64.tar.gz", syclWsl.PrimaryAsset.Name);
        Assert.Equal("SYCL WSL", RuntimePackageSourceCatalog.BackendLabel(syclWsl.Preset));
        Assert.Equal("atomic-windows-turboquant-cuda", atomicWindows.SourcePresetId);
        Assert.Equal(RuntimePackageSourceCatalog.AtomicTurboQuantHuggingFaceApiUrl, RuntimePackageSourceCatalog.ReleaseApiUrlFor(atomicWindows));
        Assert.Equal("Atomic llama.cpp prebuilt", RuntimePackageSourceCatalog.PackageSourceLabel(atomicWindows));
        Assert.EndsWith(Path.Combine("official-prebuilt-windows-cuda-b9354"), RuntimePackageInstallFileService.InstallDir(Path.Combine("D:", "runtimes"), cuda), StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void RuntimePackageAssetSelectorSelectsAtomicHuggingFaceAssets()
    {
        var release = RuntimePackageReleaseClient.ParseHuggingFaceModelJson("""
        {
          "id": "atomicmilkshake/llama-cpp-turboquant-binaries",
          "sha": "402c91005e37c8b42a3159c5b0f5f7d062095ba6",
          "lastModified": "2026-04-08T20:01:19.000Z",
          "siblings": [
            { "rfilename": ".gitattributes" },
            { "rfilename": "README.md" },
            { "rfilename": "llama-turboquant-triattention-win-cu13-x64.zip" }
          ]
        }
        """, RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "atomic-prebuilt-windows-cuda"));
        var windowsPreset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "atomic-prebuilt-windows-cuda");
        var wslPreset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "atomic-prebuilt-cuda");

        var windows = RuntimePackageAssetSelector.SelectAssets(windowsPreset, release);
        var unavailable = Assert.Throws<RuntimePackageAssetUnavailableException>(() => RuntimePackageAssetSelector.SelectAssets(wslPreset, release));

        Assert.Equal("hf-402c91005e37", release.TagName);
        Assert.Equal("402c91005e37c8b42a3159c5b0f5f7d062095ba6", release.TargetCommit);
        Assert.Equal(RuntimePackageSourceCatalog.AtomicTurboQuantHuggingFacePageUrl, release.HtmlUrl);
        Assert.Equal("llama-turboquant-triattention-win-cu13-x64.zip", windows.PrimaryAsset.Name);
        Assert.Contains("/resolve/402c91005e37c8b42a3159c5b0f5f7d062095ba6/llama-turboquant-triattention-win-cu13-x64.zip?download=true", windows.PrimaryAsset.DownloadUrl, StringComparison.Ordinal);
        Assert.Contains("Atomic llama.cpp TurboQuant CUDA WSL", unavailable.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimePackageAssetSelectorReportsCudaWslUnavailableWhenReleaseOmitsAsset()
    {
        var release = RuntimePackageReleaseClient.ParseReleaseJson("""
        {
          "tag_name": "b9357",
          "html_url": "https://github.com/ggml-org/llama.cpp/releases/tag/b9357",
          "target_commitish": "abcdef1234567890",
          "assets": [
            { "name": "llama-b9357-bin-ubuntu-vulkan-x64.tar.gz", "browser_download_url": "https://example.com/ubuntu-vulkan.tar.gz", "size": 6 }
          ]
        }
        """);
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-cuda");
        var unavailable = new RuntimePackageUpdateState(false, "", release.TagName, release.HtmlUrl, "not available", DateTimeOffset.UtcNow, TargetCommit: release.TargetCommit, IsAvailable: false);

        var ex = Assert.Throws<RuntimePackageAssetUnavailableException>(() => RuntimePackageAssetSelector.SelectAssets(preset, release));

        Assert.Contains("CUDA WSL", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Not published", RuntimePackageInventoryPresenter.LocalStatusLabel([], [], unavailable));
        Assert.False(RuntimePackageInventoryPresenter.CanInstallPackage([], [], unavailable));
    }


    [Fact]
    public void RuntimePackageInstallFileServiceExtractsAndFindsRuntimeExecutable()
    {
        var root = CreateTempRoot();
        var source = Path.Combine(root, "source");
        var nested = Path.Combine(source, "llama-b9354-bin-win-cpu-x64", "bin");
        var archive = Path.Combine(root, "runtime.zip");
        var destination = Path.Combine(root, "runtime");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "llama-server.exe"), "fake");
        System.IO.Compression.ZipFile.CreateFromDirectory(source, archive);

        RuntimePackageInstallFileService.ExtractArchive(archive, destination);

        var executable = RuntimePackageInstallFileService.FindRuntimeExecutable(destination, RuntimeMode.Native);
        Assert.Equal(Path.Combine(destination, "bin", "llama-server.exe"), executable);
        Assert.Equal(destination, RuntimePackageInstallFileService.RuntimeFolderFromExecutable(executable));
    }


    [Fact]
    public void RuntimePackageInstallFileServiceExtractsCompanionArchivesBesidePrimaryRuntime()
    {
        var root = CreateTempRoot();
        var primarySource = Path.Combine(root, "primary-source");
        var primaryNested = Path.Combine(primarySource, "llama-b9354-bin-win-cuda-x64", "bin");
        var companionSource = Path.Combine(root, "companion-source");
        var companionNested = Path.Combine(companionSource, "cudart-llama-bin-win-cuda-12.4-x64", "bin");
        var primaryArchive = Path.Combine(root, "primary.zip");
        var companionArchive = Path.Combine(root, "companion.zip");
        var destination = Path.Combine(root, "runtime");
        Directory.CreateDirectory(primaryNested);
        Directory.CreateDirectory(companionNested);
        File.WriteAllText(Path.Combine(primaryNested, "llama-server.exe"), "fake server");
        File.WriteAllText(Path.Combine(companionNested, "cudart64_12.dll"), "fake cudart");
        System.IO.Compression.ZipFile.CreateFromDirectory(primarySource, primaryArchive);
        System.IO.Compression.ZipFile.CreateFromDirectory(companionSource, companionArchive);

        RuntimePackageInstallFileService.ExtractArchive(primaryArchive, destination);
        RuntimePackageInstallFileService.ExtractArchive(companionArchive, destination);

        Assert.True(File.Exists(Path.Combine(destination, "bin", "llama-server.exe")));
        Assert.True(File.Exists(Path.Combine(destination, "bin", "cudart64_12.dll")));
        Assert.False(Directory.Exists(Path.Combine(destination, "cudart-llama-bin-win-cuda-12.4-x64")));
    }


    [Fact]
    public async Task RuntimePackageInstallServiceDownloadsExtractsStampsAndRegistersRuntime()
    {
        var root = CreateTempRoot();
        var source = Path.Combine(root, "package-source", "llama-b9354-bin-win-cpu-x64", "bin");
        var archive = Path.Combine(root, "runtime.zip");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "llama-server.exe"), "fake server", TestContext.Current.CancellationToken);
        System.IO.Compression.ZipFile.CreateFromDirectory(Path.Combine(root, "package-source"), archive);
        var archiveBytes = await File.ReadAllBytesAsync(archive, TestContext.Current.CancellationToken);
        var releaseJson = $$"""
        {
          "tag_name": "b9354",
          "target_commitish": "9777256c3130",
          "html_url": "https://example.com/release",
          "published_at": "2026-05-28T10:00:00Z",
          "assets": [
            {
              "name": "llama-b9354-bin-win-cpu-x64.zip",
              "browser_download_url": "https://example.com/win-cpu.zip",
              "size": {{archiveBytes.Length}}
            }
          ]
        }
        """;
        using var handler = new CapturingHttpHandler(request =>
        {
            if (request.RequestUri?.ToString() == RuntimePackageSourceCatalog.LatestReleaseApiUrl)
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(releaseJson) };
            if (request.RequestUri?.ToString() == "https://example.com/win-cpu.zip")
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(archiveBytes) };
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });
        using var http = new HttpClient(handler);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var runtimes = new RuntimeRegistryService(store);
        var installer = new RuntimePackageInstallService(http, runtimes);
        var settings = AppSettings.CreateDefault(root);
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cpu");
        var progress = new List<RuntimePackageInstallProgress>();
        var logPath = Path.Combine(root, "logs", "runtime-package.log");

        var result = await installer.InstallAsync(new RuntimePackageInstallRequest(
            preset,
            settings,
            logPath,
            BoundedLogFile.MegabytesToBytes(1),
            progressItem =>
            {
                progress.Add(progressItem);
                return Task.CompletedTask;
            },
            CancellationToken: TestContext.Current.CancellationToken));
        var registered = Assert.Single(await store.ListRuntimesAsync());
        var log = await File.ReadAllTextAsync(logPath, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(settings.RuntimeRoot, "official-prebuilt-windows-cpu-b9354"), result.InstallDir);
        Assert.Equal(result.InstallDir, result.RuntimeFolder);
        Assert.Equal(result.RuntimeFolder, RuntimeMetadataService.Folder(registered));
        Assert.Equal("b9354", result.UpdateState.LocalTag);
        Assert.Equal("package:b9354", result.UpdateState.LocalIdentity);
        Assert.Contains("installed from b9354", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(result.RuntimeFolder, "local-llm-runtime.json")));
        Assert.True(File.Exists(Path.Combine(result.RuntimeFolder, "bin", "llama-server.exe")));
        Assert.Contains(progress, item => item.Message.Contains("Resolving latest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(progress, item => item.Message.Contains("Downloading llama-b9354", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Extracting llama-b9354-bin-win-cpu-x64.zip", log, StringComparison.Ordinal);
    }


    [Fact]
    public async Task RuntimePackageInstallServiceCleansIncompleteInstallOnFailure()
    {
        var root = CreateTempRoot();
        var badArchiveBytes = System.Text.Encoding.UTF8.GetBytes("not a zip");
        var releaseJson = $$"""
        {
          "tag_name": "b9354",
          "target_commitish": "9777256c3130",
          "html_url": "https://example.com/release",
          "published_at": "2026-05-28T10:00:00Z",
          "assets": [
            {
              "name": "llama-b9354-bin-win-cpu-x64.zip",
              "browser_download_url": "https://example.com/win-cpu.zip",
              "size": {{badArchiveBytes.Length}}
            }
          ]
        }
        """;
        using var handler = new CapturingHttpHandler(request =>
        {
            if (request.RequestUri?.ToString() == RuntimePackageSourceCatalog.LatestReleaseApiUrl)
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(releaseJson) };
            if (request.RequestUri?.ToString() == "https://example.com/win-cpu.zip")
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(badArchiveBytes) };
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });
        using var http = new HttpClient(handler);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var installer = new RuntimePackageInstallService(http, new RuntimeRegistryService(store));
        var settings = AppSettings.CreateDefault(root);
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cpu");
        var installDir = Path.Combine(settings.RuntimeRoot, "official-prebuilt-windows-cpu-b9354");

        await Assert.ThrowsAnyAsync<Exception>(() => installer.InstallAsync(new RuntimePackageInstallRequest(
            preset,
            settings,
            Path.Combine(root, "logs", "runtime-package.log"),
            BoundedLogFile.MegabytesToBytes(1),
            _ => Task.CompletedTask,
            CancellationToken: TestContext.Current.CancellationToken)));

        Assert.False(Directory.Exists(installDir));
        Assert.Empty(await store.ListRuntimesAsync());
    }


    [Fact]
    public async Task RuntimePackageJobServiceCreatesUpdatesAndParsesPackageJobs()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var jobs = new JobEngine(store, Path.Combine(root, "logs"));
        var service = new RuntimePackageJobService(jobs);
        var preset = new RuntimePackagePreset("official-prebuilt-windows-cuda", "CUDA Windows", RuntimeBackend.Cuda, RuntimeMode.Native, "official-cuda-source");

        var install = await service.CreateInstallJobAsync(preset, TestContext.Current.CancellationToken);
        var check = await service.CreateCheckJobAsync(preset, TestContext.Current.CancellationToken);
        await service.UpdateAsync(
            install,
            JobStatus.Running,
            preset,
            "install",
            Path.Combine(root, "runtime"),
            "Installing package",
            BoundedLogFile.MegabytesToBytes(1),
            TestContext.Current.CancellationToken);

        var stored = await store.ListJobsAsync();
        var updated = stored.Single(job => job.Id == install.Id);
        var payload = RuntimePackageJobService.ParsePayload(updated.PayloadJson);
        var log = await File.ReadAllTextAsync(install.LogPath, TestContext.Current.CancellationToken);

        Assert.Equal("runtime-package-download", install.Kind);
        Assert.Equal("runtime-package-update-check", check.Kind);
        Assert.NotNull(payload);
        Assert.Equal(preset.Id, payload.Preset.Id);
        Assert.Equal(RuntimeBackend.Cuda, payload.Backend);
        Assert.Equal(RuntimeMode.Native, payload.Mode);
        Assert.Equal("official-cuda-source", payload.SourcePresetId);
        Assert.Equal(RuntimePackageSourceCatalog.LatestReleaseApiUrl, payload.ReleaseApiUrl);
        Assert.Equal(RuntimePackageSourceCatalog.ReleasesUrl, payload.ReleasePageUrl);
        Assert.Equal("official llama.cpp", payload.PackageSourceLabel);
        Assert.Equal("official-prebuilt", payload.PackageSourceKey);
        Assert.Equal(RuntimePackageSourceCatalog.OfficialRepositoryUrl, payload.RepositoryUrl);
        Assert.Equal("install", payload.Action);
        Assert.Contains("Installing package", log, StringComparison.Ordinal);
    }


    [Fact]
    public async Task RuntimePackageInstallWorkflowServiceOwnsInstallJobLifecycle()
    {
        var root = CreateTempRoot();
        var source = Path.Combine(root, "package-source", "llama-b9354-bin-win-cpu-x64", "bin");
        var archive = Path.Combine(root, "runtime.zip");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "llama-server.exe"), "fake server", TestContext.Current.CancellationToken);
        System.IO.Compression.ZipFile.CreateFromDirectory(Path.Combine(root, "package-source"), archive);
        var archiveBytes = await File.ReadAllBytesAsync(archive, TestContext.Current.CancellationToken);
        var releaseJson = $$"""
        {
          "tag_name": "b9354",
          "target_commitish": "9777256c3130",
          "html_url": "https://example.com/release",
          "published_at": "2026-05-28T10:00:00Z",
          "assets": [
            {
              "name": "llama-b9354-bin-win-cpu-x64.zip",
              "browser_download_url": "https://example.com/win-cpu.zip",
              "size": {{archiveBytes.Length}}
            }
          ]
        }
        """;
        using var handler = new CapturingHttpHandler(request =>
        {
            if (request.RequestUri?.ToString() == RuntimePackageSourceCatalog.LatestReleaseApiUrl)
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(releaseJson) };
            if (request.RequestUri?.ToString() == "https://example.com/win-cpu.zip")
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(archiveBytes) };
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });
        using var http = new HttpClient(handler);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var settings = AppSettings.CreateDefault(root);
        var runtimes = new RuntimeRegistryService(store);
        var workflow = new RuntimePackageInstallWorkflowService(
            new RuntimePackageInstallService(http, runtimes),
            new RuntimePackageJobService(new JobEngine(store, Path.Combine(root, "logs"))),
            new RuntimePackageWslFileService(new ScriptedProcessRunner(_ => new ProcessRunResult(0, "", "")), () => "wsl.exe"));
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cpu");
        var notifications = 0;

        var result = await workflow.InstallAsync(new RuntimePackageInstallWorkflowRequest(
            preset,
            settings,
            BoundedLogFile.MegabytesToBytes(1),
            () =>
            {
                notifications++;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken));
        var job = Assert.Single(await store.ListJobsAsync());
        var payload = RuntimePackageJobService.ParsePayload(job.PayloadJson);
        var registered = Assert.Single(await store.ListRuntimesAsync());
        var log = await File.ReadAllTextAsync(job.LogPath, TestContext.Current.CancellationToken);

        Assert.Equal(result.Job.Id, job.Id);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(payload);
        Assert.Equal("install", payload.Action);
        Assert.Equal(result.RuntimeFolder, payload.InstallDir);
        Assert.Equal(result.RuntimeFolder, RuntimeMetadataService.Folder(registered));
        Assert.Equal("b9354", result.UpdateState.LocalTag);
        Assert.Contains("Resolving latest official llama.cpp release", log, StringComparison.Ordinal);
        Assert.Contains("Downloading llama-b9354-bin-win-cpu-x64.zip", log, StringComparison.Ordinal);
        Assert.Contains("installed from b9354", log, StringComparison.OrdinalIgnoreCase);
        Assert.True(notifications >= 4);
    }


    [Fact]
    public async Task RuntimePackageWslFileServiceBuildsArchiveAndChmodCommands()
    {
        var root = CreateTempRoot();
        var logPath = Path.Combine(root, "logs", "package-wsl.log");
        var archivePath = Path.Combine(root, "cache", "llama's.tar.gz");
        var installDir = Path.Combine(root, "runtimes", "llama install");
        var executable = Path.Combine(installDir, "bin", "llama-server");
        var runner = new ScriptedProcessRunner(_ => new ProcessRunResult(0, "ok", ""));
        var service = new RuntimePackageWslFileService(runner, () => "wsl.exe");

        await service.ExtractArchiveAsync(new RuntimePackageWslArchiveRequest(
            "Ubuntu-24.04",
            archivePath,
            installDir,
            logPath,
            BoundedLogFile.MegabytesToBytes(1),
            TestContext.Current.CancellationToken));
        await service.TryPrepareExecutableAsync(new RuntimePackageWslExecutableRequest(
            new RuntimePackagePreset("pkg", "Package", RuntimeBackend.Cpu, RuntimeMode.Wsl, "source"),
            "Ubuntu-24.04",
            executable,
            logPath,
            BoundedLogFile.MegabytesToBytes(1),
            TestContext.Current.CancellationToken));
        await service.TryPrepareExecutableAsync(new RuntimePackageWslExecutableRequest(
            new RuntimePackagePreset("native", "Native", RuntimeBackend.Cpu, RuntimeMode.Native, "source"),
            "Ubuntu-24.04",
            executable,
            logPath,
            BoundedLogFile.MegabytesToBytes(1),
            TestContext.Current.CancellationToken));

        var extractCommand = runner.Commands[0].Last();
        var chmodCommand = runner.Commands[1].Last();
        var log = await File.ReadAllTextAsync(logPath, TestContext.Current.CancellationToken);

        Assert.Equal(2, runner.Commands.Count);
        Assert.Contains("Ubuntu-24.04", runner.Commands[0]);
        Assert.Contains("tar --overwrite -xzf", extractCommand, StringComparison.Ordinal);
        Assert.Contains(CommandLineService.BashQuote(RuntimePackageWslFileService.WindowsPathToWslPath(archivePath)), extractCommand, StringComparison.Ordinal);
        Assert.Contains(CommandLineService.BashQuote(RuntimePackageWslFileService.WindowsPathToWslPath(installDir)), extractCommand, StringComparison.Ordinal);
        Assert.Contains("chmod +x", chmodCommand, StringComparison.Ordinal);
        Assert.Contains(CommandLineService.BashQuote(RuntimePackageWslFileService.WindowsPathToWslPath(executable)), chmodCommand, StringComparison.Ordinal);
        Assert.Contains("ok", log, StringComparison.Ordinal);
    }


    [Fact]
    public async Task RuntimePackageWslFileServiceReportsArchiveFailuresAndChmodWarnings()
    {
        var root = CreateTempRoot();
        var archiveFailure = new RuntimePackageWslFileService(
            new ScriptedProcessRunner(_ => new ProcessRunResult(2, "", "tar failed")),
            () => "wsl.exe");

        var archive = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            archiveFailure.ExtractArchiveAsync(new RuntimePackageWslArchiveRequest(
                "Ubuntu-24.04",
                Path.Combine(root, "cache", "runtime.tar.gz"),
                Path.Combine(root, "runtime"),
                Path.Combine(root, "logs", "archive.log"),
                BoundedLogFile.MegabytesToBytes(1),
                TestContext.Current.CancellationToken)));
        Assert.Contains("exit code 2", archive.Message, StringComparison.Ordinal);
        Assert.Contains("tar failed", archive.Message, StringComparison.Ordinal);

        var logPath = Path.Combine(root, "logs", "chmod.log");
        var chmodFailure = new RuntimePackageWslFileService(
            new ScriptedProcessRunner(_ => throw new InvalidOperationException("chmod failed")),
            () => "wsl.exe");
        await chmodFailure.TryPrepareExecutableAsync(new RuntimePackageWslExecutableRequest(
            new RuntimePackagePreset("pkg", "Package", RuntimeBackend.Cpu, RuntimeMode.Wsl, "source"),
            "Ubuntu-24.04",
            Path.Combine(root, "runtime", "bin", "llama-server"),
            logPath,
            BoundedLogFile.MegabytesToBytes(1),
            TestContext.Current.CancellationToken));

        var chmodLog = await File.ReadAllTextAsync(logPath, TestContext.Current.CancellationToken);
        Assert.Contains("Warning: could not chmod WSL runtime executable", chmodLog, StringComparison.Ordinal);
        Assert.Contains("chmod failed", chmodLog, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimePackageMetadataIdentifiesInstalledPrebuiltRuntime()
    {
        var root = CreateTempRoot();
        var runtimeFolder = Path.Combine(root, "official-prebuilt-windows-cuda-b9354");
        Directory.CreateDirectory(runtimeFolder);
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cuda");
        var runtime = new RuntimeRecord(
            "runtime-1",
            "Official llama.cpp CUDA Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(runtimeFolder, "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = runtimeFolder,
                runtimeMetadata = new
                {
                    managedPackageId = preset.Id,
                    managedPresetId = preset.Id,
                    releaseTag = "b9354"
                }
            }),
            DateTimeOffset.UtcNow);

        var installed = RuntimePackageInventoryPresenter.InstalledPackages([runtime], preset);

        Assert.Equal(preset.Id, RuntimeMetadataService.ManagedPackageId(runtime));
        Assert.Equal(preset.Id, RuntimeMetadataService.ManagedPresetId(runtime));
        Assert.Equal("b9354", RuntimeMetadataService.PackageTag(runtime));
        Assert.Single(installed);
        Assert.Equal("b9354", RuntimePackageInventoryPresenter.LatestInstalledTag(installed));
        Assert.False(RuntimePackageInventoryPresenter.CanInstallPackage(installed, null));
        Assert.True(RuntimePackageInventoryPresenter.CanInstallPackage(installed, new RuntimePackageUpdateState(true, "b9354", "b9355", "", "", DateTimeOffset.UtcNow)));
    }


    [Fact]
    public void RuntimePackageStatusServiceBuildsInventoryRowsAndHonorsStateIdentity()
    {
        var root = CreateTempRoot();
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cuda");
        var runtimeFolder = Path.Combine(root, "official-prebuilt-windows-cuda-b9354");
        var runtime = new RuntimeRecord(
            "runtime-1",
            "Official llama.cpp CUDA Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(runtimeFolder, "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = runtimeFolder,
                runtimeMetadata = new
                {
                    managedPackageId = preset.Id,
                    managedPresetId = preset.Id,
                    releaseTag = "b9354"
                }
            }),
            DateTimeOffset.UtcNow);
        var stale = new RuntimePackageUpdateState(true, "old", "b9355", "", "stale", DateTimeOffset.UtcNow, "package:old");
        var current = new RuntimePackageUpdateState(true, "b9354", "b9355", "", "current", DateTimeOffset.UtcNow, "package:b9354");
        var service = new RuntimePackageStatusService();

        var staleInventory = service.BuildInventory(preset, [runtime], new Dictionary<string, RuntimePackageUpdateState> { [preset.Id] = stale });
        var currentInventory = service.BuildInventory(preset, [runtime], new Dictionary<string, RuntimePackageUpdateState> { [preset.Id] = current });
        var row = service.CreateRow(preset, currentInventory);

        Assert.Null(staleInventory.CheckedState);
        Assert.Same(current, currentInventory.CheckedState);
        Assert.Equal("package:b9354", currentInventory.LocalIdentity);
        Assert.Equal("Update", row.InstallAction);
        Assert.True(row.CanInstall);
        Assert.Equal("current", row.Assets);
    }


    [Fact]
    public void RuntimePackageStatusServiceEvaluatesAvailableAndUnavailableChecks()
    {
        var root = CreateTempRoot();
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cuda");
        var runtimeFolder = Path.Combine(root, "official-prebuilt-windows-cuda-b9354");
        var runtime = new RuntimeRecord(
            "runtime-1",
            "Official llama.cpp CUDA Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(runtimeFolder, "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = runtimeFolder,
                runtimeMetadata = new
                {
                    managedPackageId = preset.Id,
                    managedPresetId = preset.Id,
                    releaseTag = "b9354",
                    assets = new[] { new { name = "llama-b9354-bin-win-cuda-12.4-x64.zip" } }
                }
            }),
            DateTimeOffset.UtcNow);
        var service = new RuntimePackageStatusService();
        var inventory = service.BuildInventory(preset, [runtime], new Dictionary<string, RuntimePackageUpdateState>());
        var release = new RuntimePackageRelease(
            "b9354",
            "9777256c3130",
            "https://example.com/release",
            DateTimeOffset.UtcNow,
            [new RuntimePackageAsset("llama-b9354-bin-win-cuda-13.1-x64.zip", "https://example.com/asset.zip", 1024)]);
        var selection = new RuntimePackageSelection(preset, release.TagName, release.HtmlUrl, release.PublishedAt, release.Assets[0], []);

        var available = service.EvaluateAvailableRelease(inventory, release, selection, DateTimeOffset.UtcNow);
        var unavailableInventory = service.BuildInventory(preset, [], new Dictionary<string, RuntimePackageUpdateState>());
        var unavailable = service.EvaluateUnavailableRelease(unavailableInventory, release, "not published", DateTimeOffset.UtcNow);

        Assert.True(available.State.HasUpdate);
        Assert.Equal("Update available", available.LocalStatus);
        Assert.Contains("Package variant available", available.Message, StringComparison.Ordinal);
        Assert.Equal("Update", available.InstallAction);
        Assert.False(unavailable.State.IsAvailable);
        Assert.Equal("Not published", unavailable.LocalStatus);
        Assert.False(unavailable.CanInstall);
    }


    [Fact]
    public async Task RuntimePackageUpdateCheckServiceMapsAvailableAndUnavailableReleaseChecks()
    {
        var checkedAt = new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero);
        const string releaseJson = """
        {
          "tag_name": "b9354",
          "target_commitish": "9777256c3130",
          "html_url": "https://example.com/release",
          "published_at": "2026-05-28T10:00:00Z",
          "assets": [
            {
              "name": "llama-b9354-bin-win-cpu-x64.zip",
              "browser_download_url": "https://example.com/win-cpu.zip",
              "size": 1024
            }
          ]
        }
        """;
        using var handler = new CapturingHttpHandler(request =>
            request.RequestUri?.ToString() == RuntimePackageSourceCatalog.LatestReleaseApiUrl
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(releaseJson) }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        var status = new RuntimePackageStatusService();
        var service = new RuntimePackageUpdateCheckService(http, status);
        var availablePreset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cpu");
        var unavailablePreset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-cuda");

        var available = await service.CheckAsync(new RuntimePackageUpdateCheckRequest(
            availablePreset,
            status.BuildInventory(availablePreset, [], new Dictionary<string, RuntimePackageUpdateState>()),
            "latest",
            checkedAt,
            TestContext.Current.CancellationToken));
        var unavailable = await service.CheckAsync(new RuntimePackageUpdateCheckRequest(
            unavailablePreset,
            status.BuildInventory(unavailablePreset, [], new Dictionary<string, RuntimePackageUpdateState>()),
            "latest",
            checkedAt,
            TestContext.Current.CancellationToken));

        Assert.False(available.AssetUnavailable);
        Assert.Equal("b9354", available.Result.State.LatestTag);
        Assert.True(available.Result.State.IsAvailable);
        Assert.Contains("Latest available release is b9354", available.Result.Message, StringComparison.Ordinal);
        Assert.True(unavailable.AssetUnavailable);
        Assert.False(unavailable.Result.State.IsAvailable);
        Assert.Contains("CUDA WSL/Linux", unavailable.Result.Message, StringComparison.Ordinal);
    }


    [Fact]
    public async Task RuntimePackageCheckWorkflowServiceOwnsCheckJobLifecycle()
    {
        var root = CreateTempRoot();
        const string releaseJson = """
        {
          "tag_name": "b9354",
          "target_commitish": "9777256c3130",
          "html_url": "https://example.com/release",
          "published_at": "2026-05-28T10:00:00Z",
          "assets": [
            {
              "name": "llama-b9354-bin-win-cpu-x64.zip",
              "browser_download_url": "https://example.com/win-cpu.zip",
              "size": 1024
            }
          ]
        }
        """;
        using var handler = new CapturingHttpHandler(request =>
            request.RequestUri?.ToString() == RuntimePackageSourceCatalog.LatestReleaseApiUrl
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(releaseJson) }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var status = new RuntimePackageStatusService();
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cpu");
        var jobs = new RuntimePackageJobService(new JobEngine(store, Path.Combine(root, "logs")));
        var checks = new RuntimePackageUpdateCheckService(http, status);
        var workflow = new RuntimePackageCheckWorkflowService(jobs, checks);
        var notifications = 0;

        var outcome = await workflow.CheckAsync(new RuntimePackageCheckWorkflowRequest(
            preset,
            status.BuildInventory(preset, [], new Dictionary<string, RuntimePackageUpdateState>()),
            "latest",
            BoundedLogFile.MegabytesToBytes(1),
            () =>
            {
                notifications++;
                return Task.CompletedTask;
            },
            new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken));
        var job = Assert.Single(await store.ListJobsAsync());
        var payload = RuntimePackageJobService.ParsePayload(job.PayloadJson);
        var log = await File.ReadAllTextAsync(job.LogPath, TestContext.Current.CancellationToken);

        Assert.Equal(outcome.Job.Id, job.Id);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(payload);
        Assert.Equal("check", payload.Action);
        Assert.Equal(outcome.CheckResult.Message, payload.Message);
        Assert.Contains("Checking official llama.cpp release assets", log, StringComparison.Ordinal);
        Assert.Contains("Latest available release is b9354", log, StringComparison.Ordinal);
        Assert.True(notifications >= 3);
    }

    [Fact]
    public async Task RuntimePackageApplicationServiceCoordinatesInstallCheckAndDelete()
    {
        var root = CreateTempRoot();
        const string releaseJson = """
        {
          "tag_name": "b9355",
          "target_commitish": "abcdef999999",
          "html_url": "https://example.com/release",
          "published_at": "2026-05-28T10:00:00Z",
          "assets": [
            {
              "name": "llama-b9355-bin-win-cpu-x64.zip",
              "browser_download_url": "https://example.com/win-cpu.zip",
              "size": 1024
            }
          ]
        }
        """;
        using var handler = new CapturingHttpHandler(request =>
            request.RequestUri?.ToString() == RuntimePackageSourceCatalog.LatestReleaseApiUrl
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(releaseJson) }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var settings = AppSettings.CreateDefault(root);
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cpu");
        var runtimeFolder = Path.Combine(settings.RuntimeRoot, "official-prebuilt-windows-cpu-b9354");
        Directory.CreateDirectory(runtimeFolder);
        await File.WriteAllTextAsync(Path.Combine(runtimeFolder, "llama-server.exe"), "fake server", TestContext.Current.CancellationToken);
        var runtime = new RuntimeRecord(
            "runtime-package",
            "Official llama.cpp CPU Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cpu,
            Path.Combine(runtimeFolder, "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = runtimeFolder,
                runtimeMetadata = new
                {
                    managedPackageId = preset.Id,
                    managedPresetId = preset.Id,
                    releaseTag = "b9354"
                }
            }),
            DateTimeOffset.UtcNow);
        await store.UpsertRuntimeAsync(runtime);

        var sessions = CreateLoadedModelSessionManager();
        var launchProfiles = new ModelLaunchProfileService(store, sessions);
        var status = new RuntimePackageStatusService();
        var jobs = new RuntimePackageJobService(new JobEngine(store, Path.Combine(root, "logs")));
        var service = new RuntimePackageApplicationService(
            store,
            status,
            new RuntimePackageCheckWorkflowService(jobs, new RuntimePackageUpdateCheckService(http, status)),
            new RuntimePackageInstallWorkflowService(
                new RuntimePackageInstallService(http, new RuntimeRegistryService(store)),
                jobs,
                new RuntimePackageWslFileService(new ScriptedProcessRunner(_ => new ProcessRunResult(0, "", "")), () => "wsl.exe")),
            new RuntimeDeletionPlanner(store, launchProfiles, sessions),
            new RuntimeDeletionExecutorService(store),
            new RuntimeBuildPrerequisiteService(new RuntimeToolPrerequisiteService(
                _ => throw new InvalidOperationException("WSL readiness is not expected for native packages."),
                () => throw new InvalidOperationException("Windows build tools are not expected for package installs."),
                new ScriptedProcessRunner(_ => new ProcessRunResult(0, "", "")))));
        var sessionState = new RuntimeCatalogSessionState();
        var row = status.CreateRow(
            preset,
            status.BuildInventory(preset, await store.ListRuntimesAsync(), sessionState.RuntimePackageUpdateStates));
        var statuses = new List<string>();
        var busyMessages = new List<string>();
        var infoMessages = new List<string>();
        var confirmations = new List<RuntimePackageDeleteConfirmation>();
        var packageGridRefreshes = 0;
        var runtimeRefreshes = 0;
        var overviewRefreshes = 0;
        var jobRefreshes = 0;
        var yields = 0;
        var confirmDelete = false;
        RuntimePackageApplicationActions Actions() => new(
            async (message, action) =>
            {
                busyMessages.Add(message);
                await action();
            },
            () =>
            {
                runtimeRefreshes++;
                return Task.CompletedTask;
            },
            () =>
            {
                overviewRefreshes++;
                return Task.CompletedTask;
            },
            () =>
            {
                jobRefreshes++;
                return Task.CompletedTask;
            },
            () =>
            {
                yields++;
                return Task.CompletedTask;
            },
            () => packageGridRefreshes++,
            statuses.Add,
            (title, message) => infoMessages.Add($"{title}: {message}"),
            confirmation =>
            {
                confirmations.Add(confirmation);
                return confirmDelete;
            });

        var blockedInstall = await service.InstallAsync(preset, settings, sessionState, BoundedLogFile.MegabytesToBytes(1), Actions());
        var check = await service.CheckUpdateAsync(preset, row, settings, sessionState, BoundedLogFile.MegabytesToBytes(1), Actions());
        Assert.True(sessionState.RuntimePackageUpdateStates.TryGetValue(preset.Id, out var packageUpdate));
        var cancelledDelete = await service.DeleteBuildsAsync(preset, settings, sessionState, Actions());
        var afterCancelledDelete = await store.ListRuntimesAsync();
        confirmDelete = true;
        var appliedDelete = await service.DeleteBuildsAsync(preset, settings, sessionState, Actions());
        var afterAppliedDelete = await store.ListRuntimesAsync();

        Assert.Equal(RuntimePackageApplicationOutcome.Blocked, blockedInstall);
        Assert.Contains("already installed", statuses[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RuntimePackageApplicationOutcome.Applied, check);
        Assert.Equal("b9355", packageUpdate.LatestTag);
        Assert.Equal("Update", row.InstallAction);
        Assert.True(row.CanInstall);
        Assert.Contains(infoMessages, message => message.Contains("Runtime download check", StringComparison.Ordinal));
        Assert.Equal(RuntimePackageApplicationOutcome.Cancelled, cancelledDelete);
        Assert.Contains(afterCancelledDelete, candidate => candidate.Id == runtime.Id);
        Assert.Equal(RuntimePackageApplicationOutcome.Applied, appliedDelete);
        Assert.DoesNotContain(afterAppliedDelete, candidate => candidate.Id == runtime.Id);
        Assert.Empty(sessionState.RuntimePackageUpdateStates);
        Assert.Equal(["Checking Official llama.cpp CPU Windows release...", "Deleting runtime downloads..."], busyMessages);
        Assert.Equal(2, confirmations.Count);
        Assert.Contains("Installed runtimes: 1", confirmations[0].Message, StringComparison.Ordinal);
        Assert.Equal(2, packageGridRefreshes);
        Assert.Equal(3, runtimeRefreshes);
        Assert.Equal(1, overviewRefreshes);
        Assert.True(jobRefreshes >= 2);
        Assert.Equal(1, yields);
    }


    [Fact]
    public async Task RuntimeEquivalenceServiceLinksSourceBuildAndPrebuiltByFingerprint()
    {
        var root = CreateTempRoot();
        var packageFolder = Path.Combine(root, "runtimes", "official-prebuilt-windows-cuda-b9354");
        var sourceFolder = Path.Combine(root, "runtimes", "official-windows-cuda-20260527");
        Directory.CreateDirectory(Path.Combine(packageFolder, "bin"));
        Directory.CreateDirectory(Path.Combine(sourceFolder, "bin"));
        await File.WriteAllTextAsync(Path.Combine(packageFolder, "bin", "llama-server.exe"), "same binary", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sourceFolder, "bin", "llama-server.exe"), "same binary", TestContext.Current.CancellationToken);
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var now = DateTimeOffset.UtcNow;
        var packageRuntime = new RuntimeRecord(
            "package-runtime",
            "Official llama.cpp CUDA Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(packageFolder, "bin", "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = packageFolder,
                runtimeMetadata = new
                {
                    managedPackageId = "official-prebuilt-windows-cuda",
                    managedPresetId = "official-prebuilt-windows-cuda",
                    releaseTag = "b9354"
                }
            }),
            now);
        var sourceRuntime = new RuntimeRecord(
            "source-runtime",
            "Official llama.cpp CUDA Windows Source",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(sourceFolder, "bin", "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = sourceFolder,
                runtimeMetadata = new
                {
                    managedPresetId = "official-windows-cuda",
                    commit = "9777256c3130"
                }
            }),
            now);
        await store.UpsertRuntimeAsync(packageRuntime);
        await store.UpsertRuntimeAsync(sourceRuntime);

        Assert.True(await RuntimeEquivalenceService.ReconcileOfficialRuntimeEquivalenceAsync(store, await store.ListRuntimesAsync(), TestContext.Current.CancellationToken));
        var runtimes = await store.ListRuntimesAsync();
        var reconciledSource = runtimes.Single(runtime => runtime.Id == sourceRuntime.Id);
        var reconciledPackage = runtimes.Single(runtime => runtime.Id == packageRuntime.Id);
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cuda");

        Assert.Equal(RuntimeMetadataService.RuntimeFingerprint(reconciledPackage), RuntimeMetadataService.RuntimeFingerprint(reconciledSource));
        Assert.Contains(preset.Id, RuntimeMetadataService.EquivalentPackageIds(reconciledSource));
        Assert.Contains(preset.SourcePresetId, RuntimeMetadataService.EquivalentSourcePresetIds(reconciledPackage));
        Assert.Contains(reconciledSource, RuntimePackageInventoryPresenter.InstalledPackages(runtimes, preset));
    }


    [Fact]
    public void RuntimePackageInventoryPresenterReportsSourceBuildCandidates()
    {
        var root = CreateTempRoot();
        var runtime = new RuntimeRecord(
            "source-runtime",
            "Official llama.cpp CUDA Windows",
            RuntimeMode.Native,
            RuntimeBackend.Cuda,
            Path.Combine(root, "runtime", "bin", "llama-server.exe"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = Path.Combine(root, "runtime"),
                runtimeMetadata = new
                {
                    managedPresetId = "official-windows-cuda",
                    commit = "9777256c3130"
                }
            }),
            DateTimeOffset.UtcNow);
        var preset = RuntimePackageSourceCatalog.PresetRows().Single(candidate => candidate.Id == "official-prebuilt-windows-cuda");

        var sourceBuilds = RuntimePackageInventoryPresenter.MatchingSourceBuilds([runtime], preset);

        Assert.Single(sourceBuilds);
        Assert.Equal("Built from source", RuntimePackageInventoryPresenter.LocalStatusLabel([], sourceBuilds));
        Assert.Equal("source:9777256c3130", RuntimePackageInventoryPresenter.LocalIdentity([], sourceBuilds));
        Assert.Contains("source built", RuntimePackageInventoryPresenter.LatestLocalLabel([], sourceBuilds, null), StringComparison.OrdinalIgnoreCase);
    }
}
