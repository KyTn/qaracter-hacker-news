# Quickstart: Best Stories GET Endpoint

## Prerequisites

- .NET 10 SDK
- Specification 001 client implementation registered in the Host
- No database, Redis, or live Hacker News access is required for tests

## Restore, build, and test

From the outer repository root:

```powershell
dotnet restore hacker-news/src/Host.slnx
dotnet build hacker-news/src/Host.slnx --no-restore
dotnet test hacker-news/tests/UnitTests/UnitTests.csproj --no-build
dotnet test hacker-news/tests/IntegrationTests/IntegrationTests.csproj --no-build
dotnet test hacker-news/src/Host.slnx --no-build
```

The unit suite verifies validation, progressive batching, deduplication, filtering, time
conversion, mapping, stable ordering, stop conditions, and application outcome classification.
The integration suite uses a controlled upstream and verifies the public route, JSON schema,
Problem Details, OpenAPI, rate limiting, cancellation, cache reuse, same-key coalescing, and
the global Hacker News concurrency bound. Tests never call the live API.

## Run and call

```powershell
dotnet run --project hacker-news/src/Host/Api.csproj
```

```http
GET /api/v1/best-stories?n=10
Accept: application/json
```

## Configuration defaults

```json
{
  "BestStories": {
    "ItemConcurrency": 8,
    "EndpointPermitLimit": 100,
    "EndpointQueueLimit": 200
  }
}
```

These values are validated during startup. `ItemConcurrency` must be 1..16, endpoint permits
must be positive, and the queue limit must be non-negative. The public `n` maximum is fixed at
100. Hacker News cache, resilience, and process-wide concurrency settings remain those defined
and validated by specification 001.

