# BotFramework

`framework/` is the reusable runtime layer for CasinoShiz modules.

The framework is split into transport-neutral contracts, backend/runtime infrastructure,
and Telegram-specific adapter infrastructure. Game modules should depend on the
smallest framework surface they need and must not depend on concrete deployment
shape.

| Assembly | Role | Referenced by |
|----------|------|---------------|
| `BotFramework.Contracts` | Transport-neutral service contracts, DTOs, wallet/identity/read ports and portable integration contracts | hosts, services, transport adapters, game contracts |
| `BotFramework.Sdk` | Module-facing abstractions: modules, domain/events, repositories, projections, analytics, localization, runtime contracts | backend `Games.*` modules |
| `BotFramework.Sdk.Testing` | xUnit helpers and in-memory test doubles | `tests/CasinoShiz.Tests` |
| `BotFramework.Host` | Backend/runtime infrastructure: composition, persistence, CAP/event bus, economics/wallet adapters, analytics, runtime jobs, admin/security, health | backend/monolith composition roots |
| `BotFramework.Telegram.Abstractions` | Telegram update context, routes and handler contracts | Telegram adapters and runtime |
| `BotFramework.Scheduling.Abstractions` | Transport-neutral scheduled command contracts | game application layers |
| `BotFramework.Scheduling.Quartz` | Persistent Quartz implementation of game scheduling | backend composition roots |
| `BotFramework.Telegram` | Telegram adapter runtime: bot client, polling/webhook ingress, update pipeline, router, route attributes, Telegram update context and delivery helpers | Telegram BFF, monolith compatibility host, `Games.*.Telegram` adapters |

The intended dependency direction is:

```text
Games.* backend modules
  -> BotFramework.Sdk
  -> BotFramework.Contracts

Games.*.Telegram adapters
  -> BotFramework.Telegram
  -> BotFramework.Contracts / game contracts

Composition roots
  -> BotFramework.Host
  -> BotFramework.Telegram when Telegram ingress is enabled
```

Modules should not reference deployment-specific hosts. Hosts select which modules
and transports are active.

## Repository boundaries

```text
framework/
  BotFramework.Contracts/   transport-neutral contracts and portable DTOs
  BotFramework.Sdk/         module, domain, persistence and event abstractions
  BotFramework.Sdk.Testing/ test helpers and in-memory doubles
  BotFramework.Host/        backend infrastructure and composition
  BotFramework.Telegram.Abstractions/ Telegram update contracts
  BotFramework.Scheduling.Abstractions/ scheduled command contracts
  BotFramework.Scheduling.Quartz/ persistent Quartz scheduler
  BotFramework.Telegram/    Telegram ingress, update routing and delivery

games/
  Games.X.Contracts/        logical interfaces and portable DTOs
  Games.X/                  backend application/domain/infrastructure
  Games.X.Telegram/         Telegram presentation adapter
  Games.X.Transport.Grpc/   protobuf and remote adapters when needed

host/
  CasinoShiz.Host/          combined compatibility deployment
  CasinoShiz.Backend/       Telegram-free backend process
  CasinoShiz.TelegramBff/   Telegram client process
  CasinoShiz.AdminBff/      browser/admin BFF without direct database access

services/
  CasinoShiz.IdentityService/
  CasinoShiz.WalletService/

tests/
  CasinoShiz.Tests/
```

Not every context needs every optional project. Simple modules may only have
`Games.X` and `Games.X.Telegram`. Split-service modules may additionally provide
`Games.X.Contracts` and `Games.X.Transport.Grpc`.

## Layering

```text
┌────────────────────────────────────────────────────────────────────────────┐
│ L5  Presentation adapters                                                  │
│     games/*/*.Telegram, BotFramework.Telegram                              │
│     Telegram parsing, route attributes, message rendering, callbacks       │
├────────────────────────────────────────────────────────────────────────────┤
│ L4  Application                                                            │
│     games/*/Application                                                    │
│     services, commands, jobs, projections, use-case result records         │
├────────────────────────────────────────────────────────────────────────────┤
│ L3  Domain                                                                 │
│     games/*/Domain                                                         │
│     pure domain transitions, aggregates, policies, state machines          │
├────────────────────────────────────────────────────────────────────────────┤
│ L2  Platform contracts                                                     │
│     BotFramework.Contracts + BotFramework.Sdk                              │
│     modules, repositories, event store, projections, event bus, ports      │
├────────────────────────────────────────────────────────────────────────────┤
│ L1  Infrastructure                                                         │
│     games/*/Infrastructure + BotFramework.Host + transport projects        │
│     Postgres, Redis/CAP, ClickHouse, gRPC, migrations, jobs, admin, ops    │
└────────────────────────────────────────────────────────────────────────────┘
```

