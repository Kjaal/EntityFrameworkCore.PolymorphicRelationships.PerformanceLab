namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class BenchmarkDataSeeder
{
    public static async Task SeedAsync(PerformanceLabDbContext dbContext, int ownerCountPerType, int commentsPerOwner, CancellationToken cancellationToken = default)
    {
        var commentId = 1;

        for (var index = 1; index <= ownerCountPerType; index++)
        {
            var post = new Post { Id = index, Title = $"Post {index}" };
            dbContext.Posts.Add(post);

            for (var commentIndex = 0; commentIndex < commentsPerOwner; commentIndex++)
            {
                var comment = new Comment { Id = commentId++, Body = $"Post comment {index}-{commentIndex}" };
                dbContext.Comments.Add(comment);
                dbContext.SetMorphReference(comment, nameof(Comment.Commentable), post);
            }
        }

        for (var index = 1; index <= ownerCountPerType; index++)
        {
            var blog = new Blog { Id = index, Title = $"Blog {index}" };
            dbContext.Blogs.Add(blog);

            for (var commentIndex = 0; commentIndex < commentsPerOwner; commentIndex++)
            {
                var comment = new Comment { Id = commentId++, Body = $"Blog comment {index}-{commentIndex}" };
                dbContext.Comments.Add(comment);
                dbContext.SetMorphReference(comment, nameof(Comment.Commentable), blog);
            }
        }

        for (var index = 1; index <= ownerCountPerType; index++)
        {
            var thread = new Thread { Id = index, Title = $"Thread {index}" };
            dbContext.Threads.Add(thread);

            for (var commentIndex = 0; commentIndex < commentsPerOwner; commentIndex++)
            {
                var comment = new Comment { Id = commentId++, Body = $"Thread comment {index}-{commentIndex}" };
                dbContext.Comments.Add(comment);
                dbContext.SetMorphReference(comment, nameof(Comment.Commentable), thread);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
