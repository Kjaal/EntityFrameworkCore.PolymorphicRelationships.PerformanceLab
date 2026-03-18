namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class PerformanceLabRuntimeOptions
{
    public static bool QuickMode { get; private set; }

    public static string RunStamp { get; private set; } = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");

    public static string ArtifactsRootPath => Path.Combine(AppContext.BaseDirectory, "BenchmarkDotNet.Artifacts");

    public static string BenchmarkArtifactsPath => Path.Combine(ArtifactsRootPath, "runs", RunStamp);

    public static int OwnerSampleSize => QuickMode ? 25 : 100;

    public static int CommentSampleSize => QuickMode ? 120 : 1000;

    public static IReadOnlyList<int> OwnerCountPerTypeValues => QuickMode ? [250] : [1000];

    public static IReadOnlyList<int> CommentsPerOwnerValues => QuickMode ? [8] : [20];

    public static void Configure(PerformanceLabCommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        QuickMode = options.Quick;
        RunStamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
    }
}
