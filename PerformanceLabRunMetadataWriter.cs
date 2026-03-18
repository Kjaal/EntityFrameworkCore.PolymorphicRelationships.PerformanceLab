using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using EntityFrameworkCore.PolymorphicRelationships;
using Npgsql;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class PerformanceLabRunMetadataWriter
{
    public static async Task<PerformanceLabRunMetadataWriteResult> WriteAsync(
        PerformanceLabCommandLineOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var performanceLabRoot = ResolvePerformanceLabRoot();
        var libraryRoot = ResolveLibraryRoot(performanceLabRoot);

        var metadata = new PerformanceLabRunMetadata(
            DateTimeOffset.UtcNow,
            Environment.OSVersion.VersionString,
            Environment.Version.ToString(),
            options.RawArgs,
            options.BenchmarkArgs,
            options.Smoke,
            options.Quick,
            options.UpdateHistory,
            options.HistoryLabel,
            PerformanceLabBenchmarkConfig.MethodologyVersion,
            PostgresOptions.GetConfigurationMessage(),
            PostgresOptions.GetConnectionTargetDescription(),
            await TryGetPostgresServerVersionAsync(cancellationToken),
            GetDatasetParameters(),
            GetPackageMetadata(libraryRoot),
            GetGitMetadata(performanceLabRoot),
            GetGitMetadata(libraryRoot));

        var artifactsDirectory = Path.Combine(PerformanceLabRuntimeOptions.ArtifactsRootPath, "run-metadata");
        Directory.CreateDirectory(artifactsDirectory);

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        var latestPath = Path.Combine(artifactsDirectory, "latest-run-metadata.json");
        var timestampedPath = Path.Combine(artifactsDirectory, $"run-metadata-{metadata.GeneratedAtUtc:yyyyMMdd-HHmmss}.json");

        await File.WriteAllTextAsync(latestPath, json, cancellationToken);
        await File.WriteAllTextAsync(timestampedPath, json, cancellationToken);
        Console.WriteLine($"Benchmark run metadata written to {latestPath}");

        return new PerformanceLabRunMetadataWriteResult(latestPath, timestampedPath, metadata);
    }

    private static async Task<string?> TryGetPostgresServerVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(PostgresOptions.CreateMaintenanceConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "show server_version;";
            return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, object[]> GetDatasetParameters()
    {
        var benchmarkType = typeof(PolymorphicRelationshipBenchmarks);
        var benchmarkInstance = Activator.CreateInstance(benchmarkType)
            ?? throw new InvalidOperationException($"Could not create a '{benchmarkType.Name}' instance for metadata discovery.");

        return benchmarkType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => new
            {
                Property = property,
                Params = property.GetCustomAttribute<ParamsAttribute>(),
                ParamsSource = property.GetCustomAttribute<ParamsSourceAttribute>(),
            })
            .Where(item => item.Params is not null || item.ParamsSource is not null)
            .ToDictionary(
                item => item.Property.Name,
                item => ResolveParameterValues(benchmarkType, benchmarkInstance, item.Property, item.Params, item.ParamsSource));
    }

    private static object[] ResolveParameterValues(
        Type benchmarkType,
        object benchmarkInstance,
        PropertyInfo property,
        ParamsAttribute? paramsAttribute,
        ParamsSourceAttribute? paramsSourceAttribute)
    {
        if (paramsAttribute is not null)
        {
            return paramsAttribute.Values?.Cast<object>().ToArray() ?? [];
        }

        if (paramsSourceAttribute is null)
        {
            return [];
        }

        var sourceMember = benchmarkType.GetMember(paramsSourceAttribute.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault();
        return sourceMember switch
        {
            PropertyInfo sourceProperty when sourceProperty.GetValue(benchmarkInstance) is IEnumerable<object> values => values.ToArray(),
            PropertyInfo sourceProperty when sourceProperty.GetValue(benchmarkInstance) is System.Collections.IEnumerable enumerable => enumerable.Cast<object>().ToArray(),
            MethodInfo sourceMethod when sourceMethod.Invoke(benchmarkInstance, null) is IEnumerable<object> values => values.ToArray(),
            MethodInfo sourceMethod when sourceMethod.Invoke(benchmarkInstance, null) is System.Collections.IEnumerable enumerable => enumerable.Cast<object>().ToArray(),
            _ => throw new InvalidOperationException($"Could not resolve parameter source '{paramsSourceAttribute.Name}' for '{benchmarkType.Name}.{property.Name}'."),
        };
    }

    private static string ResolvePerformanceLabRoot()
    {
        return FindDirectoryContaining(
                   AppContext.BaseDirectory,
                   "EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj")
               ?? FindDirectoryContaining(Environment.CurrentDirectory, "EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj")
               ?? Environment.CurrentDirectory;
    }

    private static string ResolveLibraryRoot(string performanceLabRoot)
    {
        var siblingLibraryRoot = Path.GetFullPath(Path.Combine(performanceLabRoot, "..", "EntityFrameworkCore.PolymorphicRelationships"));
        if (File.Exists(Path.Combine(siblingLibraryRoot, "EntityFrameworkCore.PolymorphicRelationships.sln")))
        {
            return siblingLibraryRoot;
        }

        var childLibraryRoot = Path.Combine(performanceLabRoot, "EntityFrameworkCore.PolymorphicRelationships");
        if (File.Exists(Path.Combine(childLibraryRoot, "EntityFrameworkCore.PolymorphicRelationships.sln")))
        {
            return childLibraryRoot;
        }

        return FindDirectoryContaining(performanceLabRoot, "EntityFrameworkCore.PolymorphicRelationships.sln")
               ?? performanceLabRoot;
    }

    private static string? FindDirectoryContaining(string startingDirectory, string fileName)
    {
        var directory = new DirectoryInfo(startingDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, fileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static PackageMetadata GetPackageMetadata(string libraryRoot)
    {
        var assembly = typeof(DbContextOptionsBuilderExtensions).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var assemblyPath = assembly.Location;
        var source = DetectPackageSource(assemblyPath, libraryRoot);

        return new PackageMetadata(
            assembly.GetName().Name ?? "EntityFrameworkCore.PolymorphicRelationships",
            assembly.GetName().Version?.ToString(),
            informationalVersion,
            assemblyPath,
            source);
    }

    private static string DetectPackageSource(string assemblyPath, string libraryRoot)
    {
        if (!string.IsNullOrWhiteSpace(libraryRoot) && assemblyPath.StartsWith(libraryRoot, StringComparison.OrdinalIgnoreCase))
        {
            return "local-project";
        }

        if (assemblyPath.Contains(".nuget", StringComparison.OrdinalIgnoreCase)
            || assemblyPath.Contains("packages", StringComparison.OrdinalIgnoreCase))
        {
            return "package-reference";
        }

        return "unknown";
    }

    private static GitMetadata GetGitMetadata(string repositoryPath)
    {
        return new GitMetadata(
            repositoryPath,
            RunGit(repositoryPath, "rev-parse HEAD"),
            !string.IsNullOrWhiteSpace(RunGit(repositoryPath, "status --short")),
            RunGit(repositoryPath, "branch --show-current"));
    }

    private static string? RunGit(string workdir, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workdir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    internal sealed record PerformanceLabRunMetadataWriteResult(
        string LatestMetadataPath,
        string TimestampedMetadataPath,
        PerformanceLabRunMetadata Metadata);

    internal sealed record PerformanceLabRunMetadata(
        DateTimeOffset GeneratedAtUtc,
        string OsVersion,
        string DotNetVersion,
        IReadOnlyList<string> Arguments,
        IReadOnlyList<string> BenchmarkArgs,
        bool SmokeMode,
        bool QuickMode,
        bool UpdateHistoryRequested,
        string? HistoryLabel,
        string MethodologyVersion,
        string ConfigurationSources,
        string ConnectionTarget,
        string? PostgresServerVersion,
        IReadOnlyDictionary<string, object[]> DatasetParameters,
        PackageMetadata Package,
        GitMetadata PerformanceLabRepository,
        GitMetadata LibraryRepository);

    internal sealed record PackageMetadata(
        string Name,
        string? AssemblyVersion,
        string? InformationalVersion,
        string AssemblyPath,
        string Source);

    internal sealed record GitMetadata(string RepositoryPath, string? Commit, bool IsDirty, string? Branch);
}
