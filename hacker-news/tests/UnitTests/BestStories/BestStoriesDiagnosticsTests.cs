using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Application.BestStories;
using Application.HackerNews;
using UnitTests.BestStories.Support;
using static UnitTests.BestStories.BestStoriesSelectionTests;

namespace UnitTests.BestStories;

public sealed class BestStoriesDiagnosticsTests
{
    [Fact]
    public async Task Emits_low_cardinality_metrics_and_activity_counts()
    {
        ConcurrentBag<string> measurements = [];
        using MeterListener meterListener = new();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "HackerNews.BestStories")
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            measurements.Add(instrument.Name));
        meterListener.SetMeasurementEventCallback<long>((instrument, _, _, _) =>
            measurements.Add(instrument.Name));
        meterListener.Start();

        Activity? stopped = null;
        using ActivityListener activityListener = new()
        {
            ShouldListenTo = source => source.Name == "HackerNews.BestStories",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped = activity
        };
        ActivitySource.AddActivityListener(activityListener);

        FakeHackerNewsClient client = new()
        {
            FeedResult = HackerNewsResult<IReadOnlyList<long>>.Success([1])
        };
        client.ItemResults[1] = Success(Item(1));

        await Service(client).GetAsync(new BestStoriesQuery(1));

        Assert.Contains("best_stories.duration", measurements);
        Assert.Contains("best_stories.candidates", measurements);
        Assert.NotNull(stopped);
        Assert.Equal(1, stopped!.GetTagItem("best_stories.requested"));
        Assert.Equal("Success", stopped.GetTagItem("best_stories.outcome"));
    }
}
