using Npgsql;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class PostgresDatabaseManager
{
    private const string BenchmarkDatabasePrefix = "polymorphic_perf_";

    public static async Task RecreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        ValidateBenchmarkDatabaseName(databaseName);

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
        ValidateBenchmarkDatabaseName(databaseName);

        await using var connection = new NpgsqlConnection(PostgresOptions.CreateMaintenanceConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateBenchmarkDatabaseName(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        if (!databaseName.StartsWith(BenchmarkDatabasePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The performance lab may only create or drop databases starting with '{BenchmarkDatabasePrefix}'. Received '{databaseName}'.");
        }

        if (databaseName.Any(character => !(char.IsLetterOrDigit(character) || character == '_')))
        {
            throw new InvalidOperationException(
                $"The performance lab database name '{databaseName}' contains unsupported characters.");
        }
    }
}
