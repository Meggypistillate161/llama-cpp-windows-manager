using System.Data;
using System.Threading;

namespace LocalLlmConsole.Services;

public sealed partial class StateStore : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _disposed;

    public string DatabasePath { get; }

    public StateStore(string databasePath)
    {
        DatabasePath = databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString());
    }

    public static string QuarantineDatabaseFiles(string databasePath)
    {
        var stateRoot = Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory;
        var backupRoot = Path.Combine(stateRoot, $"corrupt-database-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(backupRoot);
        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" }.Where(File.Exists))
        {
            var backupPath = Path.Combine(backupRoot, Path.GetFileName(path));
            File.Move(path, backupPath, overwrite: true);
        }

        return backupRoot;
    }

    public async Task InitializeAsync()
    {
        await WithConnectionAsync(async () =>
        {
            await ExecuteUnlockedAsync("PRAGMA journal_mode=WAL;");
            await ExecuteUnlockedAsync("PRAGMA foreign_keys=ON;");
            await ExecuteUnlockedAsync("PRAGMA busy_timeout=5000;");
            await ExecuteUnlockedAsync("""
CREATE TABLE IF NOT EXISTS migrations (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  applied_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS settings (
  key TEXT PRIMARY KEY,
  value_json TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS models (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  model_path TEXT NOT NULL,
  ownership TEXT NOT NULL CHECK (ownership IN ('AppOwned','External','RegistryOnly')),
  metadata_json TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS model_launch_settings (
  model_id TEXT PRIMARY KEY,
  settings_json TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  FOREIGN KEY(model_id) REFERENCES models(id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS runtimes (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  mode TEXT NOT NULL,
  backend TEXT NOT NULL,
  executable_path TEXT NOT NULL,
  metadata_json TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS jobs (
  id TEXT PRIMARY KEY,
  kind TEXT NOT NULL,
  status TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  log_path TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS downloads (
  id TEXT PRIMARY KEY,
  job_id TEXT NOT NULL,
  source_url TEXT NOT NULL,
  destination_path TEXT NOT NULL,
  expected_bytes INTEGER,
  downloaded_bytes INTEGER NOT NULL DEFAULT 0,
  checksum TEXT,
  etag TEXT,
  updated_at TEXT NOT NULL,
  FOREIGN KEY(job_id) REFERENCES jobs(id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS ownership_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  target_path TEXT NOT NULL,
  ownership TEXT NOT NULL,
  action TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS history (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  category TEXT NOT NULL,
  message TEXT NOT NULL,
  data_json TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS token_usage (
  model_id TEXT PRIMARY KEY,
  model_name TEXT NOT NULL,
  prompt_tokens INTEGER NOT NULL DEFAULT 0,
  generated_tokens INTEGER NOT NULL DEFAULT 0,
  updated_at TEXT NOT NULL
);
""");
            await ApplyMigrationsUnlockedAsync();
        });
    }

    private async Task ExecuteUnlockedAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task WithConnectionAsync(Func<Task> action)
    {
        await _dbLock.WaitAsync();
        try
        {
            ThrowIfDisposed();
            await EnsureOpenAsync();
            await action();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<T> WithConnectionAsync<T>(Func<Task<T>> action)
    {
        await _dbLock.WaitAsync();
        try
        {
            ThrowIfDisposed();
            await EnsureOpenAsync();
            return await action();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task EnsureOpenAsync()
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StateStore));
    }

    private async Task BackupCorruptSettingsAsync(Dictionary<string, string> corrupt)
    {
        try
        {
            var backupRoot = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "corrupt-settings");
            Directory.CreateDirectory(backupRoot);
            var backupPath = Path.Combine(backupRoot, $"settings-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
            await File.WriteAllTextAsync(backupPath, JsonSerializer.Serialize(new
            {
                backedUpAt = DateTimeOffset.UtcNow,
                databasePath = DatabasePath,
                settings = corrupt
            }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static bool IsStoredIntValue(IReadOnlyDictionary<string, string> values, string key, int expected)
        => values.TryGetValue(key, out var json)
            && TryReadJsonNumber(json, out var value)
            && value == expected;

    private static bool IsStoredDoubleValue(IReadOnlyDictionary<string, string> values, string key, double expected)
        => values.TryGetValue(key, out var json)
            && TryReadJsonDouble(json, out var value)
            && Math.Abs(value - expected) < 0.000_001;

    private static bool IsStoredStringValue(IReadOnlyDictionary<string, string> values, string key, string expected)
        => values.TryGetValue(key, out var json)
            && TryReadJsonString(json, out var value)
            && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static ModelLaunchSettings MigrateLegacyModelLaunchDefaults(ModelLaunchSettings settings, out bool changed)
    {
        changed = false;
        var migrated = settings;
        if (settings.ContextSize == 0)
        {
            migrated = migrated with { ContextSize = AppSettings.DefaultContextSize };
            changed = true;
        }
        if (settings.GpuLayers == 0)
        {
            migrated = migrated with { GpuLayers = AppSettings.DefaultGpuLayers };
            changed = true;
        }
        if (settings.BatchSize == 2048)
        {
            migrated = migrated with { BatchSize = AppSettings.DefaultBatchSize };
            changed = true;
        }
        if (string.Equals(settings.CacheTypeK, "f16", StringComparison.OrdinalIgnoreCase))
        {
            migrated = migrated with { CacheTypeK = AppSettings.DefaultCacheType };
            changed = true;
        }
        if (string.Equals(settings.CacheTypeV, "f16", StringComparison.OrdinalIgnoreCase))
        {
            migrated = migrated with { CacheTypeV = AppSettings.DefaultCacheType };
            changed = true;
        }
        if (Math.Abs(settings.Temperature - 0.8) < 0.000_001)
        {
            migrated = migrated with { Temperature = AppSettings.DefaultTemperature };
            changed = true;
        }
        return migrated;
    }

    private static bool TryReadJsonString(string json, out string value)
    {
        value = "";
        try
        {
            value = JsonSerializer.Deserialize<string>(json) ?? "";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadJsonNumber(string json, out long value)
    {
        value = 0;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return false;
            if (node.GetValueKind() == JsonValueKind.Number && node.GetValue<long>() is var number)
            {
                value = number;
                return true;
            }
            if (node.GetValueKind() == JsonValueKind.String && long.TryParse(node.GetValue<string>(), out number))
            {
                value = number;
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool TryReadJsonDouble(string json, out double value)
    {
        value = 0;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return false;
            if (node.GetValueKind() == JsonValueKind.Number && node.GetValue<double>() is var number)
            {
                value = number;
                return true;
            }
            if (node.GetValueKind() == JsonValueKind.String
                && double.TryParse(node.GetValue<string>(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number))
            {
                value = number;
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool TryReadJsonBool(string json, out bool value)
    {
        value = false;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return false;
            if (node.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
            {
                value = node.GetValue<bool>();
                return true;
            }
            if (node.GetValueKind() == JsonValueKind.String && bool.TryParse(node.GetValue<string>(), out var parsed))
            {
                value = parsed;
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static TEnum EnumValue<TEnum>(string value, TEnum fallback)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static DateTimeOffset DateValue(string value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UtcNow;

    public async ValueTask DisposeAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            await _connection.DisposeAsync();
        }
        finally
        {
            _dbLock.Release();
            _dbLock.Dispose();
        }
    }
}
