# Persistence boundaries

Persistence technology follows consistency requirements rather than personal preference.

| Workload | Default |
|---|---|
| Ordinary `Games.*.Infrastructure` aggregate persistence | EF Core |
| Simple CQRS read models | EF Core, normally `AsNoTracking` |
| Hot or bulk projections | Dapper/raw SQL |
| Event store and snapshots | Dapper/raw SQL |
| Telegram/external-side-effect outbox | Dapper/raw SQL |
| Wallet and ledger mutations | Dapper/raw SQL with explicit transactions/idempotency |
| Schema migrations and distributed/advisory locks | Dapper/raw SQL |

Rules:

1. Game modules own their domain model and EF mappings. Framework projects do not model game entities.
2. EF migrations are not run automatically by game modules. Deployment schema changes remain explicit host migrations.
3. Do not replace an atomic SQL statement with a read-modify-write EF sequence.
4. Read queries use `AsNoTracking` unless the same instance is intentionally updated in that unit of work.
5. Bulk projection rebuilds use set-based SQL; do not materialize event streams merely to satisfy an ORM API.
6. CAP is an integration-event delivery adapter. CAP entities and APIs do not enter domain contracts.
7. The outbox is for external side effects. Domain state and wallet consistency are committed at their owning boundary.

`SecretHitlerDbContext` and `SecretHitlerGameStore` are the first reference implementation for ordinary game persistence.


## Physical ownership in microservices mode

The `microservices` Docker Compose profile uses independent PostgreSQL instances and volumes for Backend, Identity, and Wallet. The monolith profile keeps the shared `cazino` database for backwards compatibility. Cross-service read composition belongs in a BFF/aggregator and must use service APIs; direct cross-database joins are forbidden. Identity migrations do not backfill from the Wallet `users` table in distributed mode.
