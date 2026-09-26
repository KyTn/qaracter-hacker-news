using System.Globalization;
using Application.BestStories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Host.Controllers;

[ApiController]
[Route("api/v1/best-stories")]
[EnableRateLimiting(BestStoriesServiceCollectionExtensions.RateLimitPolicy)]
public sealed class BestStoriesController(IBestStoriesService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BestStory>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<IReadOnlyList<BestStory>>> Get(
        [FromQuery(Name = "n")] string? n,
        CancellationToken cancellationToken)
    {
        _ = n;
        if (!Request.Query.TryGetValue("n", out Microsoft.Extensions.Primitives.StringValues values) ||
            values.Count != 1 ||
            !int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
            count is < BestStoriesQuery.MinimumCount or > BestStoriesQuery.MaximumCount)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid story count",
                detail: $"Query parameter 'n' must occur once and be an integer between {BestStoriesQuery.MinimumCount} and {BestStoriesQuery.MaximumCount}.");
        }

        BestStoriesResult result = await service
            .GetAsync(new BestStoriesQuery(count), cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome switch
        {
            BestStoriesOutcome.Success => Ok(result.Stories),
            BestStoriesOutcome.InvalidInput => Failure(StatusCodes.Status400BadRequest, "Invalid request"),
            BestStoriesOutcome.InvalidUpstreamData => Failure(StatusCodes.Status502BadGateway, "Invalid upstream response"),
            BestStoriesOutcome.Timeout => Failure(StatusCodes.Status504GatewayTimeout, "Upstream timeout"),
            BestStoriesOutcome.Unavailable or BestStoriesOutcome.Throttled =>
                Failure(StatusCodes.Status503ServiceUnavailable, "Upstream unavailable"),
            BestStoriesOutcome.Canceled when cancellationToken.IsCancellationRequested =>
                throw new OperationCanceledException(cancellationToken),
            _ => Failure(StatusCodes.Status503ServiceUnavailable, "Request could not be completed")
        };
    }

    private ObjectResult Failure(int status, string title) => Problem(
        statusCode: status,
        title: title,
        detail: "The request could not be completed safely. Try again later.");
}
