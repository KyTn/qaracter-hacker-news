namespace Application.BestStories;

public enum BestStoriesOutcome
{
    Success,
    InvalidInput,
    InvalidUpstreamData,
    Timeout,
    Unavailable,
    Throttled,
    Canceled
}

/// <summary>A closed, transport-independent outcome for the best-stories use case.</summary>
public sealed record BestStoriesResult
{
    private BestStoriesResult(
        BestStoriesOutcome outcome,
        IReadOnlyList<BestStory>? stories,
        string? reasonCode)
    {
        if (outcome == BestStoriesOutcome.Success && stories is null)
            throw new ArgumentNullException(nameof(stories));
        if (outcome != BestStoriesOutcome.Success && stories is not null)
            throw new ArgumentException("Failures cannot contain stories.", nameof(stories));

        Outcome = outcome;
        Stories = stories;
        ReasonCode = reasonCode;
    }

    public BestStoriesOutcome Outcome { get; }
    public IReadOnlyList<BestStory>? Stories { get; }
    public string? ReasonCode { get; }
    public bool IsSuccess => Outcome == BestStoriesOutcome.Success;

    public static BestStoriesResult Success(IReadOnlyList<BestStory> stories) =>
        new(BestStoriesOutcome.Success, stories, null);

    public static BestStoriesResult Failure(BestStoriesOutcome outcome, string? reasonCode = null)
    {
        if (outcome == BestStoriesOutcome.Success)
            throw new ArgumentException("Use Success to create a successful result.", nameof(outcome));

        return new BestStoriesResult(outcome, null, reasonCode);
    }
}
