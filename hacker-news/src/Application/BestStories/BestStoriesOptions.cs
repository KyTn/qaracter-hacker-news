namespace Application.BestStories;

public sealed class BestStoriesOptions
{
    public const string SectionName = "BestStories";

    public int ItemConcurrency { get; set; } = 8;
    public int EndpointPermitLimit { get; set; } = 100;
    public int EndpointQueueLimit { get; set; } = 200;
}
