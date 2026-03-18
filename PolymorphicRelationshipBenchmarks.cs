using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

[MemoryDiagnoser]
public class PolymorphicRelationshipBenchmarks
{
    private string _databaseName = null!;
    private DbContextOptions<PerformanceLabDbContext> _options = null!;
    private DbContextOptions<PerformanceLabDbContext> _noTrackingOptions = null!;
    private List<int> _postIds = null!;
    private List<int> _controlPostIds = null!;
    private List<int> _controlCommentIds = null!;
    private List<int> _blogIds = null!;
    private List<int> _threadIds = null!;
    private List<int> _mixedCommentIds = null!;

    [ParamsSource(nameof(OwnerCountPerTypeValues))]
    public int OwnerCountPerType { get; set; }

    [ParamsSource(nameof(CommentsPerOwnerValues))]
    public int CommentsPerOwner { get; set; }

    public IEnumerable<int> OwnerCountPerTypeValues => PerformanceLabRuntimeOptions.OwnerCountPerTypeValues;

    public IEnumerable<int> CommentsPerOwnerValues => PerformanceLabRuntimeOptions.CommentsPerOwnerValues;

    [GlobalSetup]
    public async Task GlobalSetupAsync()
    {
        _databaseName = $"polymorphic_perf_{Guid.NewGuid():N}";
        await PostgresDatabaseManager.RecreateDatabaseAsync(_databaseName);

        _options = new DbContextOptionsBuilder<PerformanceLabDbContext>()
            .UseNpgsql(PostgresOptions.CreateDatabaseConnectionString(_databaseName))
            .UsePolymorphicRelationships(options => options.EnableExperimentalSelectProjectionSupport())
            .Options;

        _noTrackingOptions = new DbContextOptionsBuilder<PerformanceLabDbContext>()
            .UseNpgsql(PostgresOptions.CreateDatabaseConnectionString(_databaseName))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .UsePolymorphicRelationships(options => options.EnableExperimentalSelectProjectionSupport())
            .Options;

        await using var setupContext = new PerformanceLabDbContext(_options);
        await setupContext.Database.EnsureCreatedAsync();
        await BenchmarkDataSeeder.SeedAsync(setupContext, OwnerCountPerType, CommentsPerOwner);

        _postIds = TakeEvenlySpacedSample(await setupContext.Posts.OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(), PerformanceLabRuntimeOptions.OwnerSampleSize);
        _controlPostIds = TakeEvenlySpacedSample(await setupContext.ControlPosts.OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(), PerformanceLabRuntimeOptions.OwnerSampleSize);
        _controlCommentIds = TakeEvenlySpacedSample(await setupContext.ControlComments.OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(), PerformanceLabRuntimeOptions.CommentSampleSize);
        _blogIds = TakeEvenlySpacedSample(await setupContext.Blogs.OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(), PerformanceLabRuntimeOptions.OwnerSampleSize);
        _threadIds = TakeEvenlySpacedSample(await setupContext.Threads.OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(), PerformanceLabRuntimeOptions.OwnerSampleSize);
        _mixedCommentIds = BuildMixedCommentSample(
            TakeEvenlySpacedSample(await setupContext.Comments.Where(entity => entity.CommentableType == "posts").OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(), PerformanceLabRuntimeOptions.CommentSampleSize / 3 + 1),
            TakeEvenlySpacedSample(await setupContext.Comments.Where(entity => entity.CommentableType == "blogs").OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(), PerformanceLabRuntimeOptions.CommentSampleSize / 3),
            TakeEvenlySpacedSample(await setupContext.Comments.Where(entity => entity.CommentableType == "threads").OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(), PerformanceLabRuntimeOptions.CommentSampleSize / 3));
    }

    [GlobalCleanup]
    public async Task GlobalCleanupAsync()
    {
        if (!string.IsNullOrWhiteSpace(_databaseName))
        {
            await PostgresDatabaseManager.DropDatabaseAsync(_databaseName);
        }
    }

