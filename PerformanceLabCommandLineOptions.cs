namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal sealed class PerformanceLabCommandLineOptions
{
    public bool Smoke { get; private init; }

    public bool Quick { get; private init; }

    public bool UpdateHistory { get; private init; }

    public string? HistoryLabel { get; private init; }

    public IReadOnlyList<string> RawArgs { get; private init; } = [];

    public IReadOnlyList<string> BenchmarkArgs { get; private init; } = [];

    public static PerformanceLabCommandLineOptions Parse(string[] args)
    {
        var benchmarkArgs = new List<string>();
        var smoke = false;
        var quick = false;
        var updateHistory = false;
        string? historyLabel = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--smoke", StringComparison.OrdinalIgnoreCase))
            {
                smoke = true;
                continue;
            }

            if (string.Equals(argument, "--quick", StringComparison.OrdinalIgnoreCase))
            {
                quick = true;
                continue;
            }

            if (string.Equals(argument, "--update-history", StringComparison.OrdinalIgnoreCase))
            {
                updateHistory = true;
                continue;
            }

            if (argument.StartsWith("--history-label=", StringComparison.OrdinalIgnoreCase))
            {
                historyLabel = argument["--history-label=".Length..];
                continue;
            }

            if (string.Equals(argument, "--history-label", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                historyLabel = args[++index];
                continue;
            }

            benchmarkArgs.Add(argument);
        }

        if (smoke && benchmarkArgs.Count > 0)
        {
            throw new InvalidOperationException("The --smoke option cannot be combined with BenchmarkDotNet benchmark arguments.");
        }

        if (updateHistory && quick)
        {
            throw new InvalidOperationException("The --update-history option cannot be used with --quick. Generate publishable benchmark results first.");
        }

        if (updateHistory && benchmarkArgs.Any(IsBenchmarkFilterArgument))
        {
            throw new InvalidOperationException("The --update-history option cannot be combined with BenchmarkDotNet filtering arguments. Generate a full benchmark summary before updating committed history.");
        }

        return new PerformanceLabCommandLineOptions
        {
            Smoke = smoke,
            Quick = quick,
            UpdateHistory = updateHistory,
            HistoryLabel = string.IsNullOrWhiteSpace(historyLabel) ? null : historyLabel.Trim(),
            RawArgs = args.ToArray(),
            BenchmarkArgs = benchmarkArgs,
        };
    }

    private static bool IsBenchmarkFilterArgument(string argument)
    {
        return string.Equals(argument, "--filter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "-f", StringComparison.OrdinalIgnoreCase)
            || argument.StartsWith("--filter=", StringComparison.OrdinalIgnoreCase)
            || argument.StartsWith("-f=", StringComparison.OrdinalIgnoreCase);
    }
}
