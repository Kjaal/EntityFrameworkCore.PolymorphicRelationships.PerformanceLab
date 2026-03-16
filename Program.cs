using BenchmarkDotNet.Running;
using EntityFrameworkCore.PolymorphicRelationships;
using EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;
using Microsoft.EntityFrameworkCore;

if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
{
    var databaseName = $"polymorphic_perf_smoke_{Guid.NewGuid():N}";

    try
    {
        await PostgresDatabaseManager.RecreateDatabaseAsync(databaseName);

        var options = new DbContextOptionsBuilder<PerformanceLabDbContext>()
            .UseNpgsql(PostgresOptions.CreateDatabaseConnectionString(databaseName))
            .UsePolymorphicRelationships()
            .Options;

        await using var dbContext = new PerformanceLabDbContext(options);
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

BenchmarkSwitcher.FromAssembly(typeof(PolymorphicRelationshipBenchmarks).Assembly).Run(args);
