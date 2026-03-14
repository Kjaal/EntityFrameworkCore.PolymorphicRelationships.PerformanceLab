using EntityFrameworkCore.PolymorphicRelationships;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

public sealed class PerformanceLabDbContext(DbContextOptions<PerformanceLabDbContext> options) : DbContext(options)
{
    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Blog> Blogs => Set<Blog>();

    public DbSet<Thread> Threads => Set<Thread>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Taggable> Taggables => Set<Taggable>();

    public DbSet<PostDetail> PostDetails => Set<PostDetail>();

    public DbSet<BlogDetail> BlogDetails => Set<BlogDetail>();

    public DbSet<ThreadDetail> ThreadDetails => Set<ThreadDetail>();

    public DbSet<ControlPost> ControlPosts => Set<ControlPost>();

    public DbSet<ControlComment> ControlComments => Set<ControlComment>();

    public DbSet<ControlTag> ControlTags => Set<ControlTag>();

    public DbSet<ControlPostDetail> ControlPostDetails => Set<ControlPostDetail>();

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

            polymorphic.MorphToManyConvention<Post, Tag, Taggable, int, int>(
                nameof(Post.Tags),
                nameof(Tag.Posts),
                "taggable");
        });

        modelBuilder.Entity<Post>()
            .HasOne(entity => entity.Detail)
            .WithOne(entity => entity.Post)
            .HasForeignKey<PostDetail>(entity => entity.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Blog>()
            .HasOne(entity => entity.Detail)
            .WithOne(entity => entity.Blog)
            .HasForeignKey<BlogDetail>(entity => entity.BlogId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Thread>()
            .HasOne(entity => entity.Detail)
            .WithOne(entity => entity.Thread)
            .HasForeignKey<ThreadDetail>(entity => entity.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ControlPost>()
            .HasMany(entity => entity.Comments)
            .WithOne(entity => entity.Post)
            .HasForeignKey(entity => entity.ControlPostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ControlPost>()
            .HasOne(entity => entity.Detail)
            .WithOne(entity => entity.Post)
            .HasForeignKey<ControlPostDetail>(entity => entity.ControlPostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ControlPost>()
            .HasMany(entity => entity.Tags)
            .WithMany(entity => entity.Posts)
            .UsingEntity<Dictionary<string, object>>(
                "ControlPostTag",
                right => right.HasOne<ControlTag>().WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<ControlPost>().WithMany().HasForeignKey("PostId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.HasKey("PostId", "TagId");
                    join.HasIndex("TagId");
                });

        base.OnModelCreating(modelBuilder);
    }
}
