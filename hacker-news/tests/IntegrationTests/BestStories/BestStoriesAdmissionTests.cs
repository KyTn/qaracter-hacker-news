using System.Net;
using Application.BestStories;
using Application.HackerNews;
using IntegrationTests.BestStories.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.BestStories;

public sealed class BestStoriesAdmissionTests
{
    [Fact]
    public async Task Exhausted_endpoint_capacity_returns_429_without_starting_use_case()
    {
        Dictionary<string, string?> configuration = new()
        {
            ["BestStories:EndpointPermitLimit"] = "1",
            ["BestStories:EndpointQueueLimit"] = "0"
        };
        await using BestStoriesApiFactory factory = new(configuration);
        factory.HackerNews.FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1]);
        factory.HackerNews.ItemResults[1] = HackerNewsResult<HackerNewsItem>.Success(
            new HackerNewsItem(1, "story", "Story", null, "author", 1_700_000_000, 1, 0, false, false));
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.HackerNews.ItemGate = gate;
        using HttpClient firstClient = factory.CreateClient();
        using HttpClient secondClient = factory.CreateClient();
        BestStoriesOptions options = factory.Services.GetRequiredService<BestStoriesOptions>();
        Assert.Equal(1, options.EndpointPermitLimit);
        Assert.Equal(0, options.EndpointQueueLimit);
        Endpoint endpoint = Assert.Single(factory.Services.GetRequiredService<EndpointDataSource>().Endpoints,
            value => value is RouteEndpoint route && route.RoutePattern.RawText == "api/v1/best-stories");
        EnableRateLimitingAttribute attribute =
            Assert.IsType<EnableRateLimitingAttribute>(
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>());
        Assert.Equal(BestStoriesServiceCollectionExtensions.RateLimitPolicy, attribute.PolicyName);

        Task<HttpResponseMessage> admitted = Task.Run(() =>
            firstClient.GetAsync("/api/v1/best-stories?n=1"));
        for (int attempt = 0; attempt < 100 && factory.HackerNews.ItemCalls == 0; attempt++)
            await Task.Delay(10);
        Assert.Equal(1, factory.HackerNews.ItemCalls);

        HttpResponseMessage rejected = await secondClient.GetAsync("/api/v1/best-stories?n=1");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, factory.HackerNews.FeedCalls);
        Assert.Equal(1, factory.HackerNews.ItemCalls);
        gate.SetResult();
        Assert.Equal(HttpStatusCode.OK, (await admitted).StatusCode);
    }
}
