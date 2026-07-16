# CoinFlip external module

This sample is intentionally outside `framework/` and `games/`. Its projects consume only packed `BotFramework.*` packages from `../../.artifacts/local-feed`; there are no project references to Host or the demonstration games.

Build the package consumer smoke test from the repository root:

```bash
./eng/package-consumer-smoke.sh
```

The sample demonstrates a pure domain, an atomic application service, an isolated state store, and separate REST, Telegram, and Discord adapters. Every adapter resolves a `TenantContext` before invoking the game.

## Durable workflow consumer

`CoinFlip.Workflow` is the runtime adapter example for the framework's
`BotFramework.Host.Workflows` API. It is source-referenced because
`BotFramework.Host` is a composition/runtime project, while the other sample
projects remain package-only consumers.

`CoinFlip.Workflow.Tests` verifies the handler contract without requiring a
PostgreSQL server; the framework integration itself is configured by the host
composition root.

```csharp
builder.AddDurableWorkflows(typeof(CoinFlipWorkflowHandler).Assembly);
```

The command implements `IDurableWorkflowCommand`; the handler uses
`IDurableWorkflowStepExecutor`; the application boundary uses
`IDurableWorkflowDispatcher`. The workflow table is only a recovery/audit
projection. CoinFlip's domain state must still be persisted by the owning
Backend transaction/effect in a real deployment.

The workflow uses the current service's `ConnectionStrings:Postgres`. In the
microservices profile that is `backend-postgres/backend`, not the Wallet or
Identity database. Wallet/Identity interactions must go through their
contracts/gRPC adapters; this sample contains no cross-database joins or
shared-table reads.
