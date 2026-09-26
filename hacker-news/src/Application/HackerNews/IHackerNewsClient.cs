namespace Application.HackerNews;

/// <summary>Provides dependency-neutral access to the Hacker News best-stories feed.</summary>
public interface IHackerNewsClient
{
    /// <summary>Gets candidate identifiers in the order supplied by Hacker News.</summary>
    ValueTask<HackerNewsResult<IReadOnlyList<long>>> GetBestStoryIdsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Gets one Hacker News item without exposing its wire representation.</summary>
    ValueTask<HackerNewsResult<HackerNewsItem>> GetItemAsync(
        long id,
        CancellationToken cancellationToken = default);
}
