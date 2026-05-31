namespace LocalLlmConsole.Services;

public sealed partial class StateStore
{
    public async Task<JobRecord?> GetJobAsync(string id)
    {
        return await WithConnectionAsync<JobRecord?>(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT id, kind, status, payload_json, log_path, created_at, updated_at FROM jobs WHERE id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", id);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new JobRecord(
                reader.GetString(0),
                reader.GetString(1),
                EnumValue(reader.GetString(2), JobStatus.Failed),
                reader.GetString(3),
                reader.GetString(4),
                DateValue(reader.GetString(5)),
                DateValue(reader.GetString(6)));
        });
    }

    public async Task<IReadOnlyList<JobRecord>> ListJobsAsync()
    {
        return await WithConnectionAsync<IReadOnlyList<JobRecord>>(async () =>
        {
            var jobs = new List<JobRecord>();
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT id, kind, status, payload_json, log_path, created_at, updated_at FROM jobs ORDER BY updated_at DESC;";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                jobs.Add(new JobRecord(
                    reader.GetString(0),
                    reader.GetString(1),
                    EnumValue(reader.GetString(2), JobStatus.Failed),
                    reader.GetString(3),
                    reader.GetString(4),
                    DateValue(reader.GetString(5)),
                    DateValue(reader.GetString(6))));
            }
            return jobs;
        });
    }

    public async Task UpsertJobAsync(JobRecord job)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
INSERT INTO jobs (id, kind, status, payload_json, log_path, created_at, updated_at)
VALUES ($id, $kind, $status, $payload_json, $log_path, $created_at, $updated_at)
ON CONFLICT(id) DO UPDATE SET
  status = excluded.status,
  payload_json = excluded.payload_json,
  log_path = excluded.log_path,
  updated_at = excluded.updated_at;
""";
            command.Parameters.AddWithValue("$id", job.Id);
            command.Parameters.AddWithValue("$kind", job.Kind);
            command.Parameters.AddWithValue("$status", job.Status.ToString());
            command.Parameters.AddWithValue("$payload_json", job.PayloadJson);
            command.Parameters.AddWithValue("$log_path", job.LogPath);
            command.Parameters.AddWithValue("$created_at", job.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$updated_at", job.UpdatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task<bool> TryUpdateJobAsync(JobRecord job, JobStatus expectedStatus)
    {
        return await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = """
UPDATE jobs
SET status = $status,
    payload_json = $payload_json,
    log_path = $log_path,
    updated_at = $updated_at
WHERE id = $id
  AND status = $expected_status;
""";
            command.Parameters.AddWithValue("$id", job.Id);
            command.Parameters.AddWithValue("$status", job.Status.ToString());
            command.Parameters.AddWithValue("$expected_status", expectedStatus.ToString());
            command.Parameters.AddWithValue("$payload_json", job.PayloadJson);
            command.Parameters.AddWithValue("$log_path", job.LogPath);
            command.Parameters.AddWithValue("$updated_at", job.UpdatedAt.ToString("O"));
            return await command.ExecuteNonQueryAsync() == 1;
        });
    }

    public async Task DeleteJobAsync(string id)
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM jobs WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync();
        });
    }

    public async Task MarkInterruptedJobsAsync()
    {
        await WithConnectionAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "UPDATE jobs SET status = 'Interrupted', updated_at = $now WHERE status IN ('Queued','Running');";
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        });
    }
}
