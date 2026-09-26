using System.Diagnostics;
using System.Diagnostics.Metrics;
using Application.HackerNews;
using Microsoft.Extensions.Logging;

namespace Infrastructure.HackerNews;

public sealed partial class HackerNewsClient
{
    private static readonly Meter Meter = new("Qaracter.HackerNews");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("hacker_news.operations");
    private static readonly Counter<long> CachePopulations = Meter.CreateCounter<long>("hacker_news.cache_populations");
    private static readonly Counter<long> UpstreamCalls = Meter.CreateCounter<long>("hacker_news.upstream_calls");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("hacker_news.duration", "ms");

    private void RecordSuccess(string operation, long started) =>
        RecordOutcome(operation, HackerNewsOutcome.Success, started);

    private void RecordCachePopulation(string operation)
    {
        CachePopulations.Add(1, new KeyValuePair<string, object?>("operation", operation));
        logger.LogDebug("Populating Hacker News cache for {Operation}", operation);
    }

    private void RecordUpstreamCall(string operation)
    {
        UpstreamCalls.Add(1, new KeyValuePair<string, object?>("operation", operation));
        logger.LogDebug("Calling Hacker News for {Operation}", operation);
    }

    private void RecordOutcome(string operation, HackerNewsOutcome outcome, long started)
    {
        TagList tags = default;
        tags.Add("operation", operation);
        tags.Add("outcome", outcome.ToString());
        Operations.Add(1, tags);
        Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
        logger.LogInformation("Hacker News {Operation} completed with {Outcome}", operation, outcome);
    }

    private void RecordFailure(string operation, HackerNewsOutcome outcome, Exception exception, long started)
    {
        RecordOutcome(operation, outcome, started);
        logger.LogWarning("Hacker News {Operation} failed with {Outcome} ({ExceptionType})", operation, outcome, exception.GetType().Name);
    }
}
