namespace LocalLlmConsole.Services;

public sealed partial class StateStore
{
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
