using System.ComponentModel.DataAnnotations.Schema;

namespace EntityFrameworkCore.PolymorphicRelationships.PerformanceLab;

public sealed class Post
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    [NotMapped]
    public List<Comment> Comments { get; set; } = new();
}

public sealed class Blog
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    [NotMapped]
    public List<Comment> Comments { get; set; } = new();
}

public sealed class Thread
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    [NotMapped]
    public List<Comment> Comments { get; set; } = new();
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
