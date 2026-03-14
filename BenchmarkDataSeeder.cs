namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

internal static class BenchmarkDataSeeder
{
    public static async Task SeedAsync(PerformanceLabDbContext dbContext, int ownerCountPerType, int commentsPerOwner, CancellationToken cancellationToken = default)
    {
        var commentId = 1;
        var controlCommentId = 1;

        for (var index = 1; index <= ownerCountPerType; index++)
        {
            var post = new Post { Id = index, Title = $"Post {index}" };
            dbContext.Posts.Add(post);
            dbContext.PostDetails.Add(new PostDetail
            {
                Id = index,
                PostId = post.Id,
                Summary = $"Post detail {index}",
            });

            var controlPost = new ControlPost { Id = index, Title = $"Control post {index}" };
            dbContext.ControlPosts.Add(controlPost);
            dbContext.ControlPostDetails.Add(new ControlPostDetail
            {
                Id = index,
                ControlPostId = controlPost.Id,
                Summary = $"Control post detail {index}",
            });

            for (var commentIndex = 0; commentIndex < commentsPerOwner; commentIndex++)
            {
                var comment = new Comment { Id = commentId++, Body = $"Post comment {index}-{commentIndex}" };
                dbContext.Comments.Add(comment);
                dbContext.SetMorphReference(comment, nameof(Comment.Commentable), post);

                dbContext.ControlComments.Add(new ControlComment
                {
                    Id = controlCommentId++,
                    Body = $"Control post comment {index}-{commentIndex}",
                    ControlPostId = controlPost.Id,
                });
            }
        }

        for (var index = 1; index <= ownerCountPerType; index++)
        {
            var blog = new Blog { Id = index, Title = $"Blog {index}" };
            dbContext.Blogs.Add(blog);
            dbContext.BlogDetails.Add(new BlogDetail
            {
                Id = index,
                BlogId = blog.Id,
                Summary = $"Blog detail {index}",
            });

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
            dbContext.ThreadDetails.Add(new ThreadDetail
            {
                Id = index,
                ThreadId = thread.Id,
                Summary = $"Thread detail {index}",
            });

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
