using Npgsql;
using Microsoft.Extensions.Configuration;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class PostgresOptions
{
    private const string ConnectionStringEnvironmentVariable = "POLYMORPHIC_PERF_POSTGRES";
    private const string AppSettingsConnectionStringKey = "ConnectionStrings:Postgres";
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    public static string CreateDatabaseConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(GetBaseConnectionString())
        {
            Database = databaseName,
        };

        return builder.ConnectionString;
    }

    public static string CreateMaintenanceConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(GetBaseConnectionString());
        builder.Database = "postgres";

        return builder.ConnectionString;
    }

    public static string GetConfigurationMessage()
    {
        return $"Configuration sources: {ConnectionStringEnvironmentVariable}, appsettings.json ({AppSettingsConnectionStringKey}), then built-in default '{DefaultConnectionString}'.";
    }

    private static string GetBaseConnectionString()
    {
        var environmentConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            return environmentConnectionString;
        }

        var appSettingsConnectionString = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build()[AppSettingsConnectionStringKey];

        return string.IsNullOrWhiteSpace(appSettingsConnectionString)
            ? DefaultConnectionString
            : appSettingsConnectionString;
    }
}
