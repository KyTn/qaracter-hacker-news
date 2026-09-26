using Application.BestStories;
using Application.HackerNews;
using UnitTests.BestStories.Support;

namespace UnitTests.BestStories;

public sealed class BestStoriesSelectionTests
{
    [Fact]
    public async Task Selects_distinct_retrievable_stories_and_maps_optional_values()
    {
        FakeHackerNewsClient client = new()
        {
            FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1, 1, 2, 3, 4])
        };
        client.ItemResults[1] = Success(Item(1, score: 5, url: null, descendants: null));
        client.ItemResults[2] = Success(Item(2, score: 99, deleted: true));
        client.ItemResults[3] = HackerNewsResult<HackerNewsItem>.Failure(HackerNewsOutcome.NotFound);
        client.ItemResults[4] = Success(Item(4, score: 10));

        BestStoriesResult result = await Service(client, concurrency: 2)
            .GetAsync(new BestStoriesQuery(2));

        Assert.Equal(BestStoriesOutcome.Success, result.Outcome);
        IReadOnlyList<BestStory> stories = result.Stories!;
        Assert.Equal([10L, 5L], stories.Select(story => story.Score));
        Assert.Null(stories[1].Uri);
        Assert.Equal(0, stories[1].CommentCount);
        Assert.Equal(new[] { 1L, 2L, 3L, 4L }, client.RequestedIds);
    }

    [Fact]
    public async Task Skips_invalid_time_and_non_story_items()
    {
        FakeHackerNewsClient client = new()
        {
            FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1, 2, 3])
        };
        client.ItemResults[1] = Success(Item(1, unixTime: long.MaxValue));
        client.ItemResults[2] = Success(Item(2, type: "job"));
        client.ItemResults[3] = Success(Item(3));

        BestStoriesResult result = await Service(client, concurrency: 1)
            .GetAsync(new BestStoriesQuery(1));

        Assert.Single(result.Stories!);
        Assert.Equal("Story 3", result.Stories![0].Title);
    }

    internal static HackerNewsItem Item(
        long id,
        long score = 1,
        string? type = "story",
        string? url = "https://example.test/story",
        long? descendants = 3,
        bool deleted = false,
        bool dead = false,
        long? unixTime = 1_700_000_000) =>
        new(id, type, $"Story {id}", url, "author", unixTime, score, descendants, deleted, dead);

    internal static HackerNewsResult<HackerNewsItem> Success(HackerNewsItem item) =>
        HackerNewsResult<HackerNewsItem>.Success(item);

    internal static BestStoriesService Service(FakeHackerNewsClient client, int concurrency = 8) =>
        new(client, new BestStoriesOptions { ItemConcurrency = concurrency });
}