`BotFramework.Telegram` is intentionally not part of the backend SDK. Telegram is
an adapter boundary, not a domain dependency.

## Physical layout

Game backend modules use a consistent directory shape:

```text
games/Games.X/
  Application/
    Services/ Jobs/ Projections/ Results/ Analytics/
  Domain/
    Configuration/ Commands/ Entities/ Events/ Rules/ Results/
  Infrastructure/
    Persistence/ Migrations/ Modules/ Rendering/ Integrations/ Queues/
```

Telegram adapters live separately:

```text
games/Games.X.Telegram/
  Handlers/
  Rendering/
  CallbackData/
  Modules/
```

Transport adapters live separately when the module can run across a process boundary:

```text
games/Games.X.Transport.Grpc/
  Protos/
  Clients/
  Servers/
  Mapping/
```

`BotFramework.Host` is feature-first:

```text
Admin/ Analytics/ Commands/ Composition/ Contracts/ Economics/ Events/
Health/ Localization/ Persistence/ Random/ Redis/ Runtime/ Security/
```

`BotFramework.Telegram` owns Telegram-specific runtime concerns:

```text
Composition/ Ingress/ Pipeline/ Routing/ UpdateHandling/ Delivery/ Redis/
```

`BotFramework.Sdk` is also feature-first and keeps module-facing namespaces stable:

```text
Admin/ Commands/ Configuration/ Domain/ Events/ Health/ Metrics/
MiniGames/ Modules/ Pipeline/ Projections/ Snapshots/
```

## Host composition

The combined compatibility host can compose backend and Telegram adapters in one
process:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddBotFramework()
    .AddModule<DiceModule>()
    .AddModule<PokerModule>()
    .AddModule<SecretHitlerModule>();

builder.AddTelegramFramework()
    .AddTelegramModule<DiceTelegramModule>()
    .AddTelegramModule<PokerTelegramModule>()
    .AddTelegramModule<SecretHitlerTelegramModule>();

var app = builder.Build();

app.UseBotFramework();
app.UseTelegramFramework();

app.Run();
```

A split deployment composes the backend and Telegram BFF separately.

Backend process:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddBackendFramework()
    .AddModule<DiceModule>()
    .AddModule<PokerModule>()
    .AddModule<SecretHitlerModule>();

var app = builder.Build();

app.UseBackendFramework();

app.Run();
```

Telegram BFF process:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddTelegramBff()
    .AddTelegramModule<DiceTelegramModule>()
    .AddTelegramModule<PokerTelegramModule>()
    .AddTelegramModule<SecretHitlerTelegramModule>();

var app = builder.Build();

app.UseTelegramFramework();

app.Run();
```

The composition root decides whether a contract is implemented locally or through
a transport adapter such as gRPC. Application-facing interfaces do not change when
a module crosses the process boundary.

## Module registration

Backend modules implement the framework module contract and register their services:

```csharp
public sealed class MyGameModule : IModule
{
    public void ConfigureServices(IModuleServiceCollection services)
    {
        services.AddApplicationService<MyGameService>();
        services.AddProjection<MyProjection>();
        services.RegisterAggregate<MyAggregate>(PersistenceStrategy.EventSourced);
        services.AddMigrations<MyGameMigrations>();
    }
}
```

Telegram modules register handlers and presentation services:

```csharp
public sealed class MyGameTelegramModule : ITelegramModule
{
    public void ConfigureServices(ITelegramModuleServiceCollection services)
    {
        services.AddHandler<MyGameCommandHandler>();
        services.AddRenderer<MyGameRenderer>();
    }
}
```

A new module should be able to start with a small set of contracts and grow only
when it needs persistence, projections, transport adapters or Telegram presentation.

## Telegram update pipeline and routing

Every Telegram update flows through the Telegram update pipeline and then the
attribute router:

```text
Exception → Deduplication → Logging → RateLimit → KnownChats → UpdateRouter
```

Routing is attribute-driven. A Telegram adapter adds a handler by implementing
`IUpdateHandler`, decorating it with route attributes and registering it in the
Telegram module:

```csharp
services.AddHandler<MyHandler>();
```

Supported route attributes include:

```text
[Command]
[CallbackPrefix]
[CallbackFallback]
[MessageDice]
[ChannelPost]
```

Only the Telegram layer knows Telegram update types, bot clients and rendering
details. Backend services receive application commands through logical contracts.

## Deployment shapes

The framework supports two main deployment shapes.

### Combined compatibility host

```text
CasinoShiz.Host
  backend modules
  Telegram modules
  admin UI
  persistence
  eventing
  jobs
