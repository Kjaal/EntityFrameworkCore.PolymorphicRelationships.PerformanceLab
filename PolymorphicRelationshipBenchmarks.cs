using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

[MemoryDiagnoser]
public sealed class PolymorphicRelationshipBenchmarks
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<PerformanceLabDbContext> _options = null!;
    private List<int> _postIds = null!;
    private List<int> _blogIds = null!;
    private List<int> _threadIds = null!;
    private List<int> _mixedCommentIds = null!;

    [Params(250)]
    public int OwnerCountPerType { get; set; }

    [Params(12)]
    public int CommentsPerOwner { get; set; }

    [GlobalSetup]
    public async Task GlobalSetupAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<PerformanceLabDbContext>()
            .UseSqlite(_connection)
            .UsePolymorphicRelationships()
            .Options;

        await using var setupContext = new PerformanceLabDbContext(_options);
        await setupContext.Database.EnsureCreatedAsync();
        await BenchmarkDataSeeder.SeedAsync(setupContext, OwnerCountPerType, CommentsPerOwner);

        _postIds = await setupContext.Posts.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(25).ToListAsync();
        _blogIds = await setupContext.Blogs.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(25).ToListAsync();
        _threadIds = await setupContext.Threads.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(25).ToListAsync();
        _mixedCommentIds = await setupContext.Comments.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(150).ToListAsync();
    }

    [GlobalCleanup]
    public async Task GlobalCleanupAsync()
    {
        await _connection.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> Manual_Post_Comment_Filter()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var total = 0;

        foreach (var postId in _postIds)
        {
            total += await dbContext.Comments
                .Where(entity => entity.CommentableType == "posts" && entity.CommentableId == postId)
                .CountAsync();
        }

        return total;
    }

    [Benchmark]
    public async Task<int> Extension_LoadMorphMany_For_Posts()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var total = 0;

        foreach (var postId in _postIds)
        {
            var post = await dbContext.Posts.SingleAsync(entity => entity.Id == postId);
            var comments = await dbContext.LoadMorphManyAsync<Post, Comment>(post, nameof(Post.Comments));
            total += comments.Count;
        }

        return total;
    }

    [Benchmark]
    public async Task<int> Extension_LoadMorphMany_For_Blogs_And_Threads()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var total = 0;

        foreach (var blogId in _blogIds)
        {
            var blog = await dbContext.Blogs.SingleAsync(entity => entity.Id == blogId);
            total += (await dbContext.LoadMorphManyAsync<Blog, Comment>(blog, nameof(Blog.Comments))).Count;
        }

        foreach (var threadId in _threadIds)
        {
            var thread = await dbContext.Threads.SingleAsync(entity => entity.Id == threadId);
            total += (await dbContext.LoadMorphManyAsync<Thread, Comment>(thread, nameof(Thread.Comments))).Count;
        }

        return total;
    }

    [Benchmark]
    public async Task<int> Extension_LoadMixedMorphOwners_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var comments = await dbContext.Comments
            .Where(entity => _mixedCommentIds.Contains(entity.Id))
            .ToListAsync();

        var owners = await dbContext.LoadMorphsAsync(comments, nameof(Comment.Commentable));
        return owners.Count(owner => owner.Value is not null);
    }
}
