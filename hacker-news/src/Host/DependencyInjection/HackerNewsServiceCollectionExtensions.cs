using Application.HackerNews;
using Infrastructure.HackerNews;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Microsoft.Extensions.DependencyInjection;

public static class HackerNewsServiceCollectionExtensions
{
    public static IServiceCollection AddHackerNewsIntegration(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<HttpMessageHandler>? primaryHandlerFactory = null)
    {
        services.AddSingleton<IValidateOptions<HackerNewsOptions>, HackerNewsOptionsValidator>();
        services.AddOptions<HackerNewsOptions>()
            .Bind(configuration.GetSection(HackerNewsOptions.SectionName))
            .ValidateOnStart();

        HackerNewsOptions settings = configuration
            .GetSection(HackerNewsOptions.SectionName)
            .Get<HackerNewsOptions>() ?? new HackerNewsOptions();
        ValidateSettings(settings);

        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = settings.MaximumPayloadBytes;
            options.MaximumKeyLength = settings.MaximumCacheKeyLength;
        });
        services.AddSingleton(sp => new HackerNewsConcurrencyGate(
            sp.GetRequiredService<IOptions<HackerNewsOptions>>().Value));

        services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((sp, client) =>
            {
                HackerNewsOptions options = sp.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandlerFactory?.Invoke() ??
                new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = settings.AttemptTimeout;
                options.TotalRequestTimeout.Timeout = settings.TotalTimeout;
                options.Retry.MaxRetryAttempts = Math.Max(1, settings.RetryCount);
                if (settings.RetryCount == 0)
                    options.Retry.ShouldHandle = static _ => ValueTask.FromResult(false);
                options.Retry.Delay = settings.InitialRetryDelay;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.CircuitBreaker.FailureRatio = settings.CircuitBreakerFailureRatio;
                options.CircuitBreaker.MinimumThroughput = settings.CircuitBreakerMinimumThroughput;
                options.CircuitBreaker.SamplingDuration = settings.CircuitBreakerSamplingDuration;
                options.CircuitBreaker.BreakDuration = settings.CircuitBreakerBreakDuration;
            });

        return services;
    }

    private static void ValidateSettings(HackerNewsOptions settings)
    {
        ValidateOptionsResult validation = new HackerNewsOptionsValidator().Validate(null, settings);
        if (validation.Failed)
            throw new OptionsValidationException(HackerNewsOptions.SectionName, typeof(HackerNewsOptions), validation.Failures);
    }
}
