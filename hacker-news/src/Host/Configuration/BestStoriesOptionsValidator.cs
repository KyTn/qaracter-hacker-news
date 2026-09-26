using Application.BestStories;
using Microsoft.Extensions.Options;

namespace Host.Configuration;

public sealed class BestStoriesOptionsValidator : IValidateOptions<BestStoriesOptions>
{
    public ValidateOptionsResult Validate(string? name, BestStoriesOptions options)
    {
        List<string> failures = [];
        if (options.ItemConcurrency is < 1 or > 16)
            failures.Add("BestStories:ItemConcurrency must be between 1 and 16.");
        if (options.EndpointPermitLimit < 1)
            failures.Add("BestStories:EndpointPermitLimit must be positive.");
        if (options.EndpointQueueLimit < 0)
            failures.Add("BestStories:EndpointQueueLimit cannot be negative.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
