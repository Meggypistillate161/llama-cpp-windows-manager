namespace LocalLlmConsole.Services;

public sealed partial class StateStore
{
    public async Task UpsertModelAsync(ModelRecord model)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
INSERT INTO models (id, name, model_path, ownership, metadata_json, updated_at)
VALUES ($id, $name, $model_path, $ownership, $metadata_json, $updated_at)
ON CONFLICT(id) DO UPDATE SET
  name = excluded.name,
  model_path = excluded.model_path,
  ownership = excluded.ownership,
  metadata_json = excluded.metadata_json,
  updated_at = excluded.updated_at;
""";
            command.Parameters.AddWithValue("$id", model.Id);
            command.Parameters.AddWithValue("$name", model.Name);
            command.Parameters.AddWithValue("$model_path", model.ModelPath);
            command.Parameters.AddWithValue("$ownership", model.Ownership.ToString());
            command.Parameters.AddWithValue("$metadata_json", model.MetadataJson);
            command.Parameters.AddWithValue("$updated_at", model.UpdatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task<IReadOnlyList<ModelRecord>> ListModelsAsync()
    {
        return await WithConnectionAsync<IReadOnlyList<ModelRecord>>(async () =>
        {
            var models = new List<ModelRecord>();
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT id, name, model_path, ownership, metadata_json, updated_at FROM models ORDER BY name;";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                models.Add(new ModelRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    EnumValue(reader.GetString(3), OwnershipKind.External),
                    reader.GetString(4),
                    DateValue(reader.GetString(5))));
            }
            return models;
        });
    }

    public async Task DeleteModelAsync(string id)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM models WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task<ModelLaunchSettings?> GetModelLaunchSettingsAsync(string modelId)
    {
        return await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT settings_json FROM model_launch_settings WHERE model_id = $model_id;";
            command.Parameters.AddWithValue("$model_id", modelId);
            var json = await command.ExecuteScalarAsync() as string;
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var settings = JsonSerializer.Deserialize<ModelLaunchSettings>(json);
                if (settings is null) return null;
                var migrated = MigrateLegacyModelLaunchDefaults(settings, out var changed);
                if (changed)
                {
                    await using var update = _connection.CreateCommand();
                    update.CommandText = """
UPDATE model_launch_settings
SET settings_json = $settings_json,
    updated_at = $updated_at
WHERE model_id = $model_id;
""";
                    update.Parameters.AddWithValue("$model_id", modelId);
                    update.Parameters.AddWithValue("$settings_json", JsonSerializer.Serialize(migrated));
                    update.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
                    await update.ExecuteNonQueryAsync();
                }
                return migrated;
            }
            catch { return null; }
        });
    }

    public async Task SaveModelLaunchSettingsAsync(string modelId, ModelLaunchSettings settings)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
INSERT INTO model_launch_settings (model_id, settings_json, updated_at)
VALUES ($model_id, $settings_json, $updated_at)
ON CONFLICT(model_id) DO UPDATE SET
  settings_json = excluded.settings_json,
  updated_at = excluded.updated_at;
""";
            command.Parameters.AddWithValue("$model_id", modelId);
            command.Parameters.AddWithValue("$settings_json", JsonSerializer.Serialize(settings));
            command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task DeleteModelLaunchSettingsAsync(string modelId)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM model_launch_settings WHERE model_id = $model_id;";
            command.Parameters.AddWithValue("$model_id", modelId);
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task AddTokenUsageAsync(string modelId, string modelName, long promptTokens, long generatedTokens)
    {
        if (promptTokens <= 0 && generatedTokens <= 0) return;
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
INSERT INTO token_usage (model_id, model_name, prompt_tokens, generated_tokens, updated_at)
VALUES ($model_id, $model_name, $prompt_tokens, $generated_tokens, $updated_at)
ON CONFLICT(model_id) DO UPDATE SET
  model_name = excluded.model_name,
  prompt_tokens = token_usage.prompt_tokens + excluded.prompt_tokens,
  generated_tokens = token_usage.generated_tokens + excluded.generated_tokens,
  updated_at = excluded.updated_at;
""";
            command.Parameters.AddWithValue("$model_id", modelId);
            command.Parameters.AddWithValue("$model_name", string.IsNullOrWhiteSpace(modelName) ? modelId : modelName);
            command.Parameters.AddWithValue("$prompt_tokens", promptTokens);
            command.Parameters.AddWithValue("$generated_tokens", generatedTokens);
            command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task<IReadOnlyList<TokenUsageRecord>> ListTokenUsageAsync()
    {
        return await WithConnectionAsync<IReadOnlyList<TokenUsageRecord>>(async () =>
        {
            var rows = new List<TokenUsageRecord>();
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT model_id, model_name, prompt_tokens, generated_tokens, updated_at FROM token_usage ORDER BY generated_tokens + prompt_tokens DESC, model_name;";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new TokenUsageRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3),
                    DateValue(reader.GetString(4))));
            }
            return rows;
        });
    }

    public async Task DeleteTokenUsageAsync(string modelId)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM token_usage WHERE model_id = $model_id;";
            command.Parameters.AddWithValue("$model_id", modelId);
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task DeleteAllTokenUsageAsync()
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM token_usage;";
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task UpsertRuntimeAsync(RuntimeRecord runtime)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
INSERT INTO runtimes (id, name, mode, backend, executable_path, metadata_json, updated_at)
VALUES ($id, $name, $mode, $backend, $executable_path, $metadata_json, $updated_at)
ON CONFLICT(id) DO UPDATE SET
  name = excluded.name,
  mode = excluded.mode,
  backend = excluded.backend,
  executable_path = excluded.executable_path,
  metadata_json = excluded.metadata_json,
  updated_at = excluded.updated_at;
""";
            command.Parameters.AddWithValue("$id", runtime.Id);
            command.Parameters.AddWithValue("$name", runtime.Name);
            command.Parameters.AddWithValue("$mode", runtime.Mode.ToString());
            command.Parameters.AddWithValue("$backend", runtime.Backend.ToString());
            command.Parameters.AddWithValue("$executable_path", runtime.ExecutablePath);
            command.Parameters.AddWithValue("$metadata_json", runtime.MetadataJson);
            command.Parameters.AddWithValue("$updated_at", runtime.UpdatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task<IReadOnlyList<RuntimeRecord>> ListRuntimesAsync()
    {
        return await WithConnectionAsync<IReadOnlyList<RuntimeRecord>>(async () =>
        {
            var runtimes = new List<RuntimeRecord>();
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT id, name, mode, backend, executable_path, metadata_json, updated_at FROM runtimes ORDER BY name;";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                runtimes.Add(new RuntimeRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    EnumValue(reader.GetString(2), RuntimeMode.Native),
                    EnumValue(reader.GetString(3), RuntimeBackend.Cpu),
                    reader.GetString(4),
                    reader.GetString(5),
                    DateValue(reader.GetString(6))));
            }
            return runtimes;
        });
    }

    public async Task DeleteRuntimeAsync(string id)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM runtimes WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        });
    }
}
