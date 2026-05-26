namespace LocalLlmConsole.Services;

public sealed partial class StateStore
{
    private sealed record SchemaMigration(int Id, string Name, string Sql);

    private static readonly SchemaMigration[] SchemaMigrations =
    [
        new(1, "baseline-v1", "")
    ];

    private async Task ApplyMigrationsUnlockedAsync()
    {
        var applied = new HashSet<int>();
        await using (var list = _connection.CreateCommand())
        {
            list.CommandText = "SELECT id FROM migrations;";
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                applied.Add(reader.GetInt32(0));
        }

        foreach (var migration in SchemaMigrations.OrderBy(migration => migration.Id))
        {
            if (applied.Contains(migration.Id)) continue;
            await using var transaction = await _connection.BeginTransactionAsync();
            try
            {
                if (!string.IsNullOrWhiteSpace(migration.Sql))
                {
                    await using var migrate = _connection.CreateCommand();
                    migrate.Transaction = (SqliteTransaction)transaction;
                    migrate.CommandText = migration.Sql;
                    await migrate.ExecuteNonQueryAsync();
                }

                await using var mark = _connection.CreateCommand();
                mark.Transaction = (SqliteTransaction)transaction;
                mark.CommandText = """
INSERT INTO migrations (id, name, applied_at)
VALUES ($id, $name, $applied_at);
""";
                mark.Parameters.AddWithValue("$id", migration.Id);
                mark.Parameters.AddWithValue("$name", migration.Name);
                mark.Parameters.AddWithValue("$applied_at", DateTimeOffset.UtcNow.ToString("O"));
                await mark.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
