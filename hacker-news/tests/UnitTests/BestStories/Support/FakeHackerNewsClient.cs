using Application.HackerNews;

namespace UnitTests.BestStories.Support;

internal sealed class FakeHackerNewsClient : IHackerNewsClient
{
    private int _active;
    private int _maximumActive;

    public HackerNewsResult<IReadOnlyList<long>> FeedResult { get; set; } =
        HackerNewsResult<IReadOnlyList<long>>.Success(Array.Empty<long>());

    public Dictionary<long, HackerNewsResult<HackerNewsItem>> ItemResults { get; } = [];
    public List<long> RequestedIds { get; } = [];
    public int FeedCalls { get; private set; }
    public int MaximumActive => Volatile.Read(ref _maximumActive);
    public TimeSpan ItemDelay { get; set; }

    public ValueTask<HackerNewsResult<IReadOnlyList<long>>> GetBestStoryIdsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FeedCalls++;
        return ValueTask.FromResult(FeedResult);
    }

    public async ValueTask<HackerNewsResult<HackerNewsItem>> GetItemAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        lock (RequestedIds)
            RequestedIds.Add(id);

        int active = Interlocked.Increment(ref _active);
        int observed;
        do
        {
            observed = Volatile.Read(ref _maximumActive);
        } while (active > observed &&
                 Interlocked.CompareExchange(ref _maximumActive, active, observed) != observed);

        try
        {
            if (ItemDelay > TimeSpan.Zero)
                await Task.Delay(ItemDelay, cancellationToken);

            return ItemResults.TryGetValue(id, out HackerNewsResult<HackerNewsItem>? result)
                ? result
                : HackerNewsResult<HackerNewsItem>.Failure(HackerNewsOutcome.NotFound);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }
}
