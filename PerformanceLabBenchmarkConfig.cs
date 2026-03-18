using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class PerformanceLabBenchmarkConfig
{
    public const string MethodologyVersion = "2026-03-production-ready-1";

    public static IConfig Create(PerformanceLabCommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var job = Job.Default
            .WithLaunchCount(1)
            .WithWarmupCount(options.Quick ? 1 : 3)
            .WithIterationCount(options.Quick ? 3 : 10);

        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job)
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddExporter(JsonExporter.Full)
            .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest))
            .WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));

        config.ArtifactsPath = PerformanceLabRuntimeOptions.BenchmarkArtifactsPath;
        return config;
    }
}