```

This mode is useful for local development, compatibility and simple deployment.

### Split services

```text
CasinoShiz.Backend
  backend modules
  event store
  projections
  jobs
  admin compatibility surface

CasinoShiz.TelegramBff
  Telegram ingress
  update routing
  Telegram handlers
  gRPC/local contract clients

CasinoShiz.AdminBff
  browser auth/session
  reverse proxy / operations calls
  no direct database access

CasinoShiz.IdentityService
  identity-owned storage and contracts

CasinoShiz.WalletService
  wallet, ledger, limits and protection
```

Transport choice belongs to composition. Modules should depend on logical contracts,
not on whether the target is local, gRPC or another transport.

## Persistence styles

The framework supports two aggregate persistence styles.

### Classical aggregates

Classical aggregates are persisted by module-owned stores/repositories, usually
with Dapper and module-specific tables. Most simple game modules should use this
style.

### Event-sourced aggregates

Event-sourced aggregates implement `IEventSourcedAggregate` and are registered by
the module:

```csharp
services.RegisterAggregate<MyAggregate>(PersistenceStrategy.EventSourced);
```

This registration wires:

- `IRepository<MyAggregate>` → `EventSourcedRepository<MyAggregate>`
- `IAggregateFactory<MyAggregate>` → `DefaultAggregateFactory<MyAggregate>` unless
  the module already registered a custom factory

The event-sourced repository owns the aggregate lifecycle:

1. Load stored events from `IEventStore`.
2. Deserialize and replay them through `LoadFromHistory`.
3. Append `PendingEvents` with optimistic concurrency.
4. Dispatch appended events through `EventDispatcher`.
5. Call `MarkEventsCommitted` after successful append.

`DefaultAggregateFactory<T>` tries to create aggregates through DI, first with the
stream id as a `string` constructor argument and then without it. A module can
override this by registering its own `IAggregateFactory<T>` before calling
`RegisterAggregate<T>(PersistenceStrategy.EventSourced)`.

## Event flow

The current event-sourced flow is intentionally post-commit:

```text
Application service
  ↓
IRepository<TAggregate>
  ↓
EventSourcedRepository<TAggregate>
  ↓
IEventStore.AppendAsync(...)        // commits module_events
  ↓
EventDispatcher.DispatchAsync(...)  // projections, domain bus, analytics
```

`IEventStore` remains a small storage primitive: load stream and append events. It
does not know about projections, subscribers or analytics.

`EventDispatcher` fans each committed event into:

- matching `IProjection`s;
- `IDomainEventBus` subscribers, including framework-wide subscribers;
- `IAnalyticsService.Track(...)`.

Post-commit dispatch means a dispatch failure does not roll back the already
committed event append. Projection handlers and subscribers must be idempotent.
Recovery should be done with replay/rebuild jobs or targeted retries.

`ProjectionContext.Transaction` is nullable. It is normally `null` in the current
implementation. The field exists so a future same-transaction unit-of-work
implementation can pass a provider-specific transaction object without changing
module contracts.

## Cross-module domain events

`IDomainEventBus` lets modules react to events without referencing each other.
Subscriptions are pattern-based:

| Pattern | Meaning |
|---------|---------|
| `sh.game_ended` | exact event |
| `sh.*` | every event from one module |
| `*.game_ended` | one action from any module |
| `*` | every event |

At startup, `EventSubscriptionInitializer` subscribes framework listeners to `*`:

- `EventLogSubscriber` writes events to `event_log` for admin/history views.
- `ClickHouseEventMirror` mirrors events to ClickHouse when analytics is enabled.

Modules add their own subscribers with:

```csharp
services.AddDomainEventSubscription<MySubscriber>("poker.*");
```

`IDomainEventBus` is an abstraction. The active implementation can be in-process
or backed by CAP/Redis Streams depending on composition and configuration.

## Projections

Projections are module-owned read models. A projection declares which event types
it handles:

```csharp
public sealed class MyProjection : IProjection
{
    public IReadOnlySet<string> SubscribedEventTypes { get; } =
        new HashSet<string> { "poker.hand_won" };

