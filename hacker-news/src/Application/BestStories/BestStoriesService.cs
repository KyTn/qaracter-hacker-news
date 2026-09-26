using System.Diagnostics;
using Application.HackerNews;

namespace Application.BestStories;

public sealed partial class BestStoriesService(
    IHackerNewsClient client,
    BestStoriesOptions options) : IBestStoriesService
{
    /// <summary>
    /// Gets the asynchronous.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public async ValueTask<BestStoriesResult> GetAsync(
            BestStoriesQuery query,
            CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        using Activity? activity = ActivitySource.StartActivity("best-stories.get");
        int examined = 0;
        int skipped = 0;

        HackerNewsResult<IReadOnlyList<long>> feed =
            await client.GetBestStoryIdsAsync(cancellationToken).ConfigureAwait(false);
        if (!feed.IsSuccess)
            return Finish(MapFailure(feed.Outcome, feed.ReasonCode), started, query.Count, examined, 0, skipped);

        // Stores the id and the position in the feed, so that we can sort the results by score and then by position in the feed.
        List<(long Id, int Position)> candidates = [];

        HashSet<long> seen = [];
        IReadOnlyList<long> ids = feed.Value!;
        for (int position = 0; position < ids.Count; position++)
        {
            long id = ids[position];
            if (id > 0 && seen.Add(id))
                candidates.Add((id, position));
        }

        List<RankedStory> collected = [];
        // Evaluate candidates in batches of ItemConcurrency, until we have enough stories or we have evaluated all candidates.
        for (int offset = 0; offset < candidates.Count && collected.Count < query.Count; offset += options.ItemConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(options.ItemConcurrency, candidates.Count - offset);

            // Evaluate candidates in parallel, but preserve the order of the results by position in the feed.
            Task<CandidateEvaluation>[] tasks = new Task<CandidateEvaluation>[count];

            for (int index = 0; index < count; index++)
            {
                (long id, int position) = candidates[offset + index];
                tasks[index] = EvaluateAsync(id, position, cancellationToken);
            }

            CandidateEvaluation[] evaluations;
            try
            {
                evaluations = await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Finish(BestStoriesResult.Failure(BestStoriesOutcome.Canceled, "caller_canceled"),
                    started, query.Count, examined, 0, skipped);
            }

            foreach (CandidateEvaluation evaluation in evaluations.OrderBy(value => value.Position))
            {
                examined++;
                if (evaluation.Failure is not null)
                    return Finish(evaluation.Failure, started, query.Count, examined, 0, skipped);
                if (evaluation.Story is null)
                {
                    skipped++;
                    continue;
                }

                collected.Add(evaluation.Story);
                if (collected.Count == query.Count)
                    break;
            }
        }

        BestStory[] stories = collected
            .Take(query.Count)
            .OrderByDescending(value => value.Story.Score)
            .ThenBy(value => value.Position)
            .ThenBy(value => value.Id)
            .Select(value => value.Story)
            .ToArray();

        return Finish(BestStoriesResult.Success(stories), started, query.Count, examined,
            stories.Length, skipped);
    }

    private async Task<CandidateEvaluation> EvaluateAsync(
        long id,
        int position,
        CancellationToken cancellationToken)
    {
        HackerNewsResult<HackerNewsItem> result =
            await client.GetItemAsync(id, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            HackerNewsItem item = result.Value!;
            if (!IsRetrievable(item) || !TryMap(item, out BestStory story))
                return CandidateEvaluation.Skipped(position);

            return CandidateEvaluation.Found(new RankedStory(id, position, story));
        }

        return result.Outcome switch
        {
            HackerNewsOutcome.NotFound or HackerNewsOutcome.InvalidInput or
                HackerNewsOutcome.InvalidUpstreamData => CandidateEvaluation.Skipped(position),
            _ => CandidateEvaluation.Failed(position, MapFailure(result.Outcome, result.ReasonCode))
        };
    }

    private static bool IsRetrievable(HackerNewsItem item) =>
        string.Equals(item.Type, "story", StringComparison.OrdinalIgnoreCase) &&
        !item.IsDeleted && !item.IsDead &&
        !string.IsNullOrWhiteSpace(item.Title) &&
        !string.IsNullOrWhiteSpace(item.By) &&
        item.UnixTime.HasValue && item.Score.HasValue;

    private static bool TryMap(HackerNewsItem item, out BestStory story)
    {
        try
        {
            DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(item.UnixTime!.Value);
            story = new BestStory(
                item.Title!,
                item.Url,
                item.By!,
                time.ToUniversalTime(),
                item.Score!.Value,
                item.Descendants ?? 0);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            story = null!;
            return false;
        }
    }

    private static BestStoriesResult MapFailure(HackerNewsOutcome outcome, string? reasonCode) =>
        outcome switch
        {
            HackerNewsOutcome.InvalidInput => BestStoriesResult.Failure(BestStoriesOutcome.InvalidInput, reasonCode),
            HackerNewsOutcome.InvalidUpstreamData => BestStoriesResult.Failure(BestStoriesOutcome.InvalidUpstreamData, reasonCode),
            HackerNewsOutcome.Timeout => BestStoriesResult.Failure(BestStoriesOutcome.Timeout, reasonCode),
            HackerNewsOutcome.Throttled => BestStoriesResult.Failure(BestStoriesOutcome.Throttled, reasonCode),
            HackerNewsOutcome.Canceled => BestStoriesResult.Failure(BestStoriesOutcome.Canceled, reasonCode),
            _ => BestStoriesResult.Failure(BestStoriesOutcome.Unavailable, reasonCode)
        };

    private static BestStoriesResult Finish(
        BestStoriesResult result,
        long started,
        int requested,
        int examined,
        int returned,
        int skipped)
    {
        Record(started, result.Outcome, requested, examined, returned, skipped);
        return result;
    }

    private sealed record RankedStory(long Id, int Position, BestStory Story);

    private sealed record CandidateEvaluation(
        int Position,
        RankedStory? Story,
        BestStoriesResult? Failure)
    {
        public static CandidateEvaluation Found(RankedStory story) =>
            new(story.Position, story, null);

        public static CandidateEvaluation Skipped(int position) => new(position, null, null);

        public static CandidateEvaluation Failed(int position, BestStoriesResult failure) =>
            new(position, null, failure);
    }
}
