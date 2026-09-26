using Application.HackerNews;

namespace UnitTests.HackerNews;

public sealed class HackerNewsResultTests
{
    [Fact]
    public void Success_contains_value()
    {
        HackerNewsResult<int> result = HackerNewsResult<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(HackerNewsOutcome.Success, result.Outcome);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_contains_no_value_and_keeps_reason()
    {
        HackerNewsResult<int> result = HackerNewsResult<int>.Failure(
            HackerNewsOutcome.Timeout,
            "upstream-timeout");

        Assert.False(result.IsSuccess);
        Assert.Equal(HackerNewsOutcome.Timeout, result.Outcome);
        Assert.Equal("upstream-timeout", result.ReasonCode);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void Failure_rejects_success_outcome() =>
        Assert.Throws<ArgumentException>(() =>
            HackerNewsResult<int>.Failure(HackerNewsOutcome.Success));

    [Fact]
    public void Success_rejects_null() =>
        Assert.Throws<ArgumentNullException>(() =>
            HackerNewsResult<string>.Success(null!));
}
