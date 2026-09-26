using Microsoft.Extensions.Options;

namespace Infrastructure.HackerNews;

public sealed class HackerNewsOptionsValidator : IValidateOptions<HackerNewsOptions>
{
    public ValidateOptionsResult Validate(string? name, HackerNewsOptions options)
    {
        List<string> errors = [];

        if (!options.BaseAddress.IsAbsoluteUri || options.BaseAddress.Scheme != Uri.UriSchemeHttps)
            errors.Add("BaseAddress must be an absolute HTTPS URI.");
        if (options.FeedCacheTtl <= TimeSpan.Zero) errors.Add("FeedCacheTtl must be positive.");
        if (options.ItemCacheTtl <= TimeSpan.Zero) errors.Add("ItemCacheTtl must be positive.");
        if (options.MissingItemCacheTtl <= TimeSpan.Zero || options.MissingItemCacheTtl > options.ItemCacheTtl)
            errors.Add("MissingItemCacheTtl must be positive and no greater than ItemCacheTtl.");
        if (options.MaximumPayloadBytes is < 1024 or > 16_777_216) errors.Add("MaximumPayloadBytes is outside the supported range.");
        if (options.MaximumCacheKeyLength < 32) errors.Add("MaximumCacheKeyLength must be at least 32.");
        if (options.ConcurrencyPermitLimit <= 0) errors.Add("ConcurrencyPermitLimit must be positive.");
        if (options.ConcurrencyQueueLimit < 0) errors.Add("ConcurrencyQueueLimit cannot be negative.");
        if (options.AttemptTimeout <= TimeSpan.Zero || options.AttemptTimeout >= options.TotalTimeout)
            errors.Add("AttemptTimeout must be positive and less than TotalTimeout.");
        if (options.RetryCount is < 0 or > 2) errors.Add("RetryCount must be between zero and two.");
        if (options.InitialRetryDelay <= TimeSpan.Zero) errors.Add("InitialRetryDelay must be positive.");
        if (options.CircuitBreakerFailureRatio is <= 0 or > 1) errors.Add("CircuitBreakerFailureRatio must be in (0, 1].");
        if (options.CircuitBreakerMinimumThroughput <= 0) errors.Add("CircuitBreakerMinimumThroughput must be positive.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
