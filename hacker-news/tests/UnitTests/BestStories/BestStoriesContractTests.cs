using Application.BestStories;

namespace UnitTests.BestStories;

public sealed class BestStoriesContractTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Query_accepts_inclusive_boundaries(int count) =>
        Assert.Equal(count, new BestStoriesQuery(count).Count);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void Query_rejects_out_of_range_values(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new BestStoriesQuery(count));

    [Fact]
    public void Success_requires_a_story_collection() =>
        Assert.Throws<ArgumentNullException>(() => BestStoriesResult.Success(null!));

    [Fact]
    public void Failure_cannot_use_success_outcome() =>
        Assert.Throws<ArgumentException>(() =>
            BestStoriesResult.Failure(BestStoriesOutcome.Success));
}
