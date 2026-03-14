using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

[MemoryDiagnoser]
public class PolymorphicRelationshipBenchmarks
{
    private string _databaseName = null!;
    private DbContextOptions<PerformanceLabDbContext> _options = null!;
    private List<int> _postIds = null!;
    private List<int> _controlPostIds = null!;
    private List<int> _controlCommentIds = null!;
    private List<int> _blogIds = null!;
    private List<int> _threadIds = null!;
    private List<int> _mixedCommentIds = null!;

    [Params(1000)]
    public int OwnerCountPerType { get; set; }

    [Params(20)]
    public int CommentsPerOwner { get; set; }

    [GlobalSetup]
    public async Task GlobalSetupAsync()
    {
        _databaseName = $"polymorphic_perf_{Guid.NewGuid():N}";
        await PostgresDatabaseManager.RecreateDatabaseAsync(_databaseName);

        _options = new DbContextOptionsBuilder<PerformanceLabDbContext>()
            .UseNpgsql(PostgresOptions.CreateDatabaseConnectionString(_databaseName))
            .UsePolymorphicRelationships()
            .Options;

        await using var setupContext = new PerformanceLabDbContext(_options);
        await setupContext.Database.EnsureCreatedAsync();
        await BenchmarkDataSeeder.SeedAsync(setupContext, OwnerCountPerType, CommentsPerOwner);

        _postIds = await setupContext.Posts.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(100).ToListAsync();
        _controlPostIds = await setupContext.ControlPosts.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(100).ToListAsync();
        _controlCommentIds = await setupContext.ControlComments.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(1000).ToListAsync();
        _blogIds = await setupContext.Blogs.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(100).ToListAsync();
        _threadIds = await setupContext.Threads.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(100).ToListAsync();
        _mixedCommentIds = await setupContext.Comments.OrderBy(entity => entity.Id).Select(entity => entity.Id).Take(1000).ToListAsync();
    }

    [GlobalCleanup]
    public async Task GlobalCleanupAsync()
    {
        if (!string.IsNullOrWhiteSpace(_databaseName))
        {
            await PostgresDatabaseManager.DropDatabaseAsync(_databaseName);
        }
    }

    [Benchmark(Baseline = true)]
    public async Task<int> NonPolymorphic_Control_Post_Comments()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var total = 0;

        foreach (var postId in _controlPostIds)
        {
            total += await dbContext.ControlComments
                .Where(entity => entity.ControlPostId == postId)
                .CountAsync();
        }

        return total;
    }

    [Benchmark]
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
    public async Task<int> NonPolymorphic_Control_IncludeComments_For_Posts_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var posts = await dbContext.ControlPosts
            .Where(entity => _controlPostIds.Contains(entity.Id))
            .Include(entity => entity.Comments)
            .ToListAsync();

        return posts.Sum(entity => entity.Comments.Count);
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
    public async Task<int> Extension_LoadMorphMany_For_Posts_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var posts = await dbContext.Posts
            .Where(entity => _postIds.Contains(entity.Id))
            .ToListAsync();

        var commentsByPost = await dbContext.LoadMorphManyAsync<Post, Comment>(posts, nameof(Post.Comments));
        return commentsByPost.Sum(entry => entry.Value.Count);
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

    [Benchmark]
    public async Task<int> NonPolymorphic_Control_LoadCommentOwners_WithInclude()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var comments = await dbContext.ControlComments
            .Where(entity => _controlCommentIds.Contains(entity.Id))
            .Include(entity => entity.Post)
            .ThenInclude(entity => entity!.Detail)
            .ToListAsync();

        return comments.Count(entity => entity.Post?.Detail is not null);
    }

    [Benchmark]
    public async Task<int> Extension_LoadMixedMorphOwners_Batch_WithPlans()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var comments = await dbContext.Comments
            .Where(entity => _mixedCommentIds.Contains(entity.Id))
            .ToListAsync();

        var owners = await dbContext.LoadMorphsAsync(
            comments,
            nameof(Comment.Commentable),
            plan => plan
                .For<Post>(query => query.Include(entity => entity.Detail))
                .For<Blog>(query => query.Include(entity => entity.Detail))
                .For<Thread>(query => query.Include(entity => entity.Detail)));

        return owners.Count(entry => entry.Value switch
        {
            Post post => post.Detail is not null,
            Blog blog => blog.Detail is not null,
            Thread thread => thread.Detail is not null,
            _ => false,
        });
    }
}
