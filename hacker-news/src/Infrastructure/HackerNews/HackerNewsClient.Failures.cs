using System.Diagnostics;
using Application.HackerNews;

namespace Infrastructure.HackerNews;

public sealed partial class HackerNewsClient
{
    private HackerNewsResult<T> MapFailure<T>(
        string operation,
        Exception exception,
        long started,
        CancellationToken callerToken)
    {
        HackerNewsOutcome outcome = exception switch
        {
            HackerNewsInvalidDataException => HackerNewsOutcome.InvalidUpstreamData,
            HackerNewsThrottledException => HackerNewsOutcome.Throttled,
            OperationCanceledException when callerToken.IsCancellationRequested => HackerNewsOutcome.Canceled,
            OperationCanceledException => HackerNewsOutcome.Timeout,
            HttpRequestException => HackerNewsOutcome.Unavailable,
            _ when exception.GetType().Name.Contains("TimeoutRejected", StringComparison.Ordinal) => HackerNewsOutcome.Timeout,
            _ when exception.GetType().Name.Contains("BrokenCircuit", StringComparison.Ordinal) => HackerNewsOutcome.Unavailable,
            _ => HackerNewsOutcome.Unavailable
        };

        RecordFailure(operation, outcome, exception, started);
        return HackerNewsResult<T>.Failure(outcome, ReasonCode(outcome));
    }

    private static string ReasonCode(HackerNewsOutcome outcome) => outcome switch
    {
        HackerNewsOutcome.InvalidUpstreamData => "invalid-upstream-data",
        HackerNewsOutcome.Throttled => "local-throttling",
        HackerNewsOutcome.Canceled => "caller-canceled",
        HackerNewsOutcome.Timeout => "upstream-timeout",
        _ => "upstream-unavailable"
    };
}
