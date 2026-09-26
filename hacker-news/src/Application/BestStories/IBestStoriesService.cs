namespace Application.BestStories;

public interface IBestStoriesService
{
    ValueTask<BestStoriesResult> GetAsync(
        BestStoriesQuery query,
        CancellationToken cancellationToken = default);
}
