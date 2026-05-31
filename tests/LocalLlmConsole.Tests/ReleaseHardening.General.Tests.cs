using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public async Task CorruptSettingsAreBackedUpAndDefaulted()
    {
        var root = CreateTempRoot();
        var databasePath = Path.Combine(root, "state", "local-llm-console.db");
        await using var store = new StateStore(databasePath);
        await store.InitializeAsync();

        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
INSERT INTO settings (key, value_json, updated_at)
VALUES ('port', '"not-a-port"', $updated_at)
ON CONFLICT(key) DO UPDATE SET value_json = excluded.value_json, updated_at = excluded.updated_at;
""";
            command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var settings = await store.GetAppSettingsAsync(root);

        Assert.Equal(AppSettings.CreateDefault(root).Port, settings.Port);
        Assert.True(Directory.EnumerateFiles(Path.Combine(root, "state", "corrupt-settings"), "*.json").Any());
    }


    [Fact]
    public void LargeServicesStaySplitByResponsibility()
    {
        var appRoot = Path.GetDirectoryName(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.xaml.cs"))!;

        AssertServicePartials(appRoot, "Services", "AppServiceFactory", 300,
            "AppServiceFactory.Catalog.cs",
            "AppServiceFactory.Core.cs",
            "AppServiceFactory.Foundation.cs",
            "AppServiceFactory.Loaded.cs",
            "AppServiceFactory.Runtime.cs",
            "AppServiceFactory.RuntimeModel.cs");
        AssertServicePartials(appRoot, "Services", "HuggingFaceService", 350,
            "HuggingFaceService.Downloads.cs",
            "HuggingFaceService.Search.cs",
            "HuggingFaceService.Safety.cs",
            "HuggingFaceService.LaunchProfiles.cs",
            "HuggingFaceService.Projectors.cs");
        AssertServicePartials(appRoot, "Services", "StateStore", 380,
            "StateStore.Catalog.cs",
            "StateStore.Settings.cs",
            "StateStore.Jobs.cs",
            "StateStore.LegacyLaunchDefaults.cs");
        AssertServicePartials(appRoot, "Services", "OpenCodeConfigService", 380,
            "OpenCodeConfigService.Models.cs",
            "OpenCodeConfigService.Agents.cs",
            "OpenCodeConfigService.Json.cs",
            "OpenCodeConfigService.ModelEnvelopes.cs",
            "OpenCodeConfigService.Providers.cs",
            "OpenCodeConfigService.Discovery.cs");
        AssertServicePartials(appRoot, "Services", "ModelCatalogService", 380,
            "ModelCatalogService.Legacy.cs");
        AssertServicePartials(appRoot, "Services", "LlamaProcessSupervisor", 260,
            "LlamaProcessSupervisor.Launch.cs",
            "LlamaProcessSupervisor.Wsl.cs");
        AssertServicePartials(appRoot, "Services", "HuggingFaceLaunchSettingsSuggester", 260,
            "HuggingFaceLaunchSettingsSuggester.Config.cs",
            "HuggingFaceLaunchSettingsSuggester.CommandExtraction.cs",
            "HuggingFaceLaunchSettingsSuggester.ShellParsing.cs");
        AssertServicePartials(appRoot, "Services", "RuntimeBuildCatalogService", 180,
            "RuntimeBuildCatalogService.BuildShape.cs",
            "RuntimeBuildCatalogService.CustomPresets.cs",
            "RuntimeBuildCatalogService.Presentation.cs",
            "RuntimeBuildCatalogService.Sources.cs");
        AssertServicePartials(appRoot, "Services", "RuntimeMetadataService", 180,
            "RuntimeMetadataService.Commits.cs",
            "RuntimeMetadataService.Paths.cs",
            "RuntimeMetadataService.PresetInference.cs");
        AssertServicePartials(appRoot, "Services", "RuntimeDeletionPlanner", 220,
            "RuntimeDeletionPlanner.BuildPresets.cs",
            "RuntimeDeletionPlanner.Packages.cs",
            "RuntimeDeletionPlanner.Sources.cs");

        var modelsRoot = Path.Combine(appRoot, "Models");
        var modelFiles = Directory.EnumerateFiles(modelsRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Name = Path.GetFileName(path), Lines = File.ReadAllLines(path).Length })
            .ToArray();
        var oversizedModels = modelFiles
            .Where(file => file.Lines > 250)
            .Select(file => $"{file.Name}:{file.Lines}")
            .ToArray();
        var modelFileNames = modelFiles.Select(file => file.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Empty(oversizedModels);
        Assert.Contains("AppSettings.cs", modelFileNames);
        Assert.Contains("ModelLaunchSettings.cs", modelFileNames);
        Assert.Contains("RuntimeModels.cs", modelFileNames);
        Assert.Contains("CoreModels.cs", modelFileNames);
    }

    [Fact]
    public void GlobalUsingsDoNotLeakWpfIntoServices()
    {
        var globalUsings = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "GlobalUsings.cs"));

        Assert.DoesNotContain("global using System.Windows;", globalUsings, StringComparison.Ordinal);
        Assert.DoesNotContain("global using System.Windows.Controls;", globalUsings, StringComparison.Ordinal);
        Assert.DoesNotContain("global using Forms =", globalUsings, StringComparison.Ordinal);
        Assert.DoesNotContain("global using Wpf", globalUsings, StringComparison.Ordinal);
    }


    [Fact]
    public void LocalAppServiceObservesRequestHandlerTasks()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LocalAppService.cs"));

        Assert.Contains("QueueRequest(context, cancellationToken)", source, StringComparison.Ordinal);
        Assert.Contains("_requestHandlers", source, StringComparison.Ordinal);
        Assert.Contains("ObserveCompletionAsync", source, StringComparison.Ordinal);
        Assert.Contains("LastListenerError", source, StringComparison.Ordinal);
        Assert.Contains("_listenerErrorCount", source, StringComparison.Ordinal);
        Assert.Contains("await Task.Delay(250, cancellationToken)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = Task.Run(() => HandleAsync", source, StringComparison.Ordinal);
    }


    [Fact]
    public async Task StateStoreInitializationServiceRetriesAfterQuarantiningCorruptDatabase()
    {
        var root = CreateTempRoot();
        var databasePath = Path.Combine(root, "state", "local-llm-console.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await File.WriteAllTextAsync(databasePath, "not a sqlite database", TestContext.Current.CancellationToken);
        var quarantineCalls = 0;
        var service = new StateStoreInitializationService();

        var result = await service.InitializeAsync(new StateStoreInitializationRequest(
            root,
            databasePath,
            () => new StateStore(databasePath),
            path =>
            {
                quarantineCalls++;
                return StateStore.QuarantineDatabaseFiles(path);
            }));

        await using var store = result.StateStore;
        var reloaded = await store.GetAppSettingsAsync(root);

        Assert.Equal(root, result.Settings.WorkspaceRoot);
        Assert.Equal(root, reloaded.WorkspaceRoot);
        Assert.Equal(1, quarantineCalls);
        Assert.True(File.Exists(databasePath));
        Assert.True(Directory.EnumerateDirectories(Path.Combine(root, "state"), "corrupt-database-*").Any());
    }


    [Fact]
    public async Task LocalAppServiceStartupServiceFallsBackAndDisposesFailedPort()
    {
        var created = new List<FakeLocalAppServiceHost>();
        var service = new LocalAppServiceStartupService();

        var result = await service.StartAsync(new LocalAppServiceStartupRequest(
            PreferredPort: 8090,
            MaxFallbackPort: 8092,
            CreateService: port =>
            {
                var host = new FakeLocalAppServiceHost(port, port == 8090 ? new System.Net.Sockets.SocketException() : null);
                created.Add(host);
                return host;
            }));

        Assert.Equal(2, created.Count);
        Assert.Equal(8091, result.Port);
        Assert.Same(created[1], result.Service);
        Assert.True(created[0].Disposed);
        Assert.False(created[1].Disposed);
        Assert.True(created[1].Started);
        Assert.Contains("moved to 127.0.0.1:8091", result.StatusMessage, StringComparison.Ordinal);
    }


    [Fact]
    public async Task BackgroundTaskApplicationServiceReportsFailuresAndIgnoresCancellation()
    {
        var service = new BackgroundTaskApplicationService();
        var statuses = new List<string>();
        var errors = new List<Exception>();
        var actions = new BackgroundTaskApplicationActions(
            statuses.Add,
            error =>
            {
                errors.Add(error);
                return Task.CompletedTask;
            });

        await service.RunAsync(
            () => throw new OperationCanceledException(),
            "Cancelled task failed",
            actions);
        await service.RunAsync(
            () => throw new InvalidOperationException("offline"),
            "Background refresh failed",
            actions);

        Assert.Equal(["Background refresh failed: offline"], statuses);
        var error = Assert.Single(errors);
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal("offline", error.Message);
    }


    [Fact]
    public async Task ForegroundTaskApplicationServiceOwnsBusyAndEventErrorBoundaries()
    {
        var service = new ForegroundTaskApplicationService();
        var calls = new List<string>();
        var errors = new List<Exception>();
        var dialogs = new List<string>();
        var currentStatus = "";

        ForegroundTaskApplicationActions Actions(bool canBegin = true)
            => new(
                message =>
                {
                    calls.Add($"begin:{message}");
                    return canBegin;
                },
                () => calls.Add("end"),
                status =>
                {
                    currentStatus = status;
                    calls.Add($"status:{status}");
                },
                () => currentStatus,
                () =>
                {
                    calls.Add("yield");
                    return Task.CompletedTask;
                },
                error =>
                {
                    errors.Add(error);
                    calls.Add($"log:{error.Message}");
                    return Task.CompletedTask;
                },
                error =>
                {
                    dialogs.Add(error.Message);
                    calls.Add($"dialog:{error.Message}");
                });

        await service.RunBusyAsync(
            "Loading...",
            () =>
            {
                calls.Add($"action:{currentStatus}");
                return Task.CompletedTask;
            },
            Actions());
        await service.RunBusyAsync(
            "Skipped",
            () => throw new InvalidOperationException("Should not run."),
            Actions(canBegin: false));
        await service.RunBusyAsync(
            "Saving...",
            () => throw new InvalidOperationException("save failed"),
            Actions());
        await service.RunEventAsync(
            () => throw new InvalidOperationException("event failed"),
            Actions());

        Assert.Equal([
            "begin:Loading...",
            "status:Loading...",
            "yield",
            "action:Loading...",
            "status:",
            "end",
            "begin:Skipped",
            "begin:Saving...",
            "status:Saving...",
            "yield",
            "status:save failed",
            "log:save failed",
            "dialog:save failed",
            "end",
            "status:event failed",
            "log:event failed",
            "dialog:event failed"
        ], calls);
        Assert.Equal(["save failed", "event failed"], errors.Select(error => error.Message).ToArray());
        Assert.Equal(["save failed", "event failed"], dialogs);
    }


    [Fact]
    public void ShellIntegrationServiceOwnsProcessLaunchAndFolderCreation()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ShellIntegrationService.cs"));
        var started = new List<ProcessStartInfo>();
        var service = new ShellIntegrationService(started.Add);
        var root = CreateTempRoot();
        var folder = Path.Combine(root, "logs");
        var logPath = Path.Combine(root, "logs", "runtime.log");
        var url = "https://github.com/example/repo";

        service.OpenFolder(folder);
        service.OpenPath(logPath);
        service.OpenUrl(url);

        Assert.True(Directory.Exists(folder));
        Assert.Equal([folder, logPath, url], started.Select(process => process.FileName).ToArray());
        Assert.All(started, process => Assert.True(process.UseShellExecute));
        Assert.Throws<ArgumentException>(() => service.OpenUrl("relative/path"));
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
    }


    [Fact]
    public void FileSystemDialogServiceOwnsPickerDialogRequests()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "FileSystemDialogService.cs"));
        var factory = ReadAppServiceFactorySources();
        var root = CreateTempRoot();
        var existingFolder = Path.Combine(root, "existing");
        var chosenFolder = Path.Combine(root, "chosen");
        var chosenFile = Path.Combine(root, "opencode.jsonc");
        Directory.CreateDirectory(existingFolder);
        var folderRequests = new List<FolderPickerRequest>();
        var fileRequests = new List<OpenFilePickerRequest>();
        var service = new FileSystemDialogService(
            request =>
            {
                folderRequests.Add(request);
                return chosenFolder;
            },
            (request, owner) =>
            {
                Assert.Null(owner);
                fileRequests.Add(request);
                return chosenFile;
            });
        var pickerPlan = new OpenCodeConfigFilePickerPlan(
            "Choose OpenCode config",
            "OpenCode config|opencode.json;opencode.jsonc|JSON files|*.json;*.jsonc|All files|*.*",
            CheckFileExists: false,
            AddExtension: true,
            ".jsonc",
            "opencode.jsonc",
            existingFolder);

        var folder = service.PickFolder(existingFolder);
        var file = service.PickOpenCodeConfigFile(pickerPlan);

        Assert.Equal(chosenFolder, folder);
        Assert.Equal(chosenFile, file);
        Assert.Equal(existingFolder, folderRequests.Single().InitialPath);
        var fileRequest = fileRequests.Single();
        Assert.Equal(pickerPlan.Title, fileRequest.Title);
        Assert.Equal(pickerPlan.Filter, fileRequest.Filter);
        Assert.Equal(pickerPlan.CheckFileExists, fileRequest.CheckFileExists);
        Assert.Equal(pickerPlan.AddExtension, fileRequest.AddExtension);
        Assert.Equal(pickerPlan.DefaultExt, fileRequest.DefaultExt);
        Assert.Equal(pickerPlan.FileName, fileRequest.FileName);
        Assert.Equal(pickerPlan.InitialDirectory, fileRequest.InitialDirectory);
        Assert.Equal(existingFolder, FileSystemDialogService.ExistingDirectoryOrEmpty(existingFolder));
        Assert.Equal("", FileSystemDialogService.ExistingDirectoryOrEmpty(Path.Combine(root, "missing")));
        Assert.Contains("FileSystemDialogService.ShowFolderDialog", factory, StringComparison.Ordinal);
        Assert.Contains("FileSystemDialogService.ShowOpenFileDialog", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("?? ShowFolderDialog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("?? ShowOpenFileDialog", source, StringComparison.Ordinal);
    }


    [Fact]
    public void ClipboardServiceOwnsClipboardSetTextAction()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ClipboardService.cs"));
        var copied = new List<string>();
        var service = new ClipboardService(copied.Add);

        service.SetText("secret-key");

        Assert.Equal(["secret-key"], copied);
        Assert.Throws<ArgumentNullException>(() => service.SetText(null!));
        Assert.DoesNotContain("System.Windows.Clipboard.SetText", source, StringComparison.Ordinal);
    }


    [Fact]
    public void DialogServiceOwnsThemedMessageBoxBridge()
    {
        var calls = new List<string>();
        var service = new DialogService((owner, message, title, buttons, image) =>
        {
            Assert.Null(owner);
            calls.Add($"{title}:{message}:{buttons}:{image}");
            return buttons == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK;
        });

        var confirmed = service.Confirm(null, "Proceed?", "Confirm", MessageBoxImage.Warning);
        service.Notify(null, "Done", "Info", MessageBoxImage.Information);
        var result = service.Show(null, "Plain", "Show", MessageBoxButton.OKCancel, MessageBoxImage.Error);

        Assert.True(confirmed);
        Assert.Equal(MessageBoxResult.OK, result);
        Assert.Equal(
            [
                "Confirm:Proceed?:YesNo:Exclamation",
                "Info:Done:OK:Asterisk",
                "Show:Plain:OKCancel:Hand"
            ],
            calls);
    }


    [Fact]
    public void SingleInstanceApplicationServiceOwnsLeaseLifecycle()
    {
        var nonOwnerLease = new FakeSingleInstanceLease(ownsInstance: false);
        var ownerLease = new FakeSingleInstanceLease(ownsInstance: true);
        var leases = new Queue<FakeSingleInstanceLease>([nonOwnerLease, ownerLease]);
        var acquiredNames = new List<string>();
        var service = new SingleInstanceApplicationService(name =>
        {
            acquiredNames.Add(name);
            return leases.Dequeue();
        });

        Assert.False(service.TryAcquire("Local\\app"));

        Assert.True(nonOwnerLease.Disposed);
        Assert.False(nonOwnerLease.Released);

        Assert.True(service.TryAcquire("Local\\app"));
        Assert.True(service.TryAcquire("Local\\other"));

        service.Dispose();
        service.Dispose();

        Assert.Equal(["Local\\app", "Local\\app"], acquiredNames);
        Assert.True(ownerLease.Released);
        Assert.True(ownerLease.Disposed);
        Assert.Throws<ArgumentException>(() => new SingleInstanceApplicationService(_ => throw new InvalidOperationException()).TryAcquire(""));
    }


    [Fact]
    public async Task DownloadCompletionApplicationServiceWaitsThenRefreshesOnUiThread()
    {
        var service = new DownloadCompletionApplicationService();
        var calls = new List<string>();

        Task AddAsync(string call)
        {
            calls.Add(call);
            return Task.CompletedTask;
        }

        await service.MonitorAsync(
            "job-1",
            new DownloadCompletionApplicationActions(
                (jobId, interval) => AddAsync($"wait:{jobId}:{interval.TotalMilliseconds:0}"),
                async action =>
                {
                    calls.Add("ui:begin");
                    await action();
                    calls.Add("ui:end");
                },
                () => AddAsync("scan-models"),
                () => AddAsync("refresh-models"),
                () => AddAsync("refresh-jobs"),
                () => AddAsync("refresh-overview"),
                () => AddAsync("refresh-download-history"),
                () => AddAsync("refresh-install-state")));

        Assert.Equal([
            "wait:job-1:1500",
            "ui:begin",
            "scan-models",
            "refresh-models",
            "refresh-jobs",
            "refresh-overview",
            "refresh-download-history",
            "refresh-install-state",
            "ui:end"
        ], calls);
    }


    [Fact]
    public async Task AppStartupApplicationServiceOwnsStateLoadedServicesAndLocalServiceStartup()
    {
        var root = CreateTempRoot();
        var factory = new AppServiceFactory(root);
        var infrastructure = factory.CreateMainWindowInfrastructureServices();
        using var sessions = infrastructure.Sessions;
        var processRunner = infrastructure.ProcessRunner;
        using var runtimePackageClient = infrastructure.RuntimePackageClient;
        using var runtimeProbeClient = infrastructure.RuntimeProbeClient;
        using var metricsClient = infrastructure.MetricsClient;
        var core = factory.CreateMainWindowCoreServices(infrastructure.CoreServiceRequest());
        var createdHosts = new List<FakeLocalAppServiceHost>();
        var calls = new List<string>();
        StateStore? appliedStore = null;
        AppSettings? appliedSettings = null;
        MainWindowLoadedServices? appliedLoaded = null;
        ILocalAppServiceHost? appliedLocal = null;
        AppStartupApplicationResult? result = null;

        try
        {
            result = await core.App.StartupApplication.StartAsync(
                new AppStartupApplicationRequest(
                    root,
                    factory.DatabasePath,
                    factory.CreateStateStore,
                    stateStore => factory.CreateMainWindowLoadedServices(infrastructure.LoadedServiceRequest(stateStore, core)),
                    (_, _, port) =>
                    {
                        var host = new FakeLocalAppServiceHost(port);
                        createdHosts.Add(host);
                        return host;
                    },
                    PreferredLocalServicePort: 8095,
                    MaxLocalServiceFallbackPort: 8095),
                new AppStartupApplicationActions(
                    stateStore =>
                    {
                        appliedStore = stateStore;
                        calls.Add("state");
                    },
                    settings =>
                    {
                        appliedSettings = settings;
                        calls.Add("settings");
                    },
                    loadedServices =>
                    {
                        appliedLoaded = loadedServices;
                        calls.Add("loaded");
                    },
                    localService =>
                    {
                        appliedLocal = localService;
                        calls.Add("local");
                    },
                    status => calls.Add($"status:{status}")),
                TestContext.Current.CancellationToken);

            Assert.Same(result.StateStore, appliedStore);
            Assert.Same(result.Settings, appliedSettings);
            Assert.Same(result.LoadedServices, appliedLoaded);
            Assert.Same(result.LocalService, appliedLocal);
            Assert.Equal(8095, result.LocalServicePort);
            Assert.Equal("", result.LocalServiceStatusMessage);
            Assert.Equal(["state", "settings", "loaded", "local"], calls);
            Assert.True(Directory.Exists(root));
            Assert.True(Directory.Exists(result.Settings.ModelsRoot));
            Assert.True(Directory.Exists(result.Settings.RuntimeRoot));
            Assert.True(Directory.Exists(result.Settings.CacheRoot));
            Assert.Single(createdHosts);
            Assert.True(createdHosts[0].Started);
            Assert.False(createdHosts[0].Disposed);
        }
        finally
        {
            if (result?.LocalService is not null)
                await result.LocalService.DisposeAsync();
            if (result?.StateStore is not null)
                await result.StateStore.DisposeAsync();
        }
    }


    [Fact]
    public async Task AppStartupBackgroundApplicationServiceSeedsSuggestedLaunchProfilesQuietly()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var service = new AppStartupBackgroundApplicationService();
        var observedTimeout = false;

        var seeded = await service.SeedSuggestedLaunchProfilesAsync(
            new AppStartupSuggestedLaunchProfileSeedRequest(
                settings,
                (receivedSettings, cancellationToken) =>
                {
                    observedTimeout = cancellationToken.CanBeCanceled;
                    Assert.Same(settings, receivedSettings);
                    return Task.FromResult(2);
                }),
            TestContext.Current.CancellationToken);
        var skipped = await service.SeedSuggestedLaunchProfilesAsync(
            new AppStartupSuggestedLaunchProfileSeedRequest(settings, null),
            TestContext.Current.CancellationToken);
        var failed = await service.SeedSuggestedLaunchProfilesAsync(
            new AppStartupSuggestedLaunchProfileSeedRequest(
                settings,
                (_, _) => throw new InvalidOperationException("Offline")),
            TestContext.Current.CancellationToken);

        Assert.True(observedTimeout);
        Assert.True(seeded.ShouldRefreshLaunchSettings);
        Assert.Equal(2, seeded.SeededCount);
        Assert.Equal("Applied Hugging Face suggested launch defaults for 2 models.", seeded.StatusMessage);
        Assert.False(skipped.ShouldRefreshLaunchSettings);
        Assert.Equal(0, skipped.SeededCount);
        Assert.False(failed.ShouldRefreshLaunchSettings);
        Assert.Equal(0, failed.SeededCount);
    }


    [Fact]
    public void AppShutdownDecisionServiceBuildsPromptsAndClosingStatus()
    {
        var service = new AppShutdownDecisionService();
        var shutdownState = new AppShutdownStateController();

        var idle = service.Build(runningModelSessions: 0, activeDownloads: 0);
        var downloadsOnly = service.Build(runningModelSessions: 0, activeDownloads: 1);
        var modelsAndDownloads = service.Build(runningModelSessions: 2, activeDownloads: 3);

        Assert.Empty(idle.Confirmations);
        Assert.Equal("Closing...", idle.ClosingStatus);
        var downloadPrompt = Assert.Single(downloadsOnly.Confirmations);
        Assert.Equal(AppShutdownConfirmationKind.ActiveDownloads, downloadPrompt.Kind);
        Assert.Equal("Downloads in progress", downloadPrompt.Title);
        Assert.Contains("1 model download is still running.", downloadPrompt.Message, StringComparison.Ordinal);
        Assert.Equal("Pausing active downloads and closing...", downloadsOnly.ClosingStatus);
        Assert.Equal(2, modelsAndDownloads.Confirmations.Count);
        Assert.Equal(AppShutdownConfirmationKind.RunningModels, modelsAndDownloads.Confirmations[0].Kind);
        Assert.Contains("2 model sessions are running.", modelsAndDownloads.Confirmations[0].Message, StringComparison.Ordinal);
        Assert.Equal(AppShutdownConfirmationKind.ActiveDownloads, modelsAndDownloads.Confirmations[1].Kind);
        Assert.Contains("3 model downloads are still running.", modelsAndDownloads.Confirmations[1].Message, StringComparison.Ordinal);
        Assert.Equal("Stopping runtimes and closing...", modelsAndDownloads.ClosingStatus);

        Assert.Equal(AppShutdownCloseAdmission.CancelAndStartCleanup, shutdownState.BeginClosing());
        Assert.True(shutdownState.ShutdownRequested);
        Assert.Equal(AppShutdownCloseAdmission.CancelAlreadyInProgress, shutdownState.BeginClosing());
        shutdownState.ResetRequest();
        Assert.False(shutdownState.ShutdownRequested);
        Assert.Equal(AppShutdownCloseAdmission.CancelAndStartCleanup, shutdownState.BeginClosing());
        shutdownState.MarkCleanupComplete();
        Assert.True(shutdownState.CleanupComplete);
        Assert.False(shutdownState.ShutdownRequested);
        Assert.Equal(AppShutdownCloseAdmission.AllowClose, shutdownState.BeginClosing());
    }


    [Fact]
    public async Task AppShutdownApplicationServiceOwnsAdmissionConfirmationsAndCleanupState()
    {
        var decisions = new AppShutdownDecisionService();
        var state = new AppShutdownStateController();
        var application = new AppShutdownApplicationService(decisions, state);
        var calls = new List<string>();

        var completed = await application.BeginShutdownAsync(
            new AppShutdownApplicationRequest(RunningModelSessions: 1, ActiveDownloads: 1),
            new AppShutdownApplicationActions(
                prompt =>
                {
                    calls.Add($"confirm:{prompt.Kind}");
                    return Task.FromResult(true);
                },
                () => calls.Add("disable"),
                status => calls.Add($"status:{status}"),
                () =>
                {
                    calls.Add("cleanup");
                    return Task.CompletedTask;
                }));

        Assert.Equal(AppShutdownApplicationOutcomeKind.CleanupCompleted, completed.Kind);
        Assert.True(completed.CancelClosingEvent);
        Assert.True(completed.RequestClose);
        Assert.True(state.CleanupComplete);
        Assert.Equal([
            "confirm:RunningModels",
            "confirm:ActiveDownloads",
            "disable",
            "status:Stopping runtimes and closing...",
            "cleanup"
        ], calls);

        var allowed = await application.BeginShutdownAsync(
            new AppShutdownApplicationRequest(RunningModelSessions: 0, ActiveDownloads: 0),
            new AppShutdownApplicationActions(
                _ => throw new InvalidOperationException("Already cleaned up."),
                () => throw new InvalidOperationException("Already cleaned up."),
                _ => throw new InvalidOperationException("Already cleaned up."),
                () => throw new InvalidOperationException("Already cleaned up.")));
        Assert.Equal(AppShutdownApplicationOutcomeKind.AllowClose, allowed.Kind);
        Assert.False(allowed.CancelClosingEvent);
        Assert.False(allowed.RequestClose);
    }


    [Fact]
    public async Task AppShutdownApplicationServiceResetsRequestAfterCancelledPromptOrFailure()
    {
        var cancelledState = new AppShutdownStateController();
        var cancelled = new AppShutdownApplicationService(new AppShutdownDecisionService(), cancelledState);
        var cancelledOutcome = await cancelled.BeginShutdownAsync(
            new AppShutdownApplicationRequest(RunningModelSessions: 1, ActiveDownloads: 0),
            new AppShutdownApplicationActions(
                _ => Task.FromResult(false),
                () => throw new InvalidOperationException("Should not disable UI."),
                _ => throw new InvalidOperationException("Should not set status."),
                () => throw new InvalidOperationException("Should not clean up.")));

        Assert.Equal(AppShutdownApplicationOutcomeKind.CancelledByUser, cancelledOutcome.Kind);
        Assert.True(cancelledOutcome.CancelClosingEvent);
        Assert.False(cancelledOutcome.RequestClose);
        Assert.False(cancelledState.ShutdownRequested);

        var failingState = new AppShutdownStateController();
        var failing = new AppShutdownApplicationService(new AppShutdownDecisionService(), failingState);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.BeginShutdownAsync(
            new AppShutdownApplicationRequest(RunningModelSessions: 0, ActiveDownloads: 0),
            new AppShutdownApplicationActions(
                _ => Task.FromResult(true),
                () => { },
                _ => { },
                () => throw new InvalidOperationException("cleanup failed"))));
        Assert.False(failingState.ShutdownRequested);
    }


    [Fact]
    public async Task AppShutdownCleanupApplicationServiceRunsCleanupInShutdownOrder()
    {
        var service = new AppShutdownCleanupApplicationService();
        var calls = new List<string>();

        Task AddAsync(string call)
        {
            calls.Add(call);
            return Task.CompletedTask;
        }

        await service.CleanupAsync(new AppShutdownCleanupActions(
            () => calls.Add("stop-download-history"),
            () => calls.Add("stop-runtime-dashboard"),
            () => calls.Add("cancel-launch-settings"),
            () => calls.Add("stop-readiness"),
            () => calls.Add("dispose-tray"),
            () => AddAsync("pause-downloads"),
            () => calls.Add("kill-processes"),
            () => AddAsync("cleanup-wsl-builds"),
            () => AddAsync("dispose-gateway"),
            () => AddAsync("stop-runtime-sessions"),
            () => calls.Add("dispose-sessions"),
            () => calls.Add("dispose-runtime-package-client"),
            () => calls.Add("dispose-metrics-client"),
            () => calls.Add("dispose-runtime-probe-client"),
            () => calls.Add("clear-active-settings"),
            () => calls.Add("clear-active-session"),
            () => AddAsync("dispose-local-service"),
            () => AddAsync("dispose-state-store")));

        Assert.Equal([
            "stop-download-history",
            "stop-runtime-dashboard",
            "cancel-launch-settings",
            "stop-readiness",
            "dispose-tray",
            "pause-downloads",
            "kill-processes",
            "cleanup-wsl-builds",
            "dispose-gateway",
            "stop-runtime-sessions",
            "dispose-sessions",
            "dispose-runtime-package-client",
            "dispose-metrics-client",
            "dispose-runtime-probe-client",
            "clear-active-settings",
            "clear-active-session",
            "dispose-local-service",
            "dispose-state-store"
        ], calls);
    }


    [Fact]
    public async Task DebouncedAsyncActionRunsOnlyLatestScheduledAction()
    {
        using var debounce = new DebouncedAsyncAction(TimeSpan.FromMilliseconds(40));
        var observed = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var background = new List<Task>();
        void RunObserved(Func<Task> action)
            => background.Add(Task.Run(async () =>
            {
                try { await action(); }
                catch (OperationCanceledException) { }
            }));

        debounce.Schedule(
            _ =>
            {
                observed.Enqueue("first");
                return Task.CompletedTask;
            },
            RunObserved);
        debounce.Schedule(
            _ =>
            {
                observed.Enqueue("second");
                return Task.CompletedTask;
            },
            RunObserved);

        await Task.Delay(120, TestContext.Current.CancellationToken);
        await Task.WhenAll(background);

        debounce.Schedule(
            _ =>
            {
                observed.Enqueue("cancelled");
                return Task.CompletedTask;
            },
            RunObserved);
        debounce.Cancel();
        await Task.Delay(80, TestContext.Current.CancellationToken);
        await Task.WhenAll(background);

        Assert.Equal(["second"], observed.ToArray());
    }


    [Fact]
    public void DownloadHistoryPageStateOwnsModeAndTimerRefreshGate()
    {
        var state = new DownloadHistoryPageState();

        Assert.False(state.IsShowingHistory);
        Assert.False(state.TryBeginTimerRefresh());

        state.ShowHistory();

        Assert.True(state.IsShowingHistory);
        Assert.True(state.TryBeginTimerRefresh());
        Assert.False(state.TryBeginTimerRefresh());

        state.CompleteTimerRefresh();

        Assert.True(state.TryBeginTimerRefresh());

        state.ShowSearch();

        Assert.False(state.IsShowingHistory);
        Assert.False(state.TryBeginTimerRefresh());
    }


    [Fact]
    public void RefreshGatePreventsOverlappingRefreshes()
    {
        var gate = new RefreshGate();

        Assert.True(gate.TryBegin());
        Assert.False(gate.TryBegin());

        gate.Complete();

        Assert.True(gate.TryBegin());
    }


    [Fact]
    public async Task UiAsyncRefreshTimerControllerOwnsAsyncTickErrorHandling()
    {
        var timerFactory = new ManualUiTimerFactory();
        var controller = new UiAsyncRefreshTimerController(timerFactory);
        var observed = new List<string>();
        var errors = new List<string>();

        controller.Start(
            TimeSpan.FromSeconds(1.5),
            () =>
            {
                observed.Add("tick");
                return Task.CompletedTask;
            },
            ex => errors.Add(ex.Message));

        Assert.True(controller.IsRunning);
        Assert.Single(timerFactory.Timers);
        Assert.Equal(TimeSpan.FromSeconds(1.5), timerFactory.Timers[0].Interval);
        Assert.True(timerFactory.Timers[0].Started);

        await timerFactory.Timers[0].FireAsync();
        Assert.Equal(["tick"], observed);
        Assert.Empty(errors);

        controller.Start(
            TimeSpan.FromSeconds(1),
            () => throw new InvalidOperationException("refresh failed"),
            ex => errors.Add(ex.Message));

        Assert.False(timerFactory.Timers[0].Started);
        Assert.Equal(2, timerFactory.Timers.Count);
        await timerFactory.Timers[1].FireAsync();
        Assert.Equal(["refresh failed"], errors);

        controller.Stop();
        Assert.False(controller.IsRunning);
        Assert.False(timerFactory.Timers[1].Started);
    }


    [Fact]
    public async Task AppServiceFactoryCentralizesMainWindowServiceWiring()
    {
        var root = CreateTempRoot();
        var factory = new AppServiceFactory(root);
        await using var store = factory.CreateStateStore();
        var infrastructure = factory.CreateMainWindowInfrastructureServices();
        using var sessions = infrastructure.Sessions;
        using var runtimeProbeClient = infrastructure.RuntimeProbeClient;
        using var metricsClient = infrastructure.MetricsClient;
        using var runtimePackageClient = infrastructure.RuntimePackageClient;

        var core = factory.CreateMainWindowCoreServices(infrastructure.CoreServiceRequest());
        var loaded = factory.CreateMainWindowLoadedServices(infrastructure.LoadedServiceRequest(store, core));
        await using var localService = factory.CreateLocalAppService(store, loaded.App.Jobs, 8090);

        Assert.EndsWith(Path.Combine("state", "local-llm-console.db"), factory.DatabasePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("logs", factory.LogRoot, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<MainWindowInfrastructureServices>(infrastructure);
        Assert.IsType<AppUpdateService>(infrastructure.AppUpdates);
        Assert.Same(sessions, infrastructure.Sessions);
        Assert.IsType<TrackedProcessRunner>(infrastructure.ProcessRunner);
        Assert.IsType<WindowsEnvironmentService>(infrastructure.WindowsEnvironment);
        Assert.IsType<WslEnvironmentService>(infrastructure.WslEnvironment);
        Assert.Equal(TimeSpan.FromSeconds(1.5), infrastructure.RuntimeProbeClient.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(2), infrastructure.MetricsClient.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(60), infrastructure.RuntimePackageClient.Timeout);
        Assert.IsType<MainWindowUiState>(core.Ui.UiState);
        Assert.IsType<MainWindowViewModel>(core.Ui.UiState.ViewModel);
        Assert.IsType<OpenCodeFileSetState>(core.Ui.UiState.OpenCodeFileSet);
        Assert.IsType<RuntimeCatalogSessionState>(core.Ui.UiState.RuntimeCatalogState);
        Assert.IsType<DownloadHistoryPageState>(core.Ui.UiState.DownloadHistoryPageState);
        Assert.IsType<EnvironmentPageSnapshotCache>(core.Ui.UiState.EnvironmentPageSnapshots);
        Assert.IsType<StateStoreInitializationService>(core.App.StateStoreInitialization);
        Assert.IsType<BackgroundTaskApplicationService>(core.App.BackgroundTasks);
        Assert.IsType<ForegroundTaskApplicationService>(core.App.ForegroundTasks);
        Assert.IsType<ShellIntegrationService>(core.App.ShellIntegration);
        Assert.IsType<GpuStatusProbeService>(core.App.GpuStatus);
        Assert.IsType<FileSystemDialogService>(core.App.FileSystemDialogs);
        Assert.IsType<ClipboardService>(core.App.Clipboard);
        Assert.IsType<DialogService>(core.App.Dialogs);
        Assert.IsType<DownloadCompletionApplicationService>(core.App.DownloadCompletionApplication);
        Assert.IsType<UiAsyncRefreshTimerController>(core.Ui.DownloadHistoryRefreshTimer);
        Assert.IsType<UiAsyncRefreshTimerController>(core.Ui.RuntimeDashboardRefreshTimer);
        Assert.IsType<GatewayActivityStatusController>(core.Ui.GatewayActivity);
        Assert.IsType<SelectedModelCapabilityController>(core.Ui.SelectedCapabilities);
        Assert.IsType<AdvancedSectionStateController>(core.Ui.AdvancedSections);
        Assert.IsType<LaunchSettingsEditorSession>(core.Ui.LaunchSettingsEditor);
        Assert.IsType<SelectionReentrancyCoordinator>(core.Ui.SelectionReentrancy);
        Assert.IsType<GpuSummaryCache>(core.Ui.GpuSummaryCache);
        Assert.IsType<RuntimeGpuSummaryApplicationService>(core.Ui.RuntimeGpuSummaryApplication);
        Assert.IsType<UiBusyStateController>(core.Ui.UiBusyState);
        Assert.IsType<TrayWindowStateController>(core.Ui.TrayWindowState);
        Assert.IsType<RuntimeReadinessMonitorRegistry>(core.Ui.RuntimeReadinessMonitors);
        Assert.IsType<DebouncedAsyncAction>(core.Ui.LaunchSettingsRefresh);
        Assert.IsType<AppStartupApplicationService>(core.App.StartupApplication);
        Assert.IsType<AppStartupBackgroundApplicationService>(core.App.StartupBackgroundApplication);
        Assert.IsType<AppSettingsUpdateService>(core.App.SettingsUpdates);
        Assert.IsType<AppUpdateWorkflowService>(core.App.AppUpdateWorkflow);
        Assert.IsType<AppUpdateApplicationService>(core.App.AppUpdateApplication);
        Assert.IsType<CacheClearApplicationService>(core.App.CacheClearApplication);
        Assert.IsType<HuggingFaceModelCardApplicationService>(core.HuggingFaceServices.HuggingFaceModelCards);
        Assert.IsType<HuggingFaceSearchApplicationService>(core.HuggingFaceServices.HuggingFaceSearchApplication);
        Assert.IsType<HuggingFaceDownloadApplicationService>(core.HuggingFaceServices.HuggingFaceDownloadApplication);
        Assert.IsType<SettingsRowActionApplicationService>(core.App.SettingsRowActions);
        Assert.IsType<FolderSettingsApplicationService>(core.App.FolderSettingsApplication);
        Assert.IsType<AppLogApplicationService>(core.App.AppLogApplication);
        Assert.IsType<LifetimeMetricResetApplicationService>(core.App.LifetimeMetricResetApplication);
        Assert.IsType<AppShutdownDecisionService>(core.App.ShutdownDecisions);
        Assert.IsType<AppShutdownStateController>(core.App.ShutdownState);
        Assert.IsType<AppShutdownApplicationService>(core.App.ShutdownApplication);
        Assert.IsType<AppShutdownCleanupApplicationService>(core.App.ShutdownCleanupApplication);
        Assert.IsType<SettingsPageDefinitionService>(core.App.SettingsPageDefinitions);
        Assert.IsType<HelpSectionService>(core.App.HelpSections);
        Assert.IsType<HelpNavigationApplicationService>(core.App.HelpNavigation);
        Assert.IsType<RuntimeCatalogDataService>(core.Runtime.RuntimeCatalogData);
        Assert.IsType<ActiveRuntimeSessionStore>(core.Runtime.ActiveSessions);
        Assert.IsType<RuntimeBuildMarkerService>(core.Runtime.RuntimeBuildMarkers);
        Assert.IsType<RuntimeBuildCancellationRegistry>(core.Runtime.RuntimeBuildCancellations);
        Assert.IsType<OpenCodeConfigService>(core.OpenCodeServices.OpenCode);
        Assert.IsType<OpenCodeModelSyncService>(core.OpenCodeServices.OpenCodeSync);
        Assert.IsType<OpenCodeModelWorkflowService>(core.OpenCodeServices.OpenCodeModelWorkflow);
        Assert.IsType<OpenCodeModelApplicationService>(core.OpenCodeServices.OpenCodeModelApplication);
        Assert.IsType<OpenCodePageApplicationService>(core.OpenCodeServices.OpenCodePageApplication);
        Assert.IsType<OpenCodeLocalModelWorkflowService>(core.OpenCodeServices.OpenCodeLocalModelWorkflow);
        Assert.IsType<OpenCodeLocalModelApplicationService>(core.OpenCodeServices.OpenCodeLocalModelApplication);
        Assert.IsType<OpenCodeAgentWorkflowService>(core.OpenCodeServices.OpenCodeAgentWorkflow);
        Assert.IsType<OpenCodeAgentApplicationService>(core.OpenCodeServices.OpenCodeAgentApplication);
        Assert.IsType<OpenCodePageWorkflowService>(core.OpenCodeServices.OpenCodeWorkflow);
        Assert.IsType<OpenCodeRefreshApplicationService>(core.OpenCodeServices.OpenCodeRefreshApplication);
        Assert.IsType<OpenCodeFileSetApplicationService>(core.OpenCodeServices.OpenCodeFileSetApplication);
        Assert.IsType<OpenCodeSettingsSyncService>(core.OpenCodeServices.OpenCodeSettingsSync);
        Assert.IsType<LocalAppServiceStartupService>(core.App.LocalAppStartup);
        Assert.Same(sessions, core.Runtime.RuntimeSessions.Sessions);
        Assert.IsType<RuntimeSessionPersistenceService>(core.Runtime.RuntimeSessionPersistence);
        Assert.IsType<ModelCapabilityCacheService>(core.Models.ModelCapabilities);
        Assert.IsType<RuntimeSourceRepositoryService>(core.Runtime.RuntimeSources);
        Assert.IsType<RuntimeSessionRecoveryService>(core.Runtime.RuntimeSessionRecovery);
        Assert.IsType<RuntimeSessionRecoveryApplicationService>(core.Runtime.RuntimeSessionRecoveryApplication);
        Assert.IsType<RuntimeSessionReconciliationApplicationService>(core.Runtime.RuntimeSessionReconciliationApplication);
        Assert.IsType<RuntimeReadinessWorkflowService>(core.Runtime.RuntimeReadinessWorkflow);
        Assert.IsType<RuntimeReadinessCompletionService>(core.Runtime.RuntimeReadinessCompletion);
        Assert.IsType<RuntimeReadinessMonitorWorkflowService>(core.Runtime.RuntimeReadinessMonitorWorkflow);
        Assert.IsType<RuntimeReadinessCompletionApplicationService>(core.Runtime.RuntimeReadinessCompletionApplication);
        Assert.IsType<RuntimeReadinessMonitorApplicationService>(core.Runtime.RuntimeReadinessMonitorApplication);
        Assert.IsType<RuntimeSessionApplicationService>(core.Runtime.RuntimeSessionApplication);
        Assert.IsType<RuntimeEndpointProbeService>(core.Runtime.RuntimeEndpointProbe);
        Assert.IsType<RuntimeTelemetryApplicationService>(core.Runtime.RuntimeTelemetryApplication);
        Assert.IsType<RuntimeDashboardSelectionService>(core.Runtime.RuntimeDashboardSelection);
        Assert.IsType<RuntimeDashboardMetricsApplicationService>(core.Runtime.RuntimeDashboardMetricsApplication);
        Assert.IsType<RuntimeDashboardRefreshApplicationService>(core.Runtime.RuntimeDashboardRefreshApplication);
        Assert.IsType<RuntimeLogTailService>(core.Runtime.RuntimeLogTail);
        Assert.IsType<RuntimeOverviewStatusService>(core.Runtime.RuntimeOverviewStatus);
        Assert.IsType<OverviewModelSelectionApplicationService>(core.Runtime.OverviewModelSelectionApplication);
        Assert.IsType<OverviewLoadedSessionSelectionApplicationService>(core.Runtime.OverviewLoadedSessionSelectionApplication);
        Assert.IsType<ModelRuntimeStatusController>(core.Models.ModelRuntimeStatus);
        Assert.IsType<ModelRuntimeStatusRenderService>(core.Models.ModelRuntimeStatusRender);
        Assert.IsType<RuntimeLaunchAdmissionService>(core.Runtime.RuntimeLaunchAdmission);
        Assert.IsType<ModelRuntimeCommandDecisionService>(core.Models.ModelRuntimeCommands);
        Assert.IsType<ModelRuntimeLoadApplicationService>(core.Models.ModelRuntimeLoadApplication);
        Assert.IsType<ModelRuntimeUnloadApplicationService>(core.Models.ModelRuntimeUnloadApplication);
        Assert.IsType<LaunchRuntimeSelectionService>(core.Models.LaunchRuntimeSelection);
        Assert.IsType<ModelFolderApplicationService>(core.Models.ModelFolderApplication);
        Assert.IsType<ModelDeletionApplicationService>(core.Models.ModelDeletionApplication);
        Assert.IsType<ModelGatewayHostFactoryService>(core.Models.ModelGatewayHostFactory);
        Assert.IsType<ModelGatewayLifecycleApplicationService>(core.Models.ModelGatewayLifecycleApplication);
        Assert.IsType<LaunchSettingsControlStateService>(core.Models.LaunchSettingsControlStates);
        Assert.IsType<RuntimeToolPrerequisiteService>(core.Runtime.RuntimeToolPrerequisites);
        Assert.IsType<RuntimeBuildPrerequisiteService>(core.Runtime.RuntimeBuildPrerequisites);
        Assert.IsType<RuntimeLaunchPrerequisiteService>(core.Runtime.RuntimeLaunchPrerequisites);
        Assert.IsType<WindowsToolSetupWorkflowService>(core.Environment.WindowsToolSetupWorkflow);
        Assert.IsType<WindowsToolSetupApplicationService>(core.Environment.WindowsToolSetupApplication);
        Assert.IsType<WslToolSetupWorkflowService>(core.Environment.WslToolSetupWorkflow);
        Assert.IsType<WslToolSetupApplicationService>(core.Environment.WslToolSetupApplication);
        Assert.IsType<WslDistroSelectionApplicationService>(core.Environment.WslDistroSelectionApplication);
        Assert.IsType<WslPageWorkflowService>(core.Environment.WslPageWorkflow);
        Assert.IsType<ModelRuntimeLaunchApplicationService>(core.Models.ModelRuntimeLaunchApplication);
        Assert.IsType<LaunchSettingsRenderApplicationService>(core.Models.LaunchSettingsRenderApplication);
        Assert.IsType<ModelLaunchHeadSelectionApplicationService>(core.Models.ModelLaunchHeadSelectionApplication);
        Assert.IsType<ModelLaunchSettingsSaveApplicationService>(core.Models.ModelLaunchSettingsSaveApplication);
        Assert.IsType<ModelLaunchVariantSaveApplicationService>(core.Models.ModelLaunchVariantSaveApplication);
        Assert.IsType<AppSettingsWorkflowService>(loaded.App.SettingsWorkflow);
        Assert.IsType<AppSettingsApplicationService>(loaded.App.SettingsApplication);
        Assert.IsType<CacheClearWorkflowService>(loaded.App.CacheClearWorkflow);
        Assert.IsType<LogPageWorkflowService>(loaded.App.LogPageWorkflow);
        Assert.IsType<LogPageApplicationService>(loaded.App.LogPageApplication);
        Assert.IsType<LifetimeMetricsApplicationService>(loaded.App.LifetimeMetricsApplication);
        Assert.IsType<ModelLookupApplicationService>(loaded.App.ModelLookupApplication);
        Assert.NotNull(loaded.App.StateStore);
        Assert.NotNull(loaded.App.Jobs);
        Assert.NotNull(loaded.Models.Catalog);
        Assert.NotNull(loaded.Models.LaunchProfiles);
        Assert.IsType<ModelLaunchVariantWorkflowService>(loaded.Models.LaunchVariants);
        Assert.IsType<ModelLaunchSettingsWorkflowService>(loaded.Models.ModelLaunchSettingsWorkflow);
        Assert.IsType<GatewayModelLoadWorkflowService>(loaded.Gateway.GatewayModelLoadWorkflow);
        Assert.IsType<GatewayRuntimeApplicationService>(loaded.Gateway.GatewayRuntimeApplication);
        Assert.IsType<RuntimeDeletionPlanner>(loaded.Runtime.RuntimeDeletion);
        Assert.IsType<RuntimeDeletionExecutorService>(loaded.Runtime.RuntimeDeletionExecutor);
        Assert.NotNull(loaded.Runtime.Runtimes);
        Assert.IsType<RuntimePackageStatusService>(loaded.Runtime.RuntimePackageStatus);
        Assert.IsType<RuntimePackageCheckWorkflowService>(loaded.Runtime.RuntimePackageCheckWorkflow);
        Assert.IsType<RuntimePackageInstallWorkflowService>(loaded.Runtime.RuntimePackageInstallWorkflow);
        Assert.IsType<RuntimePackageApplicationService>(loaded.Runtime.RuntimePackageApplication);
        Assert.IsType<RuntimeCatalogViewService>(loaded.Runtime.RuntimeCatalogView);
        Assert.IsType<RuntimeCatalogApplicationService>(loaded.Runtime.RuntimeCatalogApplication);
        Assert.IsType<RuntimeCustomRepositoryService>(loaded.Runtime.CustomRuntimeRepositories);
        Assert.IsType<RuntimeCatalogCommandApplicationService>(loaded.Runtime.RuntimeCatalogCommands);
        Assert.IsType<RuntimeBuildDeletionApplicationService>(loaded.Runtime.RuntimeBuildDeletionApplication);
        Assert.IsType<RuntimeBuildApplicationService>(loaded.Runtime.RuntimeBuildApplication);
        Assert.IsType<RuntimeBuildJobApplicationService>(loaded.Runtime.RuntimeBuildJobApplication);
        Assert.NotNull(loaded.App.HuggingFace);
        Assert.IsType<DownloadHistoryWorkflowService>(loaded.App.DownloadHistoryWorkflow);
        Assert.IsType<DownloadHistoryApplicationService>(loaded.App.DownloadHistoryApplication);
        Assert.IsType<RuntimeSourceApplicationService>(loaded.Runtime.RuntimeSourceApplication);
        Assert.NotNull(localService);
    }


    [Fact]
    public void MainWindowUsesCompositionRootForServiceConstruction()
    {
        var mainWindow = ReadMainWindowSources();
        var factory = ReadAppServiceFactorySources();
        var factoryFileNames = ReadAppServiceFactoryFileNames();
        var serviceBundles = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "MainWindowServices.cs"));
        var execution = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.Execution.cs"));
        var source = string.Join(Environment.NewLine, mainWindow, factory, serviceBundles);

        Assert.Contains("new AppServiceFactory(_workspaceRoot)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private readonly MainWindowInfrastructureServices _infrastructureServices;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_serviceFactory.CreateMainWindowInfrastructureServices()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_serviceFactory.CreateMainWindowCoreServices(_infrastructureServices.CoreServiceRequest())", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_infrastructureServices.LoadedServiceRequest(stateStore, _coreServices)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ApplyLoadedServices,", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("AppWorkflowServiceState", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private MainWindowLoadedAppServices? _appServices;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private MainWindowLoadedModelServices? _modelServices;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private MainWindowLoadedGatewayServices? _gatewayServices;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private MainWindowLoadedRuntimeServices? _runtimeServices;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private MainWindowLoadedAppServices AppServices", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private MainWindowLoadedModelServices ModelServices", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private MainWindowLoadedGatewayServices GatewayServices", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private MainWindowLoadedRuntimeServices RuntimeServices", mainWindow, StringComparison.Ordinal);
        Assert.Contains("=> _appServices ?? throw new InvalidOperationException(\"Loaded app services are not initialized.\");", mainWindow, StringComparison.Ordinal);
        Assert.Contains("=> _modelServices ?? throw new InvalidOperationException(\"Loaded model services are not initialized.\");", mainWindow, StringComparison.Ordinal);
        Assert.Contains("=> _gatewayServices ?? throw new InvalidOperationException(\"Loaded gateway services are not initialized.\");", mainWindow, StringComparison.Ordinal);
        Assert.Contains("=> _runtimeServices ?? throw new InvalidOperationException(\"Loaded runtime services are not initialized.\");", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly ShellIntegrationService _shellIntegration;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly GpuStatusProbeService _gpuStatus;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly FileSystemDialogService _fileSystemDialogs;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly ClipboardService _clipboard;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly DialogService _dialogs;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_appServices = services.App;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_modelServices = services.Models;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_gatewayServices = services.Gateway;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_runtimeServices = services.Runtime;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.HuggingFaceServices.HuggingFaceModelCards", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.HuggingFaceServices.HuggingFaceSearchApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.HuggingFaceServices.HuggingFaceDownloadApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.SettingsRowActions", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.FolderSettingsApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.AppLogApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.LifetimeMetricResetApplication", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_shellIntegration = _coreServices.ShellIntegration", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_gpuStatus = _coreServices.GpuStatus", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_fileSystemDialogs = _coreServices.FileSystemDialogs", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_clipboard = _coreServices.Clipboard", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_dialogs = _coreServices.Dialogs", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.ShellIntegration.OpenFolder", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.GpuStatus.MemoryAsync", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.FileSystemDialogs.PickFolder", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.Clipboard.SetText", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.Dialogs.Notify", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private readonly MainWindowViewModel _viewModel;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("var uiState = _coreServices.Ui.UiState;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_viewModel = uiState.ViewModel", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_openCodeFileSet = uiState.OpenCodeFileSet", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_runtimeCatalogState = uiState.RuntimeCatalogState", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_launchSettingsPanel = uiState.LaunchSettingsPanel", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_downloadHistoryPageState = uiState.DownloadHistoryPageState", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_environmentPageSnapshots = uiState.EnvironmentPageSnapshots", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly MainWindowViewModel _viewModel = new();", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly OpenCodeFileSetState _openCodeFileSet = new();", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly EnvironmentPageSnapshotCache _environmentPageSnapshots = new();", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly SelectedModelCapabilityController _selectedCapabilities;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_selectedCapabilities = _coreServices.SelectedCapabilities", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly AdvancedSectionStateController _advancedSections;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_advancedSections = _coreServices.AdvancedSections", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly LaunchSettingsEditorSession _launchSettingsEditor;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_launchSettingsEditor = _coreServices.LaunchSettingsEditor", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly SelectionReentrancyCoordinator _selectionReentrancy;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_selectionReentrancy = _coreServices.SelectionReentrancy", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly RuntimeGpuSummaryApplicationService _runtimeGpuSummaryApplication;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeGpuSummaryApplication = _coreServices.RuntimeGpuSummaryApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeDashboardRefreshApplication", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly UiBusyStateController _uiBusyState;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_uiBusyState = _coreServices.UiBusyState", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly TrayWindowStateController _trayWindowState;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_trayWindowState = _coreServices.TrayWindowState", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly RuntimeReadinessMonitorRegistry _runtimeReadinessMonitors;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeReadinessMonitors = _coreServices.RuntimeReadinessMonitors", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly DebouncedAsyncAction _launchSettingsRefresh;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_launchSettingsRefresh = _coreServices.LaunchSettingsRefresh", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.SelectedCapabilities.Apply", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.LaunchSettingsEditor.Load", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.SelectionReentrancy.TryBeginModelGridSelection", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.RuntimeGpuSummaryApplication.SummaryAsync", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.UiBusyState.Begin", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.TrayWindowState.BuildMinimizePlan", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.RuntimeReadinessMonitors.Start", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.LaunchSettingsRefresh.Schedule", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelectedModelCapabilityController()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new AdvancedSectionStateController()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new LaunchSettingsEditorSession()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelectionReentrancyCoordinator()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new GpuSummaryCache()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new UiBusyStateController()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new TrayWindowStateController()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeReadinessMonitorRegistry()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new DebouncedAsyncAction", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.HelpSections", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.HelpNavigation", mainWindow, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowLoadedAppServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowLoadedModelServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowLoadedGatewayServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowLoadedRuntimeServices(", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public AppSettingsApplicationService SettingsApplication => App.SettingsApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public LogPageApplicationService LogPageApplication => App.LogPageApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public JobEngine Jobs => App.Jobs;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public HuggingFaceService HuggingFace => App.HuggingFace;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public DownloadHistoryApplicationService DownloadHistoryApplication => App.DownloadHistoryApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("new MainWindowLoadedAppServices(", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("ModelWorkflowServiceState", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("public ModelCatalogService Catalog => Models.Catalog;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public ModelCatalogRefreshApplicationService ModelCatalogRefreshApplication => Models.ModelCatalogRefreshApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public ModelLaunchProfileService LaunchProfiles => Models.LaunchProfiles;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public ModelLaunchVariantWorkflowService LaunchVariants => Models.LaunchVariants;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public ModelLaunchSettingsWorkflowService ModelLaunchSettingsWorkflow => Models.ModelLaunchSettingsWorkflow;", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("new MainWindowLoadedModelServices(", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("public GatewayRuntimeApplicationService GatewayRuntimeApplication => Gateway.GatewayRuntimeApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("new MainWindowLoadedGatewayServices(", factory, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelRuntimeLoadApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelRuntimeUnloadApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.LaunchRuntimeSelection", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelFolderApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelDeletionApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.LaunchSettingsControlStates", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelGatewayHostFactory.CreateRuntimeController(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelGatewayHostFactory.CreateGatewayHost", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelGatewayLifecycleApplication.RestartAsync(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelGatewayLifecycleApplication.StopAsync(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("StopModelGatewayAsync,", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeWorkflowServiceState", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeDeletionPlanner RuntimeDeletion => Runtime.RuntimeDeletion;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimePackageApplicationService RuntimePackageApplication => Runtime.RuntimePackageApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeCatalogApplicationService RuntimeCatalogApplication => Runtime.RuntimeCatalogApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeCustomRepositoryService CustomRuntimeRepositories => Runtime.CustomRuntimeRepositories;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeCatalogCommandApplicationService RuntimeCatalogCommands => Runtime.RuntimeCatalogCommands;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeBuildDeletionApplicationService RuntimeBuildDeletionApplication => Runtime.RuntimeBuildDeletionApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeBuildApplicationService RuntimeBuildApplication => Runtime.RuntimeBuildApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeBuildJobApplicationService RuntimeBuildJobApplication => Runtime.RuntimeBuildJobApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeSourceApplicationService RuntimeSourceApplication => Runtime.RuntimeSourceApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("new MainWindowLoadedRuntimeServices(", factory, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeLogTail", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeOverviewStatus", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.OverviewModelSelectionApplication", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.OverviewLoadedSessionSelectionApplication", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCodeWorkflowServiceState", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeServices", mainWindow, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowCoreServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowCoreUiServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowCoreAppServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowCoreHuggingFaceServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowCoreOpenCodeServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowCoreRuntimeServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowCoreModelServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowCoreEnvironmentServices(", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public MainWindowUiState UiState => Ui.UiState;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public BackgroundTaskApplicationService BackgroundTasks => App.BackgroundTasks;", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.BackgroundTasks.RunAsync(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("public HuggingFaceSearchApplicationService HuggingFaceSearchApplication => HuggingFaceServices.HuggingFaceSearchApplication;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public OpenCodeConfigService OpenCode => OpenCodeServices.OpenCode;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public RuntimeSessionCoordinator RuntimeSessions => Runtime.RuntimeSessions;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public ModelCapabilityCacheService ModelCapabilities => Models.ModelCapabilities;", serviceBundles, StringComparison.Ordinal);
        Assert.DoesNotContain("public WslPageWorkflowService WslPageWorkflow => Environment.WslPageWorkflow;", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowLoadedServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed record MainWindowInfrastructureServices(", serviceBundles, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class AppServiceFactory", factory, StringComparison.Ordinal);
        Assert.Contains("AppServiceFactory.Foundation.cs", factoryFileNames, StringComparison.Ordinal);
        Assert.Contains("AppServiceFactory.Catalog.cs", factoryFileNames, StringComparison.Ordinal);
        Assert.Contains("AppServiceFactory.Core.cs", factoryFileNames, StringComparison.Ordinal);
        Assert.Contains("AppServiceFactory.Loaded.cs", factoryFileNames, StringComparison.Ordinal);
        Assert.Contains("AppServiceFactory.Runtime.cs", factoryFileNames, StringComparison.Ordinal);
        Assert.Contains("AppServiceFactory.RuntimeModel.cs", factoryFileNames, StringComparison.Ordinal);
        Assert.Contains("public MainWindowInfrastructureServices CreateMainWindowInfrastructureServices()", factory, StringComparison.Ordinal);
        Assert.Contains("public MainWindowCoreServices CreateMainWindowCoreServices", factory, StringComparison.Ordinal);
        Assert.Contains("public MainWindowLoadedServices CreateMainWindowLoadedServices", factory, StringComparison.Ordinal);
        Assert.Contains("new MainWindowCoreUiServices(", factory, StringComparison.Ordinal);
        Assert.Contains("new MainWindowCoreAppServices(", factory, StringComparison.Ordinal);
        Assert.Contains("new MainWindowCoreHuggingFaceServices(", factory, StringComparison.Ordinal);
        Assert.Contains("new MainWindowCoreOpenCodeServices(", factory, StringComparison.Ordinal);
        Assert.Contains("new MainWindowCoreRuntimeServices(", factory, StringComparison.Ordinal);
        Assert.Contains("new MainWindowCoreModelServices(", factory, StringComparison.Ordinal);
        Assert.Contains("new MainWindowCoreEnvironmentServices(", factory, StringComparison.Ordinal);
        Assert.Contains("CreateAppUpdateService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppUpdateHttpClient()", source, StringComparison.Ordinal);
        Assert.Contains("CreateLoadedModelSessionManager(processRunner)", source, StringComparison.Ordinal);
        Assert.Contains("CreateLoadedModelSessionManager()", source, StringComparison.Ordinal);
        Assert.Contains("CreateLlamaProcessSupervisor", source, StringComparison.Ordinal);
        Assert.Contains("CreateWslRuntimeStopService(processRunner)", source, StringComparison.Ordinal);
        Assert.Contains("CreateNativeRuntimeStopService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateProcessRunner()", source, StringComparison.Ordinal);
        Assert.Contains("CreateWindowsEnvironmentService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateWslEnvironmentService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeProbeClient()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeMetricsClient()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageClient()", source, StringComparison.Ordinal);
        Assert.Contains("_serviceFactory.CreateStateStore", source, StringComparison.Ordinal);
        Assert.Contains("CreateStateStoreInitializationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateBackgroundTaskApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateForegroundTaskApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateDownloadCompletionApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.BackgroundTasks.RunAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.ForegroundTasks.RunBusyAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.ForegroundTasks.RunEventAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.DownloadCompletionApplication.MonitorAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new BackgroundTaskApplicationActions(SetStatus, WriteAppLogAsync)", source, StringComparison.Ordinal);
        Assert.Contains("private ForegroundTaskApplicationActions ForegroundTaskActions()", source, StringComparison.Ordinal);
        Assert.Contains("new DownloadCompletionApplicationActions(", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppStartupApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppStartupBackgroundApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.StartupApplication.StartAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.StartupBackgroundApplication.SeedSuggestedLaunchProfilesAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new AppStartupApplicationRequest(", source, StringComparison.Ordinal);
        Assert.Contains("new AppStartupApplicationActions(", source, StringComparison.Ordinal);
        Assert.Contains("new AppStartupSuggestedLaunchProfileSeedRequest(", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppSettingsUpdateService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppUpdateWorkflowService(request.AppUpdates)", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppUpdateApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateCacheClearApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateHuggingFaceModelCardApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateHuggingFaceSearchApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateHuggingFaceDownloadApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateSettingsRowActionApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateFolderSettingsApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppLogApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateLifetimeMetricResetApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppShutdownDecisionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppShutdownStateController()", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppShutdownApplicationService(shutdownDecisions, shutdownState)", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppShutdownCleanupApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.ShutdownApplication.BeginShutdownAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.ShutdownCleanupApplication.CleanupAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new AppShutdownApplicationActions(", source, StringComparison.Ordinal);
        Assert.Contains("new AppShutdownCleanupActions(", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppSettingsWorkflowService(stateStore, core.App.SettingsUpdates)", source, StringComparison.Ordinal);
        Assert.Contains("CreateWindowsStartupRegistrationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateAppSettingsApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("core.OpenCodeServices.OpenCodeSettingsSync", source, StringComparison.Ordinal);
        Assert.Contains("CreateSettingsPageDefinitionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateHelpSectionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateHelpNavigationApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateCacheClearWorkflowService(stateStore)", source, StringComparison.Ordinal);
        Assert.Contains("CreateLogPageWorkflowService(stateStore)", source, StringComparison.Ordinal);
        Assert.Contains("CreateLogPageApplicationService(logPageWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("CreateLifetimeMetricsApplicationService(stateStore)", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelLookupApplicationService(stateStore)", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelLaunchProfileService(stateStore, request.Sessions)", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelLaunchVariantWorkflowService(catalog, launchProfiles)", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelLaunchSettingsWorkflowService(launchProfiles)", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelCapabilityCacheService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeDeletionPlanner(stateStore, launchProfiles, request.Sessions)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeDeletionExecutorService(stateStore)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildMarkerService(request.ProcessRunner)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildCancellationRegistry()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildExecutionService(request.ProcessRunner, runtimes, core.Runtime.RuntimeBuildMarkers)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildWorkflowService(jobs, runtimeBuildExecutor, core.Runtime.RuntimeSources, stateStore)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildJobControlService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildJobApplicationService(runtimeBuildJobControls)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageStatusService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageUpdateCheckService(request.RuntimePackageClient, runtimePackageStatus)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeCatalogDataService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeCatalogViewService(runtimePackageStatus)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeCatalogApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeCustomRepositoryService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeCatalogCommandApplicationService(customRuntimeRepositories)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildDeletionApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("runtimeDeletionExecutor", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageInstallService(request.RuntimePackageClient, runtimes)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageJobService(jobs)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageCheckWorkflowService(runtimePackageJobs, runtimePackageUpdateChecker)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageWslFileService(request.ProcessRunner)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageInstallWorkflowService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimePackageApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSourceRepositoryService(request.ProcessRunner)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSourceWorkflowService(core.Runtime.RuntimeSources, jobs)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSourceApplicationService(stateStore, core.Runtime.RuntimeCatalogData, runtimeSourceWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("CreateDownloadHistoryWorkflowService(stateStore, huggingFace)", source, StringComparison.Ordinal);
        Assert.Contains("CreateDownloadHistoryApplicationService(downloadHistoryWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeModelSyncService(openCode)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeModelWorkflowService(openCode)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeModelApplicationService(openCodeModelWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodePageApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeLocalModelWorkflowService(openCodeSync)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeLocalModelApplicationService(openCodeLocalModelWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeAgentWorkflowService(openCode)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeAgentApplicationService(openCodeAgentWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodePageWorkflowService(openCode, openCodeSync)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeRefreshApplicationService(openCodeWorkflow, openCodePageApplication)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeFileSetApplicationService(openCodeWorkflow, openCodePageApplication)", source, StringComparison.Ordinal);
        Assert.Contains("CreateOpenCodeSettingsSyncService(openCodeWorkflow, openCodeSync)", source, StringComparison.Ordinal);
        Assert.Contains("CreateLocalAppServiceStartupService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionCoordinator(request.Sessions)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionPersistenceService(activeSessions, request.Sessions)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionRecoveryService(request.Sessions)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionRecoveryApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionReconciler()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionReconciliationApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeReadinessWorkflowService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeReadinessCompletionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeReadinessMonitorWorkflowService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeReadinessCompletionApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeReadinessMonitorApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateMainWindowUiState()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionCommandService(runtimeSessions, runtimeSessionActions)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionFollowupApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeSessionApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeStartFollowupService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeStartFollowupApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeEndpointProbeService(request.RuntimeProbeClient)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeMetricPollerService(request.MetricsClient)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeTelemetryApplicationService(runtimeMetricPoller)", source, StringComparison.Ordinal);
        Assert.Contains("CreateShellIntegrationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateGpuStatusProbeService(request.ProcessRunner)", source, StringComparison.Ordinal);
        Assert.Contains("CreateFileSystemDialogService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateClipboardService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateDialogService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateUiTimerFactory()", source, StringComparison.Ordinal);
        Assert.Contains("CreateUiAsyncRefreshTimerController(uiTimerFactory)", source, StringComparison.Ordinal);
        Assert.Contains("CreateGatewayActivityStatusController(", source, StringComparison.Ordinal);
        Assert.Contains("CreateSelectedModelCapabilityController()", source, StringComparison.Ordinal);
        Assert.Contains("CreateAdvancedSectionStateController()", source, StringComparison.Ordinal);
        Assert.Contains("CreateLaunchSettingsEditorSession()", source, StringComparison.Ordinal);
        Assert.Contains("CreateSelectionReentrancyCoordinator()", source, StringComparison.Ordinal);
        Assert.Contains("CreateGpuSummaryCache()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeGpuSummaryApplicationService(gpuStatus, gpuSummaryCache)", source, StringComparison.Ordinal);
        Assert.Contains("CreateUiBusyStateController()", source, StringComparison.Ordinal);
        Assert.Contains("CreateTrayWindowStateController()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeReadinessMonitorRegistry()", source, StringComparison.Ordinal);
        Assert.Contains("CreateLaunchSettingsRefreshAction()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeDashboardSelectionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeDashboardRenderDecisionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeMetricRowsRenderService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeDashboardMetricsApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeDashboardRefreshApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeLogTailService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeOverviewStatusService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateOverviewModelSelectionApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateOverviewLoadedSessionSelectionApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeStatusController(", source, StringComparison.Ordinal);
        Assert.Contains("CreateUiTimerFactory()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeStatusRenderService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeDashboardRefreshCoordinator()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeMetricSummaryTracker()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeLifetimeCounterTracker()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeIdleUnloadPolicyService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeLaunchAdmissionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeCommandDecisionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeLoadApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeUnloadApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateLaunchRuntimeSelectionService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelFolderApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelDeletionApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelGatewayHostFactoryService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelGatewayLifecycleApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateLaunchSettingsControlStateService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeToolPrerequisiteService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeBuildPrerequisiteService(runtimeToolPrerequisites)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeLaunchPrerequisiteService(runtimeToolPrerequisites)", source, StringComparison.Ordinal);
        Assert.Contains("CreateVisibleCommandLaunchService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateWindowsToolSetupWorkflowService(visibleCommandLauncher, request.WindowsEnvironment)", source, StringComparison.Ordinal);
        Assert.Contains("CreateWindowsToolSetupApplicationService(windowsToolSetupWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("CreateWslToolSetupWorkflowService(visibleCommandLauncher)", source, StringComparison.Ordinal);
        Assert.Contains("CreateWslToolSetupApplicationService(wslToolSetupWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("CreateWslDistroSelectionApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateWslPageWorkflowService(request.WslEnvironment, request.ProcessRunner)", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeLaunchPreparationService(", source, StringComparison.Ordinal);
        Assert.Contains("runtimeLaunchAdmission,", source, StringComparison.Ordinal);
        Assert.Contains("gpuStatus);", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelRuntimeLaunchApplicationService(", source, StringComparison.Ordinal);
        Assert.Contains("CreateLaunchSettingsRenderApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelLaunchHeadSelectionApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelLaunchSettingsSaveApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateModelLaunchVariantSaveApplicationService()", source, StringComparison.Ordinal);
        Assert.Contains("CreateGatewayModelLoadWorkflowService(stateStore, launchProfiles, core.Runtime.RuntimeSessions)", source, StringComparison.Ordinal);
        Assert.Contains("CreateGatewayRuntimeApplicationService(gatewayModelLoadWorkflow)", source, StringComparison.Ordinal);
        Assert.Contains("ModelGatewayHostFactoryService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new StateStore(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StateStore.QuarantineDatabaseFiles", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (SqliteException)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new StateStoreInitializationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new BackgroundTaskApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ForegroundTaskApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new DownloadCompletionApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task RunBackgroundAsync", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (OperationCanceledException)", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception ex)", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppStartupApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppStartupBackgroundApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStoreInitialization.InitializeAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_localAppStartup.StartAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppUpdateService", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new HuggingFaceModelCardApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new HuggingFaceSearchApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new HuggingFaceDownloadApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new LoadedModelSessionManager", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new LlamaProcessSupervisor", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new TrackedProcessRunner", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new WindowsEnvironmentService", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new WslEnvironmentService", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new HttpClient", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new MainWindowCoreServiceRequest", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new MainWindowLoadedServiceRequest", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppSettingsUpdateService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppUpdateWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new SettingsRowActionApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new FolderSettingsApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppLogApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new LifetimeMetricResetApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppShutdownDecisionService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppShutdownStateController(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppShutdownApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppShutdownCleanupApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_shutdownDecisions.Build(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_shutdownState.BeginClosing()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_shutdownState.MarkCleanupComplete()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_shutdownRequested", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_shutdownCleanupComplete", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("Close and stop loaded models?", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("Close and pause downloads?", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppSettingsWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AppSettingsApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private AppSettingsUpdateService? _settingsUpdates;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private AppSettingsWorkflowService? _settingsWorkflow;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new SettingsPageDefinitionService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new HelpSectionService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new HelpNavigationApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new CacheClearWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new LogPageWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new LogPageApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new JobEngine(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelCatalogService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelLaunchProfileService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelLaunchVariantWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelLaunchSettingsWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private ModelCatalogService? _catalog;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private ModelLaunchProfileService? _launchProfiles;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private ModelLaunchVariantWorkflowService? _launchVariants;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private GatewayModelLoadWorkflowService? _gatewayModelLoadWorkflow;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelCapabilityCacheService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeDeletionPlanner(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeDeletionExecutorService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildMarkerService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildCancellationRegistry(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildExecutionService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildJobControlService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildJobApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private RuntimeRegistryService? _runtimes;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private RuntimeCustomRepositoryService? _customRuntimeRepositories;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpenCodeConfigService? _openCode;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpenCodeModelSyncService? _openCodeSync;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpenCodeModelWorkflowService? _openCodeModelWorkflow;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpenCodeLocalModelWorkflowService? _openCodeLocalModelWorkflow;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpenCodeAgentWorkflowService? _openCodeAgentWorkflow;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpenCodePageWorkflowService? _openCodeWorkflow;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeServices.Page", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private OpenCodeSettingsSyncService? _openCodeSettingsSync;", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeCatalogDataService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeCatalogViewService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeCatalogApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeCustomRepositoryService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeCatalogCommandApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildDeletionApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimePackageStatusService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimePackageUpdateCheckService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimePackageInstallService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimePackageJobService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimePackageCheckWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimePackageWslFileService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimePackageInstallWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimePackageApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSourceRepositoryService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSourceWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSourceApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeRegistryService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new HuggingFaceService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new DownloadHistoryWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new DownloadHistoryApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeConfigService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeModelSyncService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeModelWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeModelApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodePageApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeLocalModelWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeLocalModelApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeAgentWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeAgentApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodePageWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeRefreshApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeFileSetApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeSettingsSyncService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new LocalAppServiceStartupService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (HttpListenerException)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (SocketException)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSessionPersistenceService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSessionApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSessionRecoveryService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSessionRecoveryApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSessionReconciler(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeSessionReconciliationApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeReadinessWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeEndpointProbeService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeMetricPollerService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeTelemetryApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new GpuStatusProbeService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("GpuStatusService.MemoryAsync", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("GpuStatusService.SummaryAsync", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("GpuStatusService.MemoryAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeLogTailService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeOverviewStatusService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OverviewModelSelectionApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new OverviewLoadedSessionSelectionApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeLaunchAdmissionService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelRuntimeCommandDecisionService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelRuntimeLoadApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelRuntimeUnloadApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new LaunchRuntimeSelectionService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelFolderApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelDeletionApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelGatewayLifecycleApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new LaunchSettingsControlStateService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelGatewayService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_serviceFactory.CreateModelGatewayRuntimeController", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_serviceFactory.CreateModelGatewayService", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("await _gateway.DisposeAsync()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeToolPrerequisiteService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeBuildPrerequisiteService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeLaunchPrerequisiteService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new WindowsToolSetupWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new WindowsToolSetupApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new WslToolSetupWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new WslToolSetupApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new WslDistroSelectionApplicationService(", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("new WslPageWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelRuntimeLaunchPreparationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelRuntimeLaunchApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new GatewayModelLoadWorkflowService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new GatewayRuntimeApplicationService(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModelGatewayRuntimeController(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new LocalAppService(", source, StringComparison.Ordinal);
    }


    private sealed class FakeLocalAppServiceHost : ILocalAppServiceHost
    {
        private readonly Exception? _failure;

        public FakeLocalAppServiceHost(int port, Exception? failure = null)
        {
            _failure = failure;
            BaseUri = new Uri($"http://127.0.0.1:{port}/");
        }

        public Uri BaseUri { get; }
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }

        public Task StartAsync()
        {
            if (_failure is not null) throw _failure;
            Started = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSingleInstanceLease : ISingleInstanceLease
    {
        public FakeSingleInstanceLease(bool ownsInstance)
        {
            OwnsInstance = ownsInstance;
        }

        public bool OwnsInstance { get; private set; }

        public bool Released { get; private set; }

        public bool Disposed { get; private set; }

        public void Release()
        {
            Released = true;
            OwnsInstance = false;
        }

        public void Dispose()
            => Disposed = true;
    }

    [Fact]
    public async Task ModelDeletionApplicationServiceOwnsPromptsBlockingAndRefresh()
    {
        var root = CreateTempRoot();
        var modelsRoot = Path.Combine(root, "models");
        var baseModel = new ModelRecord(
            "base-model",
            "Base Model",
            Path.Combine(modelsRoot, "base", "model.gguf"),
            OwnershipKind.External,
            "{}",
            DateTimeOffset.UtcNow);
        var appOwned = baseModel with
        {
            Id = "app-owned",
            Name = "Downloaded Model",
            Ownership = OwnershipKind.AppOwned
        };
        var alias = new ModelRecord(
            "variant-model",
            "Base Model 32K",
            baseModel.ModelPath,
            OwnershipKind.RegistryOnly,
            ModelAliasService.CreateMetadata(baseModel, [baseModel]),
            DateTimeOffset.UtcNow);
        var service = new ModelDeletionApplicationService();
        var calls = new List<string>();
        var loaded = false;
        var confirm = true;

        ModelDeletionApplicationActions Actions()
            => new(
                _ => loaded,
                confirmation =>
                {
                    calls.Add($"confirm:{confirmation.Title}:{confirmation.Message}");
                    return confirm;
                },
                async (message, action) =>
                {
                    calls.Add($"busy:{message}");
                    await action();
                },
                (model, rootPath) =>
                {
                    calls.Add($"delete:{model.Id}:{rootPath}");
                    return Task.CompletedTask;
                },
                () =>
                {
                    calls.Add("refresh-models");
                    return Task.CompletedTask;
                },
                () =>
                {
                    calls.Add("refresh-overview");
                    return Task.CompletedTask;
                },
                status => calls.Add($"status:{status}"));

        var ignored = await service.DeleteAsync(null, modelsRoot, Actions());

        loaded = true;
        var blocked = await service.DeleteAsync(baseModel, modelsRoot, Actions());

        loaded = false;
        confirm = false;
        var cancelled = await service.DeleteAsync(appOwned, modelsRoot, Actions());

        confirm = true;
        var deleted = await service.DeleteAsync(alias, modelsRoot, Actions());
        var externalConfirmation = ModelDeletionApplicationService.BuildConfirmation(baseModel);
        var appOwnedConfirmation = ModelDeletionApplicationService.BuildConfirmation(appOwned);
        var aliasConfirmation = ModelDeletionApplicationService.BuildConfirmation(alias);

        Assert.Equal(ModelDeletionApplicationOutcome.Ignored, ignored);
        Assert.Equal(ModelDeletionApplicationOutcome.BlockedLoaded, blocked);
        Assert.Equal(ModelDeletionApplicationOutcome.Cancelled, cancelled);
        Assert.Equal(ModelDeletionApplicationOutcome.Deleted, deleted);
        Assert.Contains("status:Unload the selected model before deleting it.", calls);
        Assert.Contains("remove the model registration only", externalConfirmation.Message, StringComparison.Ordinal);
        Assert.Contains("delete the downloaded model files", appOwnedConfirmation.Message, StringComparison.Ordinal);
        Assert.Contains("remove this saved model variant without deleting the GGUF file", aliasConfirmation.Message, StringComparison.Ordinal);
        Assert.Contains(calls, call => call.StartsWith("confirm:Remove model:", StringComparison.Ordinal)
            && call.Contains("delete the downloaded model files", StringComparison.Ordinal));
        Assert.Contains("busy:Removing model...", calls);
        Assert.Contains($"delete:{alias.Id}:{modelsRoot}", calls);
        Assert.True(calls.IndexOf($"delete:{alias.Id}:{modelsRoot}") < calls.IndexOf("refresh-models"));
        Assert.True(calls.IndexOf("refresh-models") < calls.IndexOf("refresh-overview"));
    }

    private static WindowsStartupRegistrationService DisabledStartupRegistration()
        => new(() => null, _ => { }, () => { }, () => "app.exe");

    private sealed class FakeDownloadOperations : IHuggingFaceDownloadOperations
    {
        private readonly HashSet<string> _activeJobIds = new(StringComparer.OrdinalIgnoreCase);

        public List<string> ResumedJobIds { get; } = [];
        public List<string> PausedJobIds { get; } = [];
        public List<string> StoppedJobIds { get; } = [];

        public Task ResumeDownloadAsync(JobRecord job, AppSettings settings)
        {
            ResumedJobIds.Add(job.Id);
            _activeJobIds.Add(job.Id);
            return Task.CompletedTask;
        }

        public Task PauseDownloadAsync(JobRecord job)
        {
            PausedJobIds.Add(job.Id);
            _activeJobIds.Remove(job.Id);
            return Task.CompletedTask;
        }

        public Task StopDownloadAsync(JobRecord job)
        {
            StoppedJobIds.Add(job.Id);
            _activeJobIds.Remove(job.Id);
            return Task.CompletedTask;
        }

        public bool IsDownloadActive(string jobId) => _activeJobIds.Contains(jobId);
    }

}