    [Benchmark]
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
    public async Task<int> NonPolymorphic_Control_IncludeComments_For_Posts_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

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
    public async Task<int> Extension_LoadMorphMany_For_Posts_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);
        var posts = await dbContext.Posts
            .Where(entity => _postIds.Contains(entity.Id))
            .ToListAsync();

        var commentsByPost = await dbContext.LoadMorphManyUntrackedAsync<Post, Comment>(posts, nameof(Post.Comments));
        return commentsByPost.Sum(entry => entry.Value.Count);
    }

    [Benchmark]
    public async Task<int> Extension_IncludeMorph_Comments_For_Posts_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var posts = await dbContext.Posts
            .IncludeMorph(entity => entity.Comments)
            .Where(entity => _postIds.Contains(entity.Id))
            .ToListAsync();

        return posts.Sum(entity => entity.Comments.Count);
    }

    [Benchmark]
    public async Task<int> Extension_IncludeMorph_Comments_For_Posts_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

        var posts = await dbContext.Posts
            .IncludeMorph(entity => entity.Comments)
            .Where(entity => _postIds.Contains(entity.Id))
            .ToListAsync();

        return posts.Sum(entity => entity.Comments.Count);
    }

    [Benchmark]
    [BenchmarkCategory("ExperimentalProjection")]
    public async Task<int> Extension_SelectProjection_Comments_For_Posts_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var posts = await dbContext.Posts
            .Where(entity => _postIds.Contains(entity.Id))
            .Select(entity => new PostProjection
            {
                Title = entity.Title,
                Comments = entity.Comments,
            })
            .ToListAsync();

        return posts.Sum(entity => entity.Comments.Count);
    }

    [Benchmark]
    [BenchmarkCategory("ExperimentalProjection")]
    public async Task<int> Extension_SelectProjection_Comments_For_Posts_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

        var posts = await dbContext.Posts
            .Where(entity => _postIds.Contains(entity.Id))
            .Select(entity => new PostProjection
            {
                Title = entity.Title,
                Comments = entity.Comments,
            })
            .ToListAsync();

        return posts.Sum(entity => entity.Comments.Count);
    }

    [Benchmark]
    public async Task<int> NonPolymorphic_Control_FilterPostsWithComments_Count()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        return await dbContext.ControlPosts.Where(entity => entity.Comments.Count > 0).CountAsync();
    }

    [Benchmark]
    public async Task<int> Extension_Translated_FilterPostsWithComments_Count()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        return await dbContext.Posts.Where(entity => entity.Comments.Count > 0).CountAsync();
    }

    [Benchmark]
    public async Task<int> Extension_Translated_FilterPostsWithComments_Any()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        return await dbContext.Posts.Where(entity => entity.Comments.Any()).CountAsync();
    }

    [Benchmark]
    public async Task<int> NonPolymorphic_Control_FilterPostsWithTwoComments_Count()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        return await dbContext.ControlPosts.Where(entity => entity.Comments.Count > 1).CountAsync();
    }

    [Benchmark]
    public async Task<int> Extension_Translated_FilterPostsWithTwoComments_Count()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        return await dbContext.Posts.Where(entity => entity.Comments.Count > 1).CountAsync();
    }

    [Benchmark]
    public async Task<int> NonPolymorphic_Control_FilterPostsWithoutComments_Count()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        return await dbContext.ControlPosts.Where(entity => entity.Comments.Count == 0).CountAsync();
    }

    [Benchmark]
    public async Task<int> Extension_Translated_FilterPostsWithoutComments_Count()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        return await dbContext.Posts.Where(entity => entity.Comments.Count == 0).CountAsync();
    }

    [Benchmark]
    public async Task<int> NonPolymorphic_Control_LoadLatestComment_For_Posts()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var total = 0;

        foreach (var postId in _controlPostIds)
        {
            var latestComment = await dbContext.ControlComments
                .Where(entity => entity.ControlPostId == postId)
                .OrderByDescending(entity => entity.Id)
                .FirstOrDefaultAsync();

            if (latestComment is not null)
            {
                total += latestComment.Id;
            }
        }

        return total;
    }

    [Benchmark]
    public async Task<int> Extension_LoadMorphLatestOfMany_For_Posts()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);
        var total = 0;

        foreach (var postId in _postIds)
        {
            var post = await dbContext.Posts.SingleAsync(entity => entity.Id == postId);
            var latestComment = await dbContext.LoadMorphLatestOfManyAsync<Post, Comment, int>(
                post,
                nameof(Post.Comments),
                entity => entity.Id,
                nameof(Post.LatestComment));

            if (latestComment is not null)
            {
                total += latestComment.Id;
            }
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
    public async Task<int> Extension_LoadMorphManyAcross_For_Blogs_And_Threads_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var blogs = await dbContext.Blogs
            .Where(entity => _blogIds.Contains(entity.Id))
            .ToListAsync();
        var threads = await dbContext.Threads
            .Where(entity => _threadIds.Contains(entity.Id))
            .ToListAsync();

        var principals = blogs.Cast<object>().Concat(threads).ToList();

        var commentsByPrincipal = await dbContext.LoadMorphManyAcrossAsync<Comment>(principals, nameof(Blog.Comments));
        return commentsByPrincipal.Sum(entry => entry.Value.Count);
    }

    [Benchmark]
    public async Task<int> Extension_LoadMorphManyAcross_For_Blogs_And_Threads_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

        var blogs = await dbContext.Blogs
            .Where(entity => _blogIds.Contains(entity.Id))
            .ToListAsync();
        var threads = await dbContext.Threads
            .Where(entity => _threadIds.Contains(entity.Id))
            .ToListAsync();

        var principals = blogs.Cast<object>().Concat(threads).ToList();

        var commentsByPrincipal = await dbContext.LoadMorphManyAcrossUntrackedAsync<Comment>(principals, nameof(Blog.Comments));
        return commentsByPrincipal.Sum(entry => entry.Value.Count);
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
    public async Task<int> Extension_IncludeMorph_Owners_For_Comments_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var comments = await dbContext.Comments
            .IncludeMorph(entity => entity.Commentable)
            .Where(entity => _mixedCommentIds.Contains(entity.Id))
            .ToListAsync();

        return comments.Count(entity => entity.Commentable is not null);
    }

    [Benchmark]
    public async Task<int> Extension_IncludeMorph_Owners_For_Comments_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

        var comments = await dbContext.Comments
            .IncludeMorph(entity => entity.Commentable)
            .Where(entity => _mixedCommentIds.Contains(entity.Id))
            .ToListAsync();

        return comments.Count(entity => entity.Commentable is not null);
    }

    [Benchmark]
    [BenchmarkCategory("ExperimentalProjection")]
    public async Task<int> Extension_SelectProjection_Owners_For_Comments_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var comments = await dbContext.Comments
            .Where(entity => _mixedCommentIds.Contains(entity.Id))
            .Select(entity => new CommentProjection
            {
                Body = entity.Body,
                Commentable = entity.Commentable,
            })
            .ToListAsync();

        return comments.Count(entity => entity.Commentable is not null);
    }

    [Benchmark]
    [BenchmarkCategory("ExperimentalProjection")]
    public async Task<int> Extension_SelectProjection_Owners_For_Comments_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

        var comments = await dbContext.Comments
            .Where(entity => _mixedCommentIds.Contains(entity.Id))
            .Select(entity => new CommentProjection
            {
                Body = entity.Body,
                Commentable = entity.Commentable,
            })
            .ToListAsync();

        return comments.Count(entity => entity.Commentable is not null);
    }

    [Benchmark]
    public async Task<int> NonPolymorphic_Control_OrderCommentsByOwnerTitle()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var commentIds = await dbContext.ControlComments
            .OrderBy(entity => entity.Post!.Title)
            .Select(entity => entity.Id)
            .Take(1000)
            .ToListAsync();

        return commentIds.Sum();
    }

    [Benchmark]
    public async Task<int> Extension_Translated_OrderCommentsByOwnerTitle()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var commentIds = await dbContext.Comments
            .Where(entity => entity.CommentableType == "posts")
            .OrderBy(entity => ((Post)entity.Commentable!).Title)
            .Select(entity => entity.Id)
            .Take(1000)
            .ToListAsync();

        return commentIds.Sum();
    }

    [Benchmark]
    public async Task<int> NonPolymorphic_Control_FilterCommentsByOwnerTitle()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        return await dbContext.ControlComments
            .Where(entity => entity.Post!.Title == "Control post 100")
            .CountAsync();
    }

    [Benchmark]
    public async Task<int> Extension_Translated_FilterCommentsByOwnerTitle()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        return await dbContext.Comments
            .Where(entity => entity.CommentableType == "posts")
            .Where(entity => ((Post)entity.Commentable!).Title == "Post 100")
            .CountAsync();
    }

    [Benchmark]
    public async Task<int> NonPolymorphic_Control_LoadTags_For_Posts_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var posts = await dbContext.ControlPosts
            .Where(entity => _controlPostIds.Contains(entity.Id))
            .Include(entity => entity.Tags)
            .ToListAsync();

        return posts.Sum(entity => entity.Tags.Count);
    }

    [Benchmark]
    public async Task<int> Extension_LoadMorphToMany_For_Posts_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var posts = await dbContext.Posts
            .Where(entity => _postIds.Contains(entity.Id))
            .ToListAsync();

        var tagsByPost = await dbContext.LoadMorphToManyAsync<Post, Tag>(posts, nameof(Post.Tags));
        return tagsByPost.Sum(entry => entry.Value.Count);
    }

    [Benchmark]
    public async Task<int> Extension_LoadMorphToMany_For_Posts_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

        var posts = await dbContext.Posts
            .Where(entity => _postIds.Contains(entity.Id))
            .ToListAsync();

        var tagsByPost = await dbContext.LoadMorphToManyUntrackedAsync<Post, Tag>(posts, nameof(Post.Tags));
        return tagsByPost.Sum(entry => entry.Value.Count);
    }

    [Benchmark]
    public async Task<int> Extension_IncludeMorph_Tags_For_Posts_Batch()
    {
        await using var dbContext = new PerformanceLabDbContext(_options);

        var posts = await dbContext.Posts
            .IncludeMorph(entity => entity.Tags)
            .Where(entity => _postIds.Contains(entity.Id))
            .ToListAsync();

        return posts.Sum(entity => entity.Tags.Count);
    }

    [Benchmark]
    public async Task<int> Extension_IncludeMorph_Tags_For_Posts_Batch_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

        var posts = await dbContext.Posts
            .IncludeMorph(entity => entity.Tags)
            .Where(entity => _postIds.Contains(entity.Id))
            .ToListAsync();

        return posts.Sum(entity => entity.Tags.Count);
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
    public async Task<int> NonPolymorphic_Control_LoadCommentOwners_WithInclude_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);

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

    [Benchmark]
    public async Task<int> Extension_LoadMixedMorphOwners_Batch_WithPlans_NoTracking()
    {
        await using var dbContext = new PerformanceLabDbContext(_noTrackingOptions);
        var comments = await dbContext.Comments
            .Where(entity => _mixedCommentIds.Contains(entity.Id))
            .ToListAsync();

        var owners = await dbContext.LoadMorphsUntrackedAsync(
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

    private static List<int> BuildMixedCommentSample(List<int> postCommentIds, List<int> blogCommentIds, List<int> threadCommentIds)
    {
        return postCommentIds
            .Concat(blogCommentIds)
            .Concat(threadCommentIds)
            .OrderBy(id => id)
            .ToList();
    }

    private static List<int> TakeEvenlySpacedSample(IReadOnlyList<int> values, int sampleSize)
    {
        if (values.Count <= sampleSize)
        {
            return values.ToList();
        }

        var results = new List<int>(sampleSize);
        for (var index = 0; index < sampleSize; index++)
        {
            var position = (int)Math.Round(index * (values.Count - 1d) / (sampleSize - 1d));
            results.Add(values[position]);
        }

        return results.Distinct().ToList();
    }
}
