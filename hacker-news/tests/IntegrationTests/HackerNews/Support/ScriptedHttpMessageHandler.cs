using System.Collections.Concurrent;

namespace IntegrationTests.HackerNews.Support;

internal sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _handler;
    private int _active;
    private int _requestCount;
    private int _maximumActive;

    public ScriptedHttpMessageHandler(
        Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> handler) =>
        _handler = handler;

    public int RequestCount => Volatile.Read(ref _requestCount);
    public int MaximumActive => Volatile.Read(ref _maximumActive);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        int requestNumber = Interlocked.Increment(ref _requestCount);
        int active = Interlocked.Increment(ref _active);
        UpdateMaximum(active);
        try
        {
            return await _handler(request, requestNumber, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    private void UpdateMaximum(int value)
    {
        int current;
        do
        {
            current = MaximumActive;
            if (value <= current) return;
        }
        while (Interlocked.CompareExchange(ref _maximumActive, value, current) != current);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
