# GameShiz

[![](https://tokei.rs/b1/github/cpp20120/CazinoShiz)](https://github.com/cpp20120/CazinoShiz)

Telegram party-games RPG bot with virtual credits and party-game bot with Russian-language UI, wallet balances, admin tools, analytics, and a modular game system. It runs as an ASP.NET Core host with independent game modules.

DISCLAMER: THIS BOT IS HAS NO ANY REAL MONEY AND ABILITY TO USE THEM.

This project is a simulation-only system.

-   It does not support real money, cryptocurrencies, or any form of monetary value.
-   Virtual coins/credits:
    -   cannot be purchased
    -   cannot be sold
    -   cannot be exchanged
    -   cannot be transferred for any real-world goods, services, or value
-   There is no mechanism to deposit or withdraw real money.
-   Redeem codes are generated internally for gameplay purposes only and have no monetary value.

This software must not be used for real-money gambling or any commercial gambling activity.

The primary purpose of this project is educational, focusing on architecture patterns such as DDD, Event Sourcing, and CQRS.
Main Product of that bot is framework for stateful games across platforms not bot itself.

## Documentation

-   [Full bot documentation](docs/docs.md) — games, commands, seasonal meta, architecture, configuration, admin UI, database, and deployment.
-   [Architecture diagrams](docs/arch.md) — Mermaid views of runtime flow, modules, events, persistence, tuning, and deployment.
-   [Operations runbook](docs/operations.md) — scheduled jobs, daily bonus recovery, diagnostics, SQL checks, and incident operations.
-   [Framework documentation](framework/README.md) — module contracts, routing, event sourcing, projections, and migrations.
-   [Secret Hitler strategy model](docs/secret_hitler.md) — probabilistic policy/deception analysis and model limitations.

## Features

### Telegram Games

Command

Feature

`/dice`

Dice cube betting with Telegram 🎲 dice animation.

`/darts`

Darts betting with Telegram 🎯 dice animation.

`/football`

Football betting with Telegram ⚽ dice animation.

`/basket`

Basketball betting with Telegram 🏀 dice animation.

`/bowling`

Bowling betting with Telegram 🎳 dice animation.

`/blackjack`

Single-player blackjack with hit, stand, double, timeout cleanup, and persisted in-progress hands.

`/horse`

Horse-race betting with SkiaSharp GIF rendering, scheduled races, manual admin runs, and place labels in the animation.

`/poker`

Texas Hold'em poker tables.

`/sh`

Secret Hitler party game. See the [strategy model](docs/secret_hitler.md).

`/challenge`

1v1 PvP betting challenges between two Telegram users.

`/pixelbattle`

Telegram WebApp mini app for shared pixel painting.

`/picklottery`

Per chat pool lottery

`/transfer`

Coin transfers between users, including transfer fees.

`/redeem`

Freespin/redeem code activation.

`/menu`

Interactive player hub. See the [seasonal meta documentation](docs/docs.md#seasonal-meta-gamesmeta).

`/mystats`

Private player statistics, stake limit, cooldown, and self-exclusion controls.

`/top`, `/balance`, `/daily`, `/help`

Leaderboard, wallet balance, daily bonus, and help.

### Operations documentation

See [docs/operations.md](docs/operations.md) for the runtime runbook covering:

-   daily bonus and downtime catch-up
-   scheduled horse races
-   `/__debug_jobs` and Admin Dashboard job visibility
-   idempotent money-flow SQL checks
-   admin wallet edits
-   event dispatch failure retry commands and the `/admin/recovery` workflow
-   audit inspection/export and current-economy reconciliation
-   Docker port mapping checks

### 1v1 Challenges

`/challenge` lets one player challenge another player to a PvP stake. The challenger can use a username:

```text
/challenge @username 500 dicecube
```

Or reply to a user's message:

```text
/challenge 500 darts
```

Supported challenge games:

-   `dice` / `dicecube` 🎲
-   `darts` 🎯
-   `bowling` 🎳
-   `basketball` 🏀
-   `football` ⚽
-   `slots` 🎰
-   `horse` 🐎
-   `blackjack` 🃏

Challenges escrow both players' stakes when accepted, return stakes on ties/failures, and pay the winner the pot minus the configured house fee. Horse challenges render a 2-player GIF and wait for the animation before announcing the winner. Blackjack challenges are auto-resolved with crypto-shuffled hands that draw until 17.

### PixelBattle WebApp

PixelBattle is a Telegram WebApp served from `wwwroot/pixelbattle` and opened by `/pixelbattle`. It uses:

-   Telegram WebApp `initData` validation
-   a canvas renderer for the pixel grid
-   Server-Sent Events for live updates
-   PostgreSQL persistence

Telegram requires a public HTTPS URL for WebApps. For local development, expose the host with a tunnel and set:

```text
Games__pixelbattle__WebAppUrl=https://your-public-host/pixelbattle/index.html
```

Old WebApp buttons can keep stale Telegram `initData`; send a fresh `/pixelbattle` command if a page starts returning `401`.

## Stack

Layer

Tech

Runtime

ASP.NET Core, .NET 10 SDK

Telegram

`Telegram.Bot` 22.x, polling and webhook modes

Persistence

PostgreSQL 16 via Dapper

Event bus

In-process domain event bus, with Redis/CAP support where configured

Update fan-out

Redis Streams, opt-in and partitioned by chat id

Analytics

ClickHouse buffered writer, disabled gracefully when unavailable

Dashboards

Grafana, Prometheus, Postgres/Redis exporters, cAdvisor

Graphics

SkiaSharp 3.x for horse race GIF rendering

Tests

xUnit tests for framework, domain logic, services, and routing

## Project Structure

```text
framework/
  BotFramework.Sdk/          module contracts split by feature: admin, commands, domain,
                             events, modules, mini-games, projections, update handling
  BotFramework.Sdk.Testing/  xUnit helpers split into repositories and fakes
  BotFramework.Host/         runtime infrastructure split by feature: composition, pipeline, events,
                             economics, analytics, admin auth, persistence, runtime jobs
games/
  Games.Dice/ Games.DiceCube/ Games.Darts/ Games.Football/
  Games.Basketball/ Games.Bowling/ Games.Blackjack/ Games.Horse/
  Games.Poker/ Games.SecretHitler/ Games.Challenges/ Games.PixelBattle/
  Games.Redeem/ Games.Leaderboard/ Games.Transfer/ Games.Admin/
host/
  CasinoShiz.Host/           Composition/Program.cs composition root and Razor admin UI
tests/
  CasinoShiz.Tests/
```

Each game module follows the same physical split:

```text
Application/      handlers, application services, jobs, projections, use-case results
Domain/           pure game rules, state, events, commands, options, result records
Infrastructure/   stores, migrations, module registration, rendering, external integrations
```

Within those layers, files are grouped into logical subfolders such as `Handlers`, `Services`, `Jobs`, `Results`, `Configuration`, `Events`, `Rules`, `Persistence`, `Modules`, and `Rendering`. The host wires modules through `AddBotFramework().AddModule<T>()`.

## Setup

Copy `.env.example` to `.env` and fill in required fields:

```bash
cp .env.example .env
# edit .env: set Bot__Token, Bot__Username, Bot__Admins__0, ConnectionStrings__Postgres
```

Run locally in polling mode:

```bash
dotnet build
dotnet run --project host/CasinoShiz.Host
```

Run the full stack as a monolith:

```bash
docker compose --profile monolith up --build
```

Run the same application boundaries as separate processes:

```bash
# Also set OPERATIONS_API_KEY and ADMIN_SUPERADMIN_TOKEN in .env.
docker compose --profile microservices up --build
```

For independent game deployments, use the distributed Compose profile. It
starts one Backend composition per game, plus separate Telegram and Discord
BFFs. Scale a game without changing code:

```bash
docker compose --profile distributed up --build
docker compose --profile distributed up --scale game-poker=3
```

Kubernetes uses the same `Backend__Modules` and `Backend__GameAddresses__*`
configuration through the Helm chart:

```bash
helm upgrade --install cazinoshiz ./deploy/helm/cazinoshiz
kubectl scale deployment game-poker --replicas=3
```

The profiles deliberately keep different database ownership. Monolith uses the
shared `postgres/cazino`; microservices use `backend-postgres/backend`,
`identity-postgres/identity`, and `wallet-postgres/wallet`. Backend, Identity,
and Wallet run only their own migrations, and BFF aggregation uses gRPC APIs
instead of cross-database reads.

Back up the active profile with:

```bash
docker compose --profile monolith run --rm db-backup
docker compose --profile microservices run --rm db-backup-microservices
```

Useful local URLs:

-   Monolith: `http://localhost:4000`
-   Backend: `http://localhost:5081/health/live`
-   Identity: `http://localhost:5082/health/live`
-   Wallet: `http://localhost:5083/health/live`
-   Telegram BFF: `http://localhost:5084/health/live`
-   Discord BFF: `http://localhost:5086/health/ready` (microservices) or `http://localhost:5088/health/ready` (distributed)
-   Admin BFF: `http://localhost:5085`
-   Grafana: `http://localhost:3001`
-   Prometheus: `http://localhost:9090`

Run tests:

```bash
dotnet test
```

## Admin UI

Go to `http://localhost:3000/admin/login`.

Sign-in methods:

-   Token form: paste `Bot__AdminWebToken` from `.env`.
-   Telegram Login Widget: requires `Bot__Username` and BotFather `/setdomain`; public domains only.

Roles:

-   `SuperAdmin` from `Bot__Admins`: full access, including balance mutations and manual race actions.
-   `ReadOnly` from `Bot__ReadOnlyAdmins`: view-only; mutation endpoints return `403`.

Admin pages include:

-   Dashboard: live PostgreSQL economy snapshot (wallet supply, pending stake, tracked coins, wallet distribution, and rolling 24-hour flows), loaded modules, event counts, sticker-game plays, and background/host job status.
-   People, Wallets, Ledger, History: user and balance tracking.
-   Chats: known group/private/channel list from Telegram updates.
-   Bets: pending game bets across supported modules.
-   1v1: challenge tracking by game type, status, chat, players, stake, and pot.
-   Horse: race controls, schedule visibility, and GIF preview tools.
-   Events: recent domain events with module/chat filters.
-   Recovery: unresolved event-dispatch failures, pending/sending Telegram outbox rows, safe confirmed single-record retries, and job status. Mutations require `SuperAdmin`.
-   Audit: actor/action/details/time filters plus bounded CSV and structured JSON exports.
-   Meta: seasons, quests, tournaments, events, economy, and alert administration.
-   Settings: runtime configuration overview.

All write actions are logged to the `admin_audit` table. Admin wallet Set/+/- operations also write idempotent `economics_ledger.operation_id` values to protect against duplicate form submits.

## Diagnostics & Health

The host provides built-in endpoints and hidden commands for monitoring:

-   **HTTP `/health/live`**: Liveness probe endpoint. Returns HTTP 200 (or 503) based on basic application responsiveness.
-   **HTTP `/health/ready`**: Readiness probe endpoint. Returns HTTP 200 (or 503) along with the status of configured infrastructure dependencies (PostgreSQL, Redis, ClickHouse).
-   **Telegram `/debug` command**: A hidden bot command that reports technical process metrics directly in the chat, including the current `chat_id`, `chat_type`, process `uptime`, total `cpu time`, and memory usage (`rss`).
-   **Telegram `/__debug_jobs` command**: Shows module and host job status, including horse scheduled races and daily bonus catch-up.
-   **Telegram `/__debug_dispatch_failures` command**: Lists unresolved projection/event dispatch failures.
-   **Telegram `/__debug_retry_dispatch_failure <id>` command**: Retries one unresolved dispatch failure.

## Configuration

Most settings can be provided in `appsettings.json` or as environment variables with `__` separators.

Key

Required

Description

`Bot__Token`

yes

Telegram bot API token.

`Bot__Username`

yes

Bot username, with or without `@`; used by Telegram login and links.

`Bot__Admins__0`

yes

Telegram user ID with full admin access.

`Bot__ReadOnlyAdmins__0`

no

Telegram user ID with read-only admin access.

`Bot__AdminWebToken`

no

Token for password-style admin login.

`Bot__IsProduction`

no

`true` for webhook mode, `false` for polling.

`Bot__WebhookBaseUrl`

production

Public webhook base URL.

`Bot__WebhookPort`

no

HTTP port used by the host.

`Bot__TrustedChannel`

no

Channel username for trusted broadcasts/race posting.

`Bot__StartingCoins`

no

Initial user balance.

`Bot__DailyBonus__Enabled`

no

Enables manual `/daily` and catch-up logic.

`Bot__DailyBonus__PercentOfBalance`

no

Percent of current balance credited by daily bonus.

`Bot__DailyBonus__MaxBonus`

no

Upper cap for daily bonus amount.

`Bot__DailyBonus__TimezoneOffsetHours`

no

Local-day boundary used by daily bonus and catch-up.

`Bot__DailyBonus__CatchUpEnabled`

no

Enables downtime catch-up for missed past local days.

`Bot__DailyBonus__MaxCatchUpDays`

no

Safety cap for number of days catch-up can process.

`ConnectionStrings__Postgres`

yes

PostgreSQL connection string.

`Redis__Enabled`

no

Enables Redis-backed infrastructure.

`Redis__ConnectionString`

if enabled

Redis connection string, for example `redis:6379`.

`TelegramOutbox__Transport`

no

`Local` (default) sends through the monolith dispatcher. Set `Cap` for split Backend → Telegram BFF delivery; it requires PostgreSQL and Redis in both processes.

`ClickHouse__Enabled`

no

Enables analytics writes.

`ClickHouse__Host`

if enabled

ClickHouse HTTP endpoint.

`Games__pixelbattle__WebAppUrl`

PixelBattle

Public HTTPS URL ending in `/pixelbattle/index.html`.

`Games__challenges__MinBet`

no

Minimum 1v1 challenge stake.

`Games__challenges__MaxBet`

no

Maximum 1v1 challenge stake.

`Games__challenges__HouseFeeBasisPoints`

no

Challenge fee in basis points, where `200` is 2%.

`Games__challenges__PendingTtlMinutes`

no

Time before pending challenges expire.

`Games__transfer__FeePercent`

no

Transfer fee ratio.

`Games__horse__AutoRunEnabled`

no

Enables scheduled horse races.

`Games__horse__AutoRunLocalHour` / `Games__horse__AutoRunLocalMinute`

no

Local scheduled race time.

`Games__horse__TimezoneOffsetHours`

no

Local day/timezone for horse scheduled races.

Telegram dice games also support per-game `MaxBet`, `DefaultBet`, and redeem drop chance options under `Games:<game>`.

See [docs/operations.md](docs/operations.md) for operation ids, SQL smoke checks, catch-up behavior, scheduled jobs, and Docker port mapping notes.

## Database And Migrations

Modules provide Dapper-based migrations through `IModuleMigrations`. The host applies them automatically at startup and tracks applied migrations in `__module_migrations`.

Important module tables include:

-   `users`: wallet balances scoped by chat/user.
-   `event_log`: domain events used by admin tracking.
-   `module_events`: append-only Event Sourcing event streams.
-   `event_dispatch_failures`: unresolved projection/dispatch failures with retry metadata.
-   `challenge_duels`: 1v1 challenge state.
-   `pixelbattle_tiles`: PixelBattle grid state.
-   per-game pending tables such as `blackjack_hands`, `horse_bets`, and Telegram dice bet tables.

## Observability

When enabled, analytics events are buffered to ClickHouse and visualized in Grafana. Every Telegram request receives a correlation ID and rolling 30-minute session ID; route and command completion events include outcome, error code, and duration. A five-minute product snapshot moves bounded PostgreSQL aggregates into `meta_analytics.*` ClickHouse events for economy and ledger reconciliation, engagement and retention inputs, delivery/reliability backlogs, live and stale game state, social activity, seasons, quests, risk, and snapshot-job health. ClickHouse writer health and native event-quality/funnel/cohort queries stay entirely in ClickHouse. The Docker stack also includes Prometheus targets for Postgres, Redis, and container metrics.

Players can use private-chat `/mystats` for 7/30-day stake and payout totals, a global UTC daily stake limit, cooldowns, and fixed-term self-exclusion. Limits are enforced transactionally in the shared economics ledger for every game. Admins receive a deduplicated weekly product/economy summary and hourly-deduplicated alerts for negative wallets, ledger mismatches, and unusually large balance mutations.

Persisted domain events are additionally written to the canonical `<events table>_es` table with deterministic IDs derived from `(stream_id, stream_version)`, aggregate/schema metadata, original occurrence time, and correlation/causation IDs. A checkpointed startup worker backfills `module_events` in batches without adding replay rows to the general product-event table.

The complete event catalog, field dictionary, snapshot schemas, metric formulas, dashboard dependencies, and replay guarantees are documented in [docs/docs.md](docs/docs.md#analytics).

If Grafana panels are empty, check Prometheus targets first, then verify Grafana data sources. Old cached Grafana state may require recreating the stack or removing the `grafana_data` volume.

Operational visibility is available through:

-   Admin Dashboard background/host jobs table.
-   `/__debug_jobs` for Telegram-side job status.
-   `/__debug_dispatch_failures` and `/__debug_retry_dispatch_failure <id>` for failed event dispatches.
-   SQL checks in [docs/operations.md](docs/operations.md).

## License

[MIT](LICENSE)
