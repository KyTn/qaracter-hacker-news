using System.Net;
using Application.HackerNews;
using IntegrationTests.BestStories.Support;

namespace IntegrationTests.BestStories;

public sealed class BestStoriesFailureMappingTests
{
    [Theory]
    [InlineData(HackerNewsOutcome.InvalidUpstreamData, HttpStatusCode.BadGateway)]
    [InlineData(HackerNewsOutcome.Timeout, HttpStatusCode.GatewayTimeout)]
    [InlineData(HackerNewsOutcome.Unavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HackerNewsOutcome.Throttled, HttpStatusCode.ServiceUnavailable)]
    public async Task Feed_failures_map_to_safe_problem_details(
        HackerNewsOutcome outcome,
        HttpStatusCode expectedStatus)
    {
        await using BestStoriesApiFactory factory = new();
        factory.HackerNews.FeedResult =
            HackerNewsResult<IReadOnlyList<long>>.Failure(outcome, "safe_code");
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/best-stories?n=1");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("safe_code", body, StringComparison.Ordinal);
        Assert.DoesNotContain("hacker-news.firebaseio.com", body, StringComparison.Ordinal);
    }
}
