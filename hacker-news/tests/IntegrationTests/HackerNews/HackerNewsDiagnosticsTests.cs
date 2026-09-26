using Application.HackerNews;
using Infrastructure.HackerNews;
using IntegrationTests.HackerNews.Support;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.HackerNews;

public sealed class HackerNewsDiagnosticsTests
{
    [Fact]
    public async Task Logs_do_not_contain_payload_or_internal_url()
    {
        const string secretPayload = "secret-payload-value";
        CapturingLogger logger = new();
        ScriptedHttpMessageHandler handler = new((_, _, _) =>
            Task.FromResult(ScriptedHttpMessageHandler.Json(secretPayload)));
        await using HackerNewsClientFixture fixture = new(handler, logger: logger);

        await fixture.Client.GetBestStoryIdsAsync();

        string combined = string.Join(Environment.NewLine, logger.Messages);
        Assert.DoesNotContain(secretPayload, combined, StringComparison.Ordinal);
        Assert.DoesNotContain("hacker-news.firebaseio.com", combined, StringComparison.Ordinal);
    }

    private sealed class CapturingLogger : ILogger<HackerNewsClient>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
