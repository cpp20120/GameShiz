# Changelog

## 0.9.0-preview.1

Breaking preview release. Public REST now uses `/api/v1/tenants/{tenantId}/scopes/{scopeId}/{module}`. SDK consumers use opaque `TenantId`, `ScopeId`, `PlayerId`, and `RequestId`, scoped `TenantContext`, stable RFC 7807 error codes, string idempotency keys, and tenant-aware gRPC metadata.

The release also adds the package-only CoinFlip sample, the `BotFramework.Templates` `dotnet new botframework-game` scaffold, Redis-backed multi-dimensional rate limiting with bounded local fallback, and OpenTelemetry Collector/Tempo/Alertmanager deployment defaults.

The transport hardening slice now wires that limiter through standalone Telegram and Discord BFFs and the command bus, restores scoped tenant context for channel handlers, and allows bounded local buckets to reconnect to Redis after recovery.
