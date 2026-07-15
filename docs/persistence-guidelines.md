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

The deployment profiles intentionally have different persistence topologies:

| Profile | Process | PostgreSQL owner | Migration runner |
|---|---|---|---|
| `monolith` | `cazino-bot` | shared `postgres/cazino` | full framework + loaded game migrations |
| `microservices` | `backend` | `backend-postgres/backend` | Backend framework/game migrations only |
| `microservices` | `identity` | `identity-postgres/identity` | Identity migrations only |
| `microservices` | `wallet` | `wallet-postgres/wallet` | Wallet migrations only |

Wallet-owned tables (`users`, `economics_ledger`, `player_protection`) are never
created by the Backend microservices migration set. Backend-owned operational,
game, outbox, quota, and event tables stay in `backend`. Identity owns only
`player_identities`. A fresh microservices deployment should use its dedicated
volumes; an old shared database is not an automatic migration source.

Backend game/admin code uses wallet and identity contracts. The Wallet atomic
batch API persists an operation id and performs the complete balance/ledger
mutation in the Wallet transaction, so retrying a request after a gRPC failure
does not double-apply it. There are no cross-database joins or shared-table
reads. The Admin BFF exposes composed read models under
`/api/aggregation/players/{userId}` and `/api/aggregation/admin`, combining
Identity, Wallet, and Backend Operations responses through service APIs.

Backups follow the same ownership boundary:

```bash
docker compose --profile monolith run --rm db-backup
docker compose --profile microservices run --rm db-backup-microservices
```

The microservices backup writes independent `backend_*.sql`, `identity_*.sql`,
and `wallet_*.sql` files. Restore each dump into its matching PostgreSQL
instance; do not merge them or restore a Wallet dump into Backend.
