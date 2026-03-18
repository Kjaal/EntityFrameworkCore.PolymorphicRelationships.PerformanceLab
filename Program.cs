using BenchmarkDotNet.Running;
using EntityFrameworkCore.PolymorphicRelationships;
using EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;
using Microsoft.EntityFrameworkCore;

var commandLineOptions = PerformanceLabCommandLineOptions.Parse(args);
PerformanceLabRuntimeOptions.Configure(commandLineOptions);

if (commandLineOptions.Smoke)
{
    var databaseName = $"polymorphic_perf_smoke_{Guid.NewGuid():N}";

    try
    {
        await PostgresDatabaseManager.RecreateDatabaseAsync(databaseName);

        var dbContextOptions = new DbContextOptionsBuilder<PerformanceLabDbContext>()
            .UseNpgsql(PostgresOptions.CreateDatabaseConnectionString(databaseName))
            .UsePolymorphicRelationships()
            .Options;

        await using var dbContext = new PerformanceLabDbContext(dbContextOptions);
        await dbContext.Database.EnsureCreatedAsync();
        await BenchmarkDataSeeder.SeedAsync(dbContext, ownerCountPerType: 3, commentsPerOwner: 2);

        var post = await dbContext.Posts
            .IncludeMorph(entity => entity.Comments)
            .OrderBy(entity => entity.Id)
            .FirstAsync();

        var comments = post.Comments;

        var loadedComments = await dbContext.Comments
            .IncludeMorph(entity => entity.Commentable)
            .OrderBy(entity => entity.Id)
            .Take(6)
            .ToListAsync();

        Console.WriteLine($"Seeded comments for first post: {comments.Count}");
        Console.WriteLine($"Loaded mixed owners: {loadedComments.Count(entity => entity.Commentable is not null)}");
        Console.WriteLine(PostgresOptions.GetConfigurationMessage());
        return;
    }
    finally
    {
        await PostgresDatabaseManager.DropDatabaseAsync(databaseName);
    }
}

var metadataResult = await PerformanceLabRunMetadataWriter.WriteAsync(commandLineOptions);
var summaries = BenchmarkSwitcher
    .FromAssembly(typeof(PolymorphicRelationshipBenchmarks).Assembly)
    .Run(commandLineOptions.BenchmarkArgs.ToArray(), PerformanceLabBenchmarkConfig.Create(commandLineOptions))
    .ToArray();

await PerformanceLabSummaryWriter.WriteAsync(summaries, commandLineOptions, metadataResult);
Environment.Exit(0);
