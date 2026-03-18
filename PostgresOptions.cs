using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class PostgresOptions
{
    private const string ConnectionStringEnvironmentVariable = "POLYMORPHIC_PERF_POSTGRES";
    private const string AllowRemoteHostEnvironmentVariable = "POLYMORPHIC_PERF_ALLOW_REMOTE_HOST";
    private const string AppSettingsConnectionStringKey = "ConnectionStrings:Postgres";

    public static string CreateDatabaseConnectionString(string databaseName)
    {
        var builder = CreateValidatedBaseBuilder();
        builder.Database = databaseName;
        return builder.ConnectionString;
    }

    public static string CreateMaintenanceConnectionString()
    {
        var builder = CreateValidatedBaseBuilder();
        builder.Database = "postgres";
        return builder.ConnectionString;
    }

    public static string GetConfigurationMessage()
    {
        return $"Configuration sources: {ConnectionStringEnvironmentVariable}, appsettings.json ({AppSettingsConnectionStringKey}). Explicit PostgreSQL configuration is required. Remote hosts are blocked unless {AllowRemoteHostEnvironmentVariable}=true.";
    }

    public static string GetConnectionTargetDescription()
    {
        var builder = CreateValidatedBaseBuilder();
        return $"Host={builder.Host};Port={builder.Port};Database={builder.Database};Username={builder.Username}";
    }

    private static NpgsqlConnectionStringBuilder CreateValidatedBaseBuilder()
    {
        var baseConnectionString = ResolveBaseConnectionString();
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        ValidateHostSafety(builder);
        return builder;
    }

    private static string ResolveBaseConnectionString()
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

        if (!string.IsNullOrWhiteSpace(appSettingsConnectionString))
        {
            return appSettingsConnectionString;
        }

        throw new InvalidOperationException(
            $"PostgreSQL configuration is required for the performance lab. Set {ConnectionStringEnvironmentVariable} or provide appsettings.json based on appsettings.example.json.");
    }

    private static void ValidateHostSafety(NpgsqlConnectionStringBuilder builder)
    {
        if (IsRemoteHostAllowed())
        {
            return;
        }

        var hosts = (builder.Host ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0)
        {
            throw new InvalidOperationException("The configured PostgreSQL connection string must include a host.");
        }

        var disallowedHosts = hosts
            .Where(host => !IsLocalHost(host))
            .ToArray();

        if (disallowedHosts.Length > 0)
        {
            throw new InvalidOperationException(
                $"The performance lab only allows localhost PostgreSQL targets by default. Configure {AllowRemoteHostEnvironmentVariable}=true to opt into remote hosts. Rejected host(s): {string.Join(", ", disallowedHosts)}.");
        }
    }

    private static bool IsRemoteHostAllowed()
    {
        var value = Environment.GetEnvironmentVariable(AllowRemoteHostEnvironmentVariable);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
    }
}
