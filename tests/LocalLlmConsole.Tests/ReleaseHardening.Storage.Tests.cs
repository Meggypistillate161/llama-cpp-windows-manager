using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public async Task StateStoreHandlesConcurrentJobTraffic()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();

        var writes = Enumerable.Range(0, 40).Select(async index =>
        {
            var now = DateTimeOffset.UtcNow;
            await store.UpsertJobAsync(new JobRecord(
                $"job-{index}",
                "test",
                JobStatus.Running,
                "{}",
                Path.Combine(root, "logs", $"job-{index}.log"),
                now,
                now));
            _ = await store.ListJobsAsync();
        });

        await Task.WhenAll(writes);

        var jobs = await store.ListJobsAsync();
        Assert.Equal(40, jobs.Count);

        await store.DeleteJobAsync("job-0");
        jobs = await store.ListJobsAsync();

        Assert.Equal(39, jobs.Count);
        Assert.DoesNotContain(jobs, job => job.Id == "job-0");
    }

    [Fact]
    public async Task StateStoreRecordsSchemaMigrationsIdempotently()
    {
        var root = CreateTempRoot();
        var databasePath = Path.Combine(root, "state", "local-llm-console.db");
        await using (var store = new StateStore(databasePath))
        {
            await store.InitializeAsync();
            await store.InitializeAsync();
        }

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), MIN(name) FROM migrations WHERE id = 1;";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("baseline-v1", reader.GetString(1));
    }


    [Fact]
    public void StateStoreQuarantinesCorruptDatabaseFiles()
    {
        var root = CreateTempRoot();
        var databasePath = Path.Combine(root, "state", "local-llm-console.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        File.WriteAllText(databasePath, "db");
        File.WriteAllText(databasePath + "-wal", "wal");
        File.WriteAllText(databasePath + "-shm", "shm");

        var quarantine = StateStore.QuarantineDatabaseFiles(databasePath);

        Assert.False(File.Exists(databasePath));
        Assert.False(File.Exists(databasePath + "-wal"));
        Assert.False(File.Exists(databasePath + "-shm"));
        Assert.Equal("db", File.ReadAllText(Path.Combine(quarantine, "local-llm-console.db")));
        Assert.Equal("wal", File.ReadAllText(Path.Combine(quarantine, "local-llm-console.db-wal")));
        Assert.Equal("shm", File.ReadAllText(Path.Combine(quarantine, "local-llm-console.db-shm")));
    }


    [Fact]
    public async Task StateStorePersistsLanModelAccessSettings()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();

        var settings = AppSettings.CreateDefault(root) with
        {
            ModelAccessMode = "lan",
            Host = "0.0.0.0",
            ModelApiKey = "test-key"
        };

        await store.SaveAppSettingsAsync(settings);
        var loaded = await store.GetAppSettingsAsync(root);

        Assert.Equal("lan", loaded.ModelAccessMode);
        Assert.Equal("0.0.0.0", loaded.Host);
        Assert.Equal("test-key", loaded.ModelApiKey);
    }


    [Fact]
    public async Task StateStorePersistsRuntimeSourceCleanupSetting()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();

        var settings = AppSettings.CreateDefault(root) with
        {
            DeleteRuntimeSourceAfterSuccessfulBuild = false
        };

        await store.SaveAppSettingsAsync(settings);
        var loaded = await store.GetAppSettingsAsync(root);

        Assert.False(loaded.DeleteRuntimeSourceAfterSuccessfulBuild);
    }

    [Fact]
    public async Task DeletingModelFreesSavedLaunchProfilePort()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var model = new ModelRecord(
            "model-1",
            "Test Model",
            Path.Combine(root, "models", "test.gguf"),
            OwnershipKind.External,
            "{}",
            DateTimeOffset.UtcNow);

        await store.UpsertModelAsync(model);
        await store.SaveModelLaunchSettingsAsync(model.Id, ModelLaunchSettings.FromAppSettings(AppSettings.CreateDefault(root) with { Port = 8083 }));
        await store.DeleteModelAsync(model.Id);

        Assert.Null(await store.GetModelLaunchSettingsAsync(model.Id));
    }


    [Fact]
    public async Task ActiveRuntimeSessionStorePersistsAndClearsSession()
    {
        var root = CreateTempRoot();
        var store = new ActiveRuntimeSessionStore(root);
        var settings = AppSettings.CreateDefault(root) with { ModelApiKey = new string('a', 32) };
        var session = new ActiveRuntimeSession(
            "model-id",
            "runtime-id",
            settings,
            Path.Combine(root, "logs", "runtime.log"),
            DateTimeOffset.UtcNow,
            "marker",
            1234);

        await store.SaveAsync(session, TestContext.Current.CancellationToken);
        var loaded = await store.TryReadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(session.ModelId, loaded.ModelId);
        Assert.Equal(session.RuntimeId, loaded.RuntimeId);
        Assert.Equal(session.ProcessMarker, loaded.ProcessMarker);
        Assert.Equal(session.ProcessId, loaded.ProcessId);

        store.Clear();
        Assert.Null(await store.TryReadAsync(TestContext.Current.CancellationToken));
    }


    [Fact]
    public async Task ActiveRuntimeSessionStoreClearsCorruptSession()
    {
        var root = CreateTempRoot();
        var store = new ActiveRuntimeSessionStore(root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.SessionPath)!);
        await File.WriteAllTextAsync(store.SessionPath, "{not-json", TestContext.Current.CancellationToken);

        var loaded = await store.TryReadAsync(TestContext.Current.CancellationToken);

        Assert.Null(loaded);
        Assert.False(File.Exists(store.SessionPath));
    }


    [Fact]
    public async Task StateStoreMigratesOnlyLegacyModelLaunchDefaults()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();

        await store.SaveAppSettingsAsync(AppSettings.CreateDefault(root) with
        {
            ContextSize = 0,
            GpuLayers = 0,
            BatchSize = 2048,
            CacheTypeK = "f16",
            CacheTypeV = "f16",
            Temperature = 0.8,
            MicroBatchSize = 256,
            VisionImageMinTokens = 256,
            VisionImageMaxTokens = 1024
        });

        var migrated = await store.GetAppSettingsAsync(root);

        Assert.Equal(131_072, migrated.ContextSize);
        Assert.Equal(999, migrated.GpuLayers);
        Assert.Equal(4096, migrated.BatchSize);
        Assert.Equal("q8_0", migrated.CacheTypeK);
        Assert.Equal("q8_0", migrated.CacheTypeV);
        Assert.Equal(0.65, migrated.Temperature);
        Assert.Equal(256, migrated.MicroBatchSize);
        Assert.Equal(256, migrated.VisionImageMinTokens);
        Assert.Equal(1024, migrated.VisionImageMaxTokens);
    }


    [Fact]
    public async Task StateStoreMigratesLegacySavedModelLaunchDefaults()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var now = DateTimeOffset.UtcNow;
        await store.UpsertModelAsync(new ModelRecord(
            "model-1",
            "Test Model",
            Path.Combine(root, "models", "test.gguf"),
            OwnershipKind.External,
            "{}",
            now));

        await store.SaveModelLaunchSettingsAsync("model-1", ModelLaunchSettings.FromAppSettings(AppSettings.CreateDefault(root) with
        {
            ContextSize = 0,
            GpuLayers = 0,
            BatchSize = 2048,
            CacheTypeK = "f16",
            CacheTypeV = "f16",
            Temperature = 0.8,
            MicroBatchSize = 256,
            VisionImageMinTokens = 256,
            VisionImageMaxTokens = 1024
        }));

        var migrated = await store.GetModelLaunchSettingsAsync("model-1");

        Assert.NotNull(migrated);
        Assert.Equal(131_072, migrated.ContextSize);
        Assert.Equal(999, migrated.GpuLayers);
        Assert.Equal(4096, migrated.BatchSize);
        Assert.Equal("q8_0", migrated.CacheTypeK);
        Assert.Equal("q8_0", migrated.CacheTypeV);
        Assert.Equal(0.65, migrated.Temperature);
        Assert.Equal(256, migrated.MicroBatchSize);
        Assert.Equal(256, migrated.VisionImageMinTokens);
        Assert.Equal(1024, migrated.VisionImageMaxTokens);
    }


    [Fact]
    public async Task StateStoreProtectsModelApiKeyAtRest()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var apiKey = new string('a', 64);
        var settings = AppSettings.CreateDefault(root) with { ModelApiKey = apiKey };

        await store.SaveAppSettingsAsync(settings);
        var rawSettings = await store.ListSettingsAsync();
        var loaded = await store.GetAppSettingsAsync(root);

        Assert.Equal(apiKey, loaded.ModelApiKey);
        Assert.True(rawSettings.TryGetValue("modelApiKey", out var rawApiKey));
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("dpapi:v1:", rawApiKey, StringComparison.Ordinal);
            Assert.DoesNotContain(apiKey, rawApiKey, StringComparison.Ordinal);
        }
    }


    [Fact]
    public void WorkspaceRootDefaultsToDataFolderBesideExecutable()
    {
        var root = CreateTempRoot();
        var executable = Path.Combine(root, "LlamaCppConsole.exe");
        var localAppData = Path.Combine(root, "localappdata");

        var workspace = WorkspaceRootResolver.Resolve(null, executable, localAppData);

        Assert.Equal(Path.Combine(root, "data"), workspace, ignoreCase: true);
        Assert.True(Directory.Exists(workspace));
    }


    [Fact]
    public void WorkspaceRootEnvironmentOverrideWins()
    {
        var root = CreateTempRoot();
        var executable = Path.Combine(root, "LlamaCppConsole.exe");
        var overrideRoot = Path.Combine(root, "custom-workspace");

        var workspace = WorkspaceRootResolver.Resolve(overrideRoot, executable, Path.Combine(root, "localappdata"));

        Assert.Equal(Path.GetFullPath(overrideRoot), workspace, ignoreCase: true);
    }


    [Fact]
    public void WorkspaceRootFallbackUsesNewNameButKeepsLegacyFolder()
    {
        var root = CreateTempRoot();
        var localAppData = Path.Combine(root, "localappdata");
        var freshWorkspace = WorkspaceRootResolver.Resolve(null, null, localAppData);

        Assert.Equal(Path.Combine(localAppData, "llama.cpp Console"), freshWorkspace, ignoreCase: true);
        Assert.Equal("LLAMA_CPP_CONSOLE_WORKSPACE", WorkspaceRootResolver.EnvironmentVariable);
        Assert.Equal("LOCAL_LLM_CONSOLE_WORKSPACE", WorkspaceRootResolver.LegacyEnvironmentVariable);

        var legacyWorkspace = Path.Combine(localAppData, "LocalLlmConsole");
        Directory.CreateDirectory(legacyWorkspace);

        var reusedWorkspace = WorkspaceRootResolver.Resolve(null, null, localAppData);

        Assert.Equal(legacyWorkspace, reusedWorkspace, ignoreCase: true);
    }


    [Fact]
    public async Task StateStoreHandlesSemicolonInDatabasePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "LocalLlmConsole.Tests", $"semi;colon-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "state", "local;llm;console.db");
        await using var store = new StateStore(databasePath);

        await store.InitializeAsync();
        await store.UpsertJobAsync(new JobRecord(
            "job",
            "test",
            JobStatus.Completed,
            "{}",
            Path.Combine(root, "logs", "job.log"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));

        Assert.True(File.Exists(databasePath));
        Assert.Single(await store.ListJobsAsync());
    }


    [Fact]
    public void CacheMaintenanceServiceClearsOnlyWorkspaceCache()
    {
        var root = CreateTempRoot();
        var cacheRoot = Path.Combine(root, "cache");
        var nested = Path.Combine(cacheRoot, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(cacheRoot, "a.bin"), "1234");
        File.WriteAllText(Path.Combine(nested, "b.bin"), "123456");
        var external = Path.Combine(Path.GetTempPath(), $"llm-console-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(external);

        try
        {
            Assert.True(CacheMaintenanceService.IsSafeCacheRoot(root, cacheRoot));
            Assert.False(CacheMaintenanceService.IsSafeCacheRoot(root, external));
            Assert.Equal(10, CacheMaintenanceService.Size(cacheRoot));

            CacheMaintenanceService.ClearSafeCacheRoot(root, cacheRoot);

            Assert.True(Directory.Exists(cacheRoot));
            Assert.Empty(Directory.EnumerateFileSystemEntries(cacheRoot));
            Assert.Throws<InvalidOperationException>(() => CacheMaintenanceService.ClearSafeCacheRoot(root, external));
        }
        finally
        {
            if (Directory.Exists(external))
                Directory.Delete(external, recursive: true);
        }
    }

}
