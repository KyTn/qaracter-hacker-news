# Quickstart: Hacker News Client

## Prerequisites

- .NET 10 SDK
- No Redis, database, or live Hacker News access is required for tests

## Restore and build

From the repository root:

```powershell
dotnet restore hacker-news/src/Host.slnx
dotnet build hacker-news/src/Host.slnx --no-restore
```

## Run tests

```powershell
dotnet test hacker-news/tests/UnitTests/UnitTests.csproj --no-build
dotnet test hacker-news/tests/IntegrationTests/IntegrationTests.csproj --no-build
dotnet test hacker-news/src/Host.slnx --no-build
```

The unit suite verifies application-owned models, invariants, validation, mapping, and
failure classification without HTTP or dependency-injection infrastructure.

The integration suite uses the controlled HTTP handler. A passing run demonstrates:

- Feed and item translation.
- Missing and malformed responses.
- Fresh cache hits and expiration.
- One population for concurrent same-key misses.
- Global concurrency never exceeding the configured limit.
- Retry eligibility and attempt bounds.
- Timeout, cancellation, circuit, and local throttling outcomes.
- Startup failure for invalid configuration.

## Configuration defaults

```json
{
  "HackerNews": {
    "BaseAddress": "https://hacker-news.firebaseio.com/",
    "FeedCacheTtl": "00:00:30",
    "ItemCacheTtl": "00:05:00",
    "MissingItemCacheTtl": "00:00:30",
    "MaximumPayloadBytes": 1048576,
    "MaximumCacheKeyLength": 128,
    "ConcurrencyPermitLimit": 16,
    "ConcurrencyQueueLimit": 64,
    "AttemptTimeout": "00:00:02",
    "TotalTimeout": "00:00:05",
    "RetryCount": 2,
    "InitialRetryDelay": "00:00:00.200"
  }
}
```

The implementation must bind and validate these settings during startup. Tests override
the base address/handler and timings; they never send traffic to the live origin.
