using Application.HackerNews;
using IntegrationTests.BestStories.Support;

namespace IntegrationTests.BestStories;

public sealed class BestStoriesCancellationTests
{
    [Fact]
    public async Task Caller_cancellation_stops_in_flight_item_work()
    {
        await using BestStoriesApiFactory factory = new();
        factory.HackerNews.FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1]);
        factory.HackerNews.ItemDelay = TimeSpan.FromSeconds(5);
        using HttpClient client = factory.CreateClient();
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync("/api/v1/best-stories?n=1", cancellation.Token));

        Assert.Equal(1, factory.HackerNews.FeedCalls);
        Assert.Equal(1, factory.HackerNews.ItemCalls);
    }
}
