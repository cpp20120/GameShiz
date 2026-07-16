# Changelog

## 0.9.0-preview.1

Breaking preview release. Public REST now uses `/api/v1/tenants/{tenantId}/scopes/{scopeId}/{module}`. SDK consumers use opaque `TenantId`, `ScopeId`, `PlayerId`, and `RequestId`, scoped `TenantContext`, stable RFC 7807 error codes, string idempotency keys, and tenant-aware gRPC metadata.

The release also adds the package-only CoinFlip sample, the `BotFramework.Templates` `dotnet new botframework-game` scaffold, Redis-backed multi-dimensional rate limiting with bounded local fallback, and OpenTelemetry Collector/Tempo/Alertmanager deployment defaults.

The transport hardening slice now wires that limiter through standalone Telegram and Discord BFFs and the command bus, restores scoped tenant context for channel handlers, and allows bounded local buckets to reconnect to Redis after recovery.

The long-running workflow slice adds `BotFramework.Host.Workflows`: a generic
durable command/step boundary backed by PostgreSQL and Wolverine. It provides
correlation, partitioned delivery, retries, idempotent command ids, operator
replay, and generic saga state without putting saga logic into the SDK or
replacing the local AtomicEffect transaction model.

Meta tournaments now consume that boundary. Wallet mutations use stable
operation ids and explicit rollback mutations for definitive local rejection;
transient failures remain pending and are retried. The new
`035_durable_workflow_steps` migration belongs to the Backend database only.
The CoinFlip workflow sample and focused workflow tests document the consumer
boundary and keep the sample independent from the demo game's database.

See the [complete 0.9.0-preview.1 release notes](docs/releases/0.9.0-preview.1.md)
and the [framework publishing guide](docs/framework-release.md).
