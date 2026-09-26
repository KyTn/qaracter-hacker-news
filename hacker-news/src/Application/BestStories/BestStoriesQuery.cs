namespace Application.BestStories;

/// <summary>A validated request for a bounded number of best stories.</summary>
public readonly record struct BestStoriesQuery
{
    public const int MinimumCount = 1;
    public const int MaximumCount = 100;

    public BestStoriesQuery(int count)
    {
        if (count is < MinimumCount or > MaximumCount)
            throw new ArgumentOutOfRangeException(nameof(count), count,
                $"Count must be between {MinimumCount} and {MaximumCount}.");

        Count = count;
    }

    public int Count { get; }
}
