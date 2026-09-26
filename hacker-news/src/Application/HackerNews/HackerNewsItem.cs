namespace Application.HackerNews;

/// <summary>
/// Application-owned Hacker News item data used for later validation, ranking, and mapping.
/// Nullable properties preserve absence in the upstream representation.
/// </summary>
public sealed record HackerNewsItem(
    long Id,
    string? Type,
    string? Title,
    string? Url,
    string? By,
    long? UnixTime,
    long? Score,
    long? Descendants,
    bool IsDeleted,
    bool IsDead);
