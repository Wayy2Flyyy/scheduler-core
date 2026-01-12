using Npgsql;

namespace Coordinator.Tests;

public static class TestDatabaseHelper
{
    public static async Task ApplyMigrationsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version TEXT PRIMARY KEY,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );", connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "migrations");
        if (!Directory.Exists(migrationsPath))
        {
            migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "migrations");
        }

        var files = Directory.GetFiles(migrationsPath, "*.sql")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            var version = Path.GetFileName(file);
            await using var check = new NpgsqlCommand("SELECT 1 FROM schema_migrations WHERE version = @version", connection);
            check.Parameters.AddWithValue("version", version);
            var exists = await check.ExecuteScalarAsync();
            if (exists is not null)
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(file);
            await using var cmd = new NpgsqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();

            await using var insert = new NpgsqlCommand("INSERT INTO schema_migrations (version) VALUES (@version)", connection);
            insert.Parameters.AddWithValue("version", version);
            await insert.ExecuteNonQueryAsync();
        }
    }
}
