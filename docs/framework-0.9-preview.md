# BotFramework 0.9-preview hardening

The 0.9 preview is intentionally breaking. There is no compatibility route for `/scopes/{scopeId}` and no public numeric `BalanceScopeId` contract. New integrations carry `TenantContext` with a tenant, scope, optional player, channel, request id, and correlation id.

## Tenant resolution

Telegram maps a chat to a tenant, a forum topic to a scope, and non-topic chats to `main`. Private chats receive a private tenant. Discord maps a guild to a tenant and a channel/thread to a scope; DMs receive a private tenant. REST requires trusted `tenant_id` and `scope_id` JWT claims and uses the string JWT `sub` as `PlayerId`.

## REST and clients

State-changing calls send `Idempotency-Key`. Responses are RFC 7807 documents with `code`, `correlationId`, and retry metadata. `BotFramework.Client` is the typed client boundary. Its generated low-level client is produced from `openapi-v1.json` by NSwag; run `dotnet build framework/BotFramework.Client -p:BotFrameworkGenerateClient=true` to regenerate it from another OpenAPI artifact.

## Distributed rate limiting

REST, Telegram, Discord, and the Host command bus use the same `IRateLimiter` contract. Redis evaluates tenant, tenant/player, tenant/route, tenant/player/route, and REST tenant/IP buckets atomically with a Lua token bucket. When Redis is unavailable, the Host uses a bounded local fallback with the configured policy values, reports degraded limiter telemetry, and retries Redis connection establishment after a backoff. Route keys are stable command/module identifiers; raw URLs and message payloads are not used as keys.

Deployment defaults live in configuration. Tenant/route overrides are stored in `rate_limit_policy_overrides`, resolved with route-over-channel precedence, cached in Redis, and invalidated after writes using a monotonically increasing tenant policy version. A resolved transport context is provisioned in PostgreSQL before module code runs; tenant, scope and channel-binding rows are created idempotently.

## Package consumer workflow

`eng/package-consumer-smoke.sh` packs framework packages into `.artifacts/local-feed` with .NET package validation/ApiCompat enabled, restores `samples/CoinFlip` with only `samples/CoinFlip/NuGet.config`, and runs the sample tests. It then installs `BotFramework.Templates` in an isolated `dotnet new` hive, generates all four channel/persistence surfaces, and restores/builds/tests the generated consumer. The sample has no Host or demo-game project references.

SDK 0.9 modules should use `TenantContext`, `RequestContextFactory.FromTenantContext`, and `TenantWalletEconomyEffect`. The older numeric game contracts remain only for the staged demo-game migration and are not part of the new tenant-aware module design.

## Operations

Compose provisions an OTLP Collector, Tempo, Prometheus alert rules, Grafana trace links, and Alertmanager's configurable generic webhook. Helm exposes `observability.otlpEndpoint` and optional Prometheus Operator resources. Metrics deliberately avoid tenant, scope, player, request, and correlation labels; those identifiers belong in traces and structured logs.

The REST transport pins `Microsoft.OpenApi` to the fixed 2.x security line. Package validation, public API inventories, generated-client verification, alert/config rendering, and package consumers run in CI.
