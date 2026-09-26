using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Application.BestStories;

public sealed partial class BestStoriesService
{
    private static readonly ActivitySource ActivitySource = new("HackerNews.BestStories");
    private static readonly Meter Meter = new("HackerNews.BestStories");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("best_stories.duration", "ms");
    private static readonly Counter<long> Candidates =
        Meter.CreateCounter<long>("best_stories.candidates");

    private static void Record(
        long started,
        BestStoriesOutcome outcome,
        int requested,
        int examined,
        int returned,
        int skipped)
    {
        TagList tags = new()
        {
            { "outcome", outcome.ToString() }
        };
        Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
        Candidates.Add(examined, new KeyValuePair<string, object?>("kind", "examined"));
        Candidates.Add(returned, new KeyValuePair<string, object?>("kind", "returned"));
        Candidates.Add(skipped, new KeyValuePair<string, object?>("kind", "skipped"));

        Activity.Current?.SetTag("best_stories.requested", requested);
        Activity.Current?.SetTag("best_stories.examined", examined);
        Activity.Current?.SetTag("best_stories.returned", returned);
        Activity.Current?.SetTag("best_stories.skipped", skipped);
        Activity.Current?.SetTag("best_stories.outcome", outcome.ToString());
    }
}
