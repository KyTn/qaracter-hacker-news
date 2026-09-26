using System.Threading.RateLimiting;
using Application.BestStories;
using Host.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class BestStoriesServiceCollectionExtensions
{
    public const string RateLimitPolicy = "best-stories";

    public static IServiceCollection AddBestStories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BestStoriesOptions settings = configuration
            .GetSection(BestStoriesOptions.SectionName)
            .Get<BestStoriesOptions>() ?? new BestStoriesOptions();
        Validate(settings);

        services.AddSingleton<IValidateOptions<BestStoriesOptions>, BestStoriesOptionsValidator>();
        services.AddOptions<BestStoriesOptions>()
            .Bind(configuration.GetSection(BestStoriesOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<BestStoriesOptions>>().Value);
        services.AddScoped<IBestStoriesService, BestStoriesService>();
        services.AddProblemDetails();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddConcurrencyLimiter(RateLimitPolicy, limiter =>
            {
                limiter.PermitLimit = settings.EndpointPermitLimit;
                limiter.QueueLimit = settings.EndpointQueueLimit;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "The service is at capacity. Try again later.",
                    Type = "https://httpstatuses.com/429"
                }, options: null, contentType: "application/problem+json", cancellationToken);
            };
        });

        return services;
    }

    private static void Validate(BestStoriesOptions settings)
    {
        ValidateOptionsResult validation = new BestStoriesOptionsValidator().Validate(null, settings);
        if (validation.Failed)
            throw new OptionsValidationException(BestStoriesOptions.SectionName,
                typeof(BestStoriesOptions), validation.Failures);
    }
}
