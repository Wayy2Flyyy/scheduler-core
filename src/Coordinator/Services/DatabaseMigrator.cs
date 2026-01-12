using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Coordinator.Services;

public sealed class DatabaseMigrator
{
    private readonly SchedulerDbContext _dbContext;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(SchedulerDbContext dbContext, ILogger<DatabaseMigrator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken)
    {
        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    version TEXT PRIMARY KEY,
                    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var migrationsPath = Path.Combine(AppContext.BaseDirectory, "migrations");
        if (!Directory.Exists(migrationsPath))
        {
            _logger.LogWarning("Migrations folder not found at {Path}", migrationsPath);
            return;
        }

        var files = Directory.GetFiles(migrationsPath, "*.sql")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            var version = Path.GetFileName(file);
            if (await MigrationAppliedAsync(connection, version, cancellationToken))
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(file, cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO schema_migrations (version) VALUES (@version)";
            var param = insert.CreateParameter();
            param.ParameterName = "@version";
            param.Value = version;
            insert.Parameters.Add(param);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Applied migration {Version}", version);
        }
    }

    private static async Task<bool> MigrationAppliedAsync(DbConnection connection, string version, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM schema_migrations WHERE version = @version";
        var param = cmd.CreateParameter();
        param.ParameterName = "@version";
        param.Value = version;
        cmd.Parameters.Add(param);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }
}
