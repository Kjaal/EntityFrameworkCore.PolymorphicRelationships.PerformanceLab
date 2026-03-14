using Npgsql;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class PostgresDatabaseManager
{
    public static async Task RecreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(PostgresOptions.CreateMaintenanceConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using (var dropCommand = connection.CreateCommand())
        {
            dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);";
            await dropCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\";";
            await createCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static async Task DropDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(PostgresOptions.CreateMaintenanceConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
