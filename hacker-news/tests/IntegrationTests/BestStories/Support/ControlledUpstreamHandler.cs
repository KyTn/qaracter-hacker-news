using System.Net;
using System.Text;

namespace IntegrationTests.BestStories.Support;

internal sealed class ControlledUpstreamHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    private int _active;
    private int _maximumActive;
    private int _requestCount;

    public int RequestCount => Volatile.Read(ref _requestCount);
    public int MaximumActive => Volatile.Read(ref _maximumActive);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        int active = Interlocked.Increment(ref _active);
        int observed;
        do
        {
            observed = Volatile.Read(ref _maximumActive);
        } while (active > observed &&
                 Interlocked.CompareExchange(ref _maximumActive, active, observed) != observed);

        try
        {
            return await responder(request, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    public static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
