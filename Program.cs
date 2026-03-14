using BenchmarkDotNet.Running;
using EntityFrameworkCore.PolymorphicRelationships;
using EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<PerformanceLabDbContext>()
        .UseSqlite(connection)
        .UsePolymorphicRelationships()
        .Options;

    await using var dbContext = new PerformanceLabDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();
    await BenchmarkDataSeeder.SeedAsync(dbContext, ownerCountPerType: 3, commentsPerOwner: 2);

    var post = await dbContext.Posts.FirstAsync();
    var comments = await dbContext.LoadMorphManyAsync<Post, Comment>(post, nameof(Post.Comments));
    var owners = await dbContext.LoadMorphsAsync(await dbContext.Comments.Take(6).ToListAsync(), nameof(Comment.Commentable));

    Console.WriteLine($"Seeded comments for first post: {comments.Count}");
    Console.WriteLine($"Loaded mixed owners: {owners.Count}");
    return;
}

BenchmarkRunner.Run<PolymorphicRelationshipBenchmarks>();
