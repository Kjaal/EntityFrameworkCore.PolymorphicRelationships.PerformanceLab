using EntityFrameworkCore.PolymorphicRelationships;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

public sealed class PerformanceLabDbContext(DbContextOptions<PerformanceLabDbContext> options) : DbContext(options)
{
    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Blog> Blogs => Set<Blog>();

    public DbSet<Thread> Threads => Set<Thread>();

    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UsePolymorphicRelationships(polymorphic =>
        {
            polymorphic.MorphMap<Post>("posts");
            polymorphic.MorphMap<Blog>("blogs");
            polymorphic.MorphMap<Thread>("threads");

            polymorphic.Entity<Comment>()
                .MorphToConvention<int>(nameof(Comment.Commentable))
                .MorphMany<Post>(nameof(Post.Comments))
                .MorphMany<Blog>(nameof(Blog.Comments))
                .MorphMany<Thread>(nameof(Thread.Comments));
        });

        base.OnModelCreating(modelBuilder);
    }
}
