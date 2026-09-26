using System.Threading.RateLimiting;

namespace Infrastructure.HackerNews;

public sealed class HackerNewsConcurrencyGate : IAsyncDisposable
{
    private readonly ConcurrencyLimiter _limiter;

    public HackerNewsConcurrencyGate(HackerNewsOptions options)
    {
        _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = options.ConcurrencyPermitLimit,
            QueueLimit = options.ConcurrencyQueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    public async ValueTask<Lease> AcquireAsync(CancellationToken cancellationToken)
    {
        RateLimitLease lease = await _limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
        return new Lease(lease);
    }

    public ValueTask DisposeAsync() => _limiter.DisposeAsync();

    public readonly struct Lease : IDisposable
    {
        private readonly RateLimitLease? _lease;

        internal Lease(RateLimitLease lease) => _lease = lease;

        public bool IsAcquired => _lease?.IsAcquired == true;

        public void Dispose() => _lease?.Dispose();
    }
}
