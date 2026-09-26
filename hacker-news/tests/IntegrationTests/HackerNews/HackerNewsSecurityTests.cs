using Application.HackerNews;
using IntegrationTests.HackerNews.Support;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsSecurityTests
{
    [Fact]
    public async Task Redirect_response_is_not_followed_by_the_client_contract()
    {
        ScriptedHttpMessageHandler handler = new((_, _, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("https://untrusted.example/") }
            }));
        await using HackerNewsClientFixture fixture = new(handler);

        HackerNewsResult<IReadOnlyList<long>> result = await fixture.Client.GetBestStoryIdsAsync();

        Assert.Equal(HackerNewsOutcome.Unavailable, result.Outcome);
        Assert.Equal(1, handler.RequestCount);
    }
}
