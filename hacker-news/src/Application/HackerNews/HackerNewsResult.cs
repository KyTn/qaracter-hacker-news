namespace Application.HackerNews;

/// <summary>Classifies every possible result of a Hacker News dependency operation.</summary>
public enum HackerNewsOutcome
{
    Success,
    NotFound,
    InvalidInput,
    InvalidUpstreamData,
    Timeout,
    Unavailable,
    Throttled,
    Canceled
}

/// <summary>Represents either a successful dependency value or a safe classified failure.</summary>
public sealed record HackerNewsResult<T>
{
    private HackerNewsResult(HackerNewsOutcome outcome, T? value, string? reasonCode)
    {
        if (outcome == HackerNewsOutcome.Success && value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Outcome = outcome;
        Value = value;
        ReasonCode = reasonCode;
    }

    /// <summary>Gets the classified operation outcome.</summary>
    public HackerNewsOutcome Outcome { get; }

    /// <summary>Gets the value when <see cref="Outcome"/> is <see cref="HackerNewsOutcome.Success"/>.</summary>
    public T? Value { get; }

    /// <summary>Gets a stable safe reason code for a failed operation.</summary>
    public string? ReasonCode { get; }

    /// <summary>Gets whether this result contains a successful value.</summary>
    public bool IsSuccess => Outcome == HackerNewsOutcome.Success;

    public static HackerNewsResult<T> Success(T value) =>
        new(HackerNewsOutcome.Success, value, null);

    public static HackerNewsResult<T> Failure(HackerNewsOutcome outcome, string? reasonCode = null)
    {
        if (outcome == HackerNewsOutcome.Success)
        {
            throw new ArgumentException("Use Success to create a successful result.", nameof(outcome));
        }

        return new(outcome, default, reasonCode);
    }
}
