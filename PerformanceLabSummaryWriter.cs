using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Reports;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class PerformanceLabSummaryWriter
{
    public static async Task WriteAsync(
        IReadOnlyCollection<Summary> summaries,
        PerformanceLabCommandLineOptions options,
        PerformanceLabRunMetadataWriter.PerformanceLabRunMetadataWriteResult metadataResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metadataResult);

        var benchmarkSummaries = summaries
            .SelectMany(summary => summary.Reports)
            .Where(report => report.ResultStatistics is not null)
            .Select(CreateBenchmarkSummary)
            .OrderBy(summary => summary.MeanMs)
            .ToList();

        if (benchmarkSummaries.Count == 0)
        {
            return;
        }

        var document = new PerformanceLabSummaryDocument(
            metadataResult.Metadata.GeneratedAtUtc,
            options.Quick,
            PerformanceLabBenchmarkConfig.MethodologyVersion,
            metadataResult.Metadata.ConnectionTarget,
            metadataResult.Metadata.PostgresServerVersion,
            metadataResult.Metadata.Package,
            metadataResult.LatestMetadataPath,
            metadataResult.TimestampedMetadataPath,
            metadataResult.Metadata.BenchmarkArgs,
            metadataResult.Metadata.DatasetParameters,
            benchmarkSummaries);

        var summariesDirectory = Path.Combine(PerformanceLabRuntimeOptions.ArtifactsRootPath, "summaries");
        Directory.CreateDirectory(summariesDirectory);

        var timestamp = metadataResult.Metadata.GeneratedAtUtc.ToString("yyyyMMdd-HHmmss");
        var latestJsonPath = Path.Combine(summariesDirectory, "latest-summary.json");
        var timestampedJsonPath = Path.Combine(summariesDirectory, $"summary-{timestamp}.json");
        var latestMarkdownPath = Path.Combine(summariesDirectory, "latest-summary.md");
        var timestampedMarkdownPath = Path.Combine(summariesDirectory, $"summary-{timestamp}.md");

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        var markdown = BuildMarkdown(document);

        await File.WriteAllTextAsync(latestJsonPath, json, cancellationToken);
        await File.WriteAllTextAsync(timestampedJsonPath, json, cancellationToken);
        await File.WriteAllTextAsync(latestMarkdownPath, markdown, cancellationToken);
        await File.WriteAllTextAsync(timestampedMarkdownPath, markdown, cancellationToken);

        if (options.UpdateHistory)
        {
            await WriteHistoryAsync(document, json, markdown, options, cancellationToken);
        }
    }

    private static BenchmarkSummaryEntry CreateBenchmarkSummary(BenchmarkReport report)
    {
        var statistics = report.ResultStatistics
            ?? throw new InvalidOperationException($"Benchmark report '{report.BenchmarkCase.Descriptor.WorkloadMethod.Name}' does not contain result statistics.");

        var parameterMap = report.BenchmarkCase.Parameters.Items.ToDictionary(
            item => item.Name,
            item => Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

        return new BenchmarkSummaryEntry(
            report.BenchmarkCase.Descriptor.WorkloadMethod.Name,
            report.BenchmarkCase.Descriptor.Categories.OrderBy(category => category, StringComparer.Ordinal).ToArray(),
            parameterMap,
            Math.Round(statistics.Mean / 1_000_000d, 3),
            Math.Round(statistics.StandardDeviation / 1_000_000d, 3),
            Math.Round(GetAllocatedKilobytes(report), 2));
    }

    private static double GetAllocatedKilobytes(BenchmarkReport report)
    {
        var gcStats = report.GcStats;
        var property = gcStats.GetType().GetProperty("AllocatedBytes");
        if (property?.GetValue(gcStats) is long allocatedBytes)
        {
            return allocatedBytes / 1024d;
        }

        return 0d;
    }

    private static string BuildMarkdown(PerformanceLabSummaryDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Performance Summary");
        builder.AppendLine();
        builder.AppendLine($"Generated: {document.GeneratedAtUtc:O}");
        builder.AppendLine();
        builder.AppendLine($"- Methodology version: `{document.MethodologyVersion}`");
        builder.AppendLine($"- Advisory: `{document.Advisory}`");
        builder.AppendLine($"- Connection target: `{document.ConnectionTarget}`");
        builder.AppendLine($"- PostgreSQL version: `{document.PostgresServerVersion ?? "unknown"}`");
        builder.AppendLine($"- Package source: `{document.Package.Source}`");
        builder.AppendLine($"- Package version: `{document.Package.InformationalVersion ?? document.Package.AssemblyVersion ?? "unknown"}`");
        builder.AppendLine($"- Run metadata: `{document.LatestRunMetadataPath}`");
        builder.AppendLine();
        builder.AppendLine("## Dataset");
        builder.AppendLine();

        foreach (var item in document.DatasetParameters.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{item.Key}`: `{string.Join(", ", item.Value.Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty))}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Benchmarks");
        builder.AppendLine();
        builder.AppendLine("| Method | Categories | Mean (ms) | StdDev (ms) | Allocated (KB) |");
        builder.AppendLine("| --- | --- | ---: | ---: | ---: |");

        foreach (var benchmark in document.Benchmarks)
        {
            builder.AppendLine($"| `{benchmark.Method}` | `{string.Join(", ", benchmark.Categories)}` | {benchmark.MeanMs:F3} | {benchmark.StdDevMs:F3} | {benchmark.AllocatedKb:F2} |");
        }

        return builder.ToString();
    }

    private static async Task WriteHistoryAsync(
        PerformanceLabSummaryDocument document,
        string json,
        string markdown,
        PerformanceLabCommandLineOptions options,
        CancellationToken cancellationToken)
    {
        var historyDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "BenchmarkHistory");
        historyDirectory = Path.GetFullPath(historyDirectory);
        Directory.CreateDirectory(historyDirectory);

        var label = SanitizeFileLabel(options.HistoryLabel);
        var stamp = document.GeneratedAtUtc.ToString("yyyy-MM-dd");
        var latestSummaryPath = Path.Combine(historyDirectory, "latest-summary.json");
        var datedMarkdownPath = Path.Combine(historyDirectory, string.IsNullOrWhiteSpace(label)
            ? $"{stamp}.md"
            : $"{stamp}-{label}.md");

        await File.WriteAllTextAsync(latestSummaryPath, json, cancellationToken);
        await File.WriteAllTextAsync(datedMarkdownPath, markdown, cancellationToken);
    }

    private static string? SanitizeFileLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var sanitized = new string(label
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private sealed record PerformanceLabSummaryDocument(
        DateTimeOffset GeneratedAtUtc,
        bool Advisory,
        string MethodologyVersion,
        string ConnectionTarget,
        string? PostgresServerVersion,
        PerformanceLabRunMetadataWriter.PackageMetadata Package,
        string LatestRunMetadataPath,
        string TimestampedRunMetadataPath,
        IReadOnlyList<string> BenchmarkArgs,
        IReadOnlyDictionary<string, object[]> DatasetParameters,
        IReadOnlyList<BenchmarkSummaryEntry> Benchmarks);

    private sealed record BenchmarkSummaryEntry(
        string Method,
        IReadOnlyList<string> Categories,
        IReadOnlyDictionary<string, string> Parameters,
        double MeanMs,
        double StdDevMs,
        double AllocatedKb);
}
