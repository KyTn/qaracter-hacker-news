using System.Net;
using IntegrationTests.BestStories.Support;

namespace IntegrationTests.BestStories;

public sealed class BestStoriesValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("?n=")]
    [InlineData("?n=%20")]
    [InlineData("?n=abc")]
    [InlineData("?n=0")]
    [InlineData("?n=-1")]
    [InlineData("?n=101")]
    [InlineData("?n=999999999999999999999")]
    [InlineData("?n=1&n=2")]
    public async Task Invalid_n_returns_400_before_client_work(string query)
    {
        await using BestStoriesApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/best-stories{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, factory.HackerNews.FeedCalls);
        Assert.Equal(0, factory.HackerNews.ItemCalls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Boundary_n_values_are_accepted(int n)
    {
        await using BestStoriesApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/best-stories?n={n}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.HackerNews.FeedCalls);
    }
}