    public Task ApplyAsync(IDomainEvent ev, ProjectionContext ctx, CancellationToken ct)
    {
        // update read model table
    }
}
```

Register it in the module:

```csharp
services.AddProjection<MyProjection>();
```

Projections run after the event append commits. They should be idempotent and safe
to replay.

## Event tables

`module_events` stores event-sourced aggregate streams. It is the source of truth
for replayable aggregate state.

`event_log` is a flat audit/history stream populated by `EventLogSubscriber`. It
can contain events from event-sourced modules and other published domain events.
Admin history pages should read this table, not replay aggregate streams.

`module_snapshots` stores optional aggregate snapshots through `ISnapshotStore<T>`.

`event_dispatch_failures` stores failed post-commit dispatch attempts for targeted
retry and operational recovery.

## Outbox and side effects

The framework distinguishes event delivery from external side effects.

Domain/integration events can flow through the configured event bus. External
effects such as Telegram messages emitted from subscribers and background jobs
should use a durable outbox.

Telegram outbox records are persisted before sending. In the monolith, the local
dispatcher claims due rows and sends them to Telegram. In the split deployment, the
Backend relay claims rows, publishes a CAP command through Redis to Telegram BFF,
and marks a row sent only after the BFF confirms the Telegram message id. Both modes
reclaim expired leases and retain retry metadata on failure.

Handlers that respond immediately to live Telegram updates may still send direct
Telegram responses. Critical asynchronous notifications should use the outbox.

## Wallet and identity boundaries

Wallet and identity are logical service boundaries.

Backend modules should not directly query wallet or identity tables. They should
use framework contracts such as wallet read/write ports, player directory ports and
service adapters selected by composition.

In a combined host those ports may be implemented locally. In a split deployment
they may be implemented by gRPC clients pointing at `CasinoShiz.IdentityService`
and `CasinoShiz.WalletService`.

Wallet invariants such as idempotent debits/credits, balance scopes, ledger append,
limits, cooldowns and self-exclusion belong to the wallet owner, not to individual
game modules.

## Migrations

Framework-owned tables are created by framework migrations first. Module migrations
are then applied through `IModuleMigrations` and tracked in `__module_migrations`.

Migrations are forward-only raw SQL migrations executed via Dapper.

When services are split, each service should own its own database/schema migrations.
Backend migrations should not create or mutate wallet/identity-owned tables except
during explicitly marked compatibility transitions.

## Adding a new backend module

A new backend module should:

1. Define its application service contract or use an existing framework contract.
2. Define domain model, commands, results and events.
3. Choose classical persistence or event-sourced persistence.
4. Register services through `ConfigureServices(IModuleServiceCollection)`.
5. Register migrations through `IModuleMigrations`.
6. Register projections and domain-event subscribers when needed.
7. Keep Telegram, HTTP and transport-specific code out of the backend module.

## Adding a new Telegram adapter module

A new Telegram adapter should:

1. Reference `BotFramework.Telegram`.
2. Reference the game contract or backend-facing interface it needs.
3. Implement Telegram handlers using route attributes.
4. Parse Telegram input and render Telegram responses.
5. Call logical application contracts, not concrete backend services.
6. Register handlers through `ConfigureServices(ITelegramModuleServiceCollection)`.

The adapter should not own game state, wallet state or persistence.

## Adding a new event-sourced module

A new module that wants event sourcing should:

1. Define domain events implementing `IDomainEvent` with stable `EventType` strings,
   e.g. `mygame.round_started`.
2. Define an aggregate implementing `IEventSourcedAggregate`.
3. Register it with `RegisterAggregate<MyAggregate>(PersistenceStrategy.EventSourced)`.
4. Optionally register `IAggregateFactory<MyAggregate>` if the default DI/id
   constructor creation is not enough.
5. Register projections with `AddProjection<TProjection>()`.
6. Register cross-module subscribers with
   `AddDomainEventSubscription<TSubscriber>(pattern)`.

After that, appended aggregate events automatically flow through projections,
event log, ClickHouse mirror, module subscribers and analytics.

## Framework rules

- `BotFramework.Sdk` should remain transport-neutral.
- `BotFramework.Telegram` is the framework project that owns Telegram runtime types.
- Backend modules should not depend on Telegram adapters.
- Telegram adapters should not own persistence.
- Transport adapters should live outside domain/application modules.
- Composition roots decide local vs remote implementation.
- Event handlers, projections and subscribers must be idempotent.
- External side effects that must survive process failure should use an outbox.
- Wallet and identity tables are owned by their services, not by games.
- Game modules should communicate through contracts and events, not by importing another game's internals.
