using Application.HackerNews;

namespace IntegrationTests.BestStories.Support;

internal sealed class TestHackerNewsClient : IHackerNewsClient
{
    private int _feedCalls;
    private int _itemCalls;

    public HackerNewsResult<IReadOnlyList<long>> FeedResult { get; set; } =
        HackerNewsResult<IReadOnlyList<long>>.Success(Array.Empty<long>());
    public Dictionary<long, HackerNewsResult<HackerNewsItem>> ItemResults { get; } = [];
    public int FeedCalls => Volatile.Read(ref _feedCalls);
    public int ItemCalls => Volatile.Read(ref _itemCalls);
    public TimeSpan ItemDelay { get; set; }
    public TaskCompletionSource? ItemGate { get; set; }

    public ValueTask<HackerNewsResult<IReadOnlyList<long>>> GetBestStoryIdsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _feedCalls);
        return ValueTask.FromResult(FeedResult);
    }

    public async ValueTask<HackerNewsResult<HackerNewsItem>> GetItemAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _itemCalls);
        if (ItemGate is not null)
            await ItemGate.Task.WaitAsync(cancellationToken);
        if (ItemDelay > TimeSpan.Zero)
            await Task.Delay(ItemDelay, cancellationToken);

        return ItemResults.TryGetValue(id, out var result)
            ? result
            : HackerNewsResult<HackerNewsItem>.Failure(HackerNewsOutcome.NotFound);
    }
}
