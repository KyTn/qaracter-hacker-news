using Application.BestStories;
using Application.HackerNews;
using UnitTests.BestStories.Support;
using static UnitTests.BestStories.BestStoriesSelectionTests;

namespace UnitTests.BestStories;

public sealed class BestStoriesFailureTests
{
    [Theory]
    [InlineData(HackerNewsOutcome.Timeout, BestStoriesOutcome.Timeout)]
    [InlineData(HackerNewsOutcome.Unavailable, BestStoriesOutcome.Unavailable)]
    [InlineData(HackerNewsOutcome.Throttled, BestStoriesOutcome.Throttled)]
    public async Task Terminal_item_failures_do_not_return_partial_success(
        HackerNewsOutcome clientOutcome,
        BestStoriesOutcome expected)
    {
        FakeHackerNewsClient client = new()
        {
            FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1, 2])
        };
        client.ItemResults[1] = Success(Item(1));
        client.ItemResults[2] = HackerNewsResult<HackerNewsItem>.Failure(clientOutcome);

        BestStoriesResult result = await Service(client)
            .GetAsync(new BestStoriesQuery(2));

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Stories);
    }

    [Fact]
    public async Task Invalid_feed_is_terminal()
    {
        FakeHackerNewsClient client = new()
        {
            FeedResult = HackerNewsResult<IReadOnlyList<long>>.Failure(
                HackerNewsOutcome.InvalidUpstreamData)
        };

        BestStoriesResult result = await Service(client).GetAsync(new BestStoriesQuery(1));

        Assert.Equal(BestStoriesOutcome.InvalidUpstreamData, result.Outcome);
        Assert.Empty(client.RequestedIds);
    }
}
