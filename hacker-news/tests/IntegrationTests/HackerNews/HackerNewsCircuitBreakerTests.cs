using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsCircuitBreakerTests
{
    [Fact]
    public async Task Circuit_rejects_after_configured_failure_sample()
    {
        ScriptedHttpMessageHandler handler = new((_, attempt, _) => Task.FromResult(
            attempt <= 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : ScriptedHttpMessageHandler.Json("{\"id\":4}")));
        await using ResilientClientFixture fixture = new(handler, new Dictionary<string, string?>
        {
            ["HackerNews:RetryCount"] = "0",
            ["HackerNews:CircuitBreakerMinimumThroughput"] = "2",
            ["HackerNews:CircuitBreakerFailureRatio"] = "0.5"
        });

        await fixture.Client.GetItemAsync(1);
        await fixture.Client.GetItemAsync(2);
        HackerNewsResult<HackerNewsItem> rejected = await fixture.Client.GetItemAsync(3);

        Assert.Equal(HackerNewsOutcome.Unavailable, rejected.Outcome);
        Assert.Equal(2, handler.RequestCount);

        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        HackerNewsResult<HackerNewsItem> recovered = await fixture.Client.GetItemAsync(4);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(3, handler.RequestCount);
    }
}
