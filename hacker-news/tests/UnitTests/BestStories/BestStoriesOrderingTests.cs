using Application.BestStories;
using Application.HackerNews;
using UnitTests.BestStories.Support;
using static UnitTests.BestStories.BestStoriesSelectionTests;

namespace UnitTests.BestStories;

public sealed class BestStoriesOrderingTests
{
    [Fact]
    public async Task Orders_by_score_then_preserves_feed_order_for_ties()
    {
        FakeHackerNewsClient client = new()
        {
            FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1, 2, 3])
        };
        client.ItemResults[1] = Success(Item(1, score: 10));
        client.ItemResults[2] = Success(Item(2, score: 20));
        client.ItemResults[3] = Success(Item(3, score: 20));

        BestStoriesResult result = await Service(client)
            .GetAsync(new BestStoriesQuery(3));

        Assert.Equal(["Story 2", "Story 3", "Story 1"],
            result.Stories!.Select(story => story.Title));
    }

    [Fact]
    public async Task Stops_before_starting_a_later_batch()
    {
        FakeHackerNewsClient client = new()
        {
            FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1, 2, 3, 4])
        };
        foreach (long id in Enumerable.Range(1, 4).Select(value => (long)value))
            client.ItemResults[id] = Success(Item(id));

        BestStoriesResult result = await Service(client, concurrency: 2)
            .GetAsync(new BestStoriesQuery(1));

        Assert.Single(result.Stories!);
        Assert.Equal([1L, 2L], client.RequestedIds);
    }

    [Fact]
    public async Task Bounds_per_request_concurrency()
    {
        FakeHackerNewsClient client = new()
        {
            FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success(
                Enumerable.Range(1, 20).Select(value => (long)value).ToArray()),
            ItemDelay = TimeSpan.FromMilliseconds(20)
        };
        foreach (long id in Enumerable.Range(1, 20).Select(value => (long)value))
            client.ItemResults[id] = Success(Item(id));

        await Service(client, concurrency: 4).GetAsync(new BestStoriesQuery(20));

        Assert.InRange(client.MaximumActive, 1, 4);
    }
}
