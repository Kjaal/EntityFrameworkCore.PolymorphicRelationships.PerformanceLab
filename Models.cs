using System.ComponentModel.DataAnnotations.Schema;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

public sealed class Post
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public PostDetail? Detail { get; set; }

    [NotMapped]
    public List<Comment> Comments { get; set; } = new();
}

public sealed class Blog
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public BlogDetail? Detail { get; set; }

    [NotMapped]
    public List<Comment> Comments { get; set; } = new();
}

public sealed class Thread
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public ThreadDetail? Detail { get; set; }

    [NotMapped]
    public List<Comment> Comments { get; set; } = new();
}

public sealed class PostDetail
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Post? Post { get; set; }
}

public sealed class BlogDetail
{
    public int Id { get; set; }

    public int BlogId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Blog? Blog { get; set; }
}

public sealed class ThreadDetail
{
    public int Id { get; set; }

    public int ThreadId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Thread? Thread { get; set; }
}

public sealed class Comment
{
    public int Id { get; set; }

    public string Body { get; set; } = string.Empty;

    public string? CommentableType { get; set; }

    public int? CommentableId { get; set; }

    [NotMapped]
    public object? Commentable { get; set; }
}

public sealed class ControlPost
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public ControlPostDetail? Detail { get; set; }

    public List<ControlComment> Comments { get; set; } = new();
}

public sealed class ControlPostDetail
{
    public int Id { get; set; }

    public int ControlPostId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public ControlPost? Post { get; set; }
}

public sealed class ControlComment
{
    public int Id { get; set; }

    public string Body { get; set; } = string.Empty;

    public int ControlPostId { get; set; }

    public ControlPost? Post { get; set; }
}
