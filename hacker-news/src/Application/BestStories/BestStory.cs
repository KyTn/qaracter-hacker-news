namespace Application.BestStories;

/// <summary>The application-owned public representation of one best story.</summary>
public sealed record BestStory(
    string Title,
    string? Uri,
    string PostedBy,
    DateTimeOffset Time,
    long Score,
    long CommentCount);
