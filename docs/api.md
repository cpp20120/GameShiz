# BotFramework API

> Status: frozen and approved for the `1.0.0` release.
>
> This document is the compatibility baseline for the `1.0.0` public API and
> package boundaries. It does not itself publish packages; publication is
> performed by the checked-in release workflow after the verification gates
> below pass.

## 1. Purpose and compatibility boundary

BotFramework is a reusable runtime and contract layer for modules that run
through Telegram, Discord, REST, or a host-owned background process.

The public API is limited to the package contracts described below. Host
internals, database schemas, Redis keys, generated implementation details,
platform update ids, and numeric persistence keys are not module contracts.

The intended dependency direction is:

```text
BotFramework.Contracts
        ↑
BotFramework.Sdk ───────────────┐
        ↑                        │
channel abstractions / REST     │
        ↑                        │
application modules ────────────┘
```

`BotFramework.Text` is an independent, platform-neutral processing package.
It does not contain moderation rules or censorship behavior. Full text API
details are documented in [`botframework-text.md`](botframework-text.md).

## 2. Public packages

| Package | Purpose | Target frameworks |
|---|---|---|
| `BotFramework.Contracts` | Tenant identity, transport-neutral DTOs, requests, pagination, errors, and rate limiting | `net8.0`, `net10.0` |
| `BotFramework.Sdk` | Module, command, domain, decision/effect, event, projection, and health contracts | `net8.0`, `net10.0` |
| `BotFramework.Testing` | Test doubles and in-memory SDK fixtures | `net8.0`, `net10.0` |
| `BotFramework.Text` | Normalization, tokenization, analyzers, policies, matches, and generic effects | `net8.0`, `net10.0` |
| `BotFramework.Scheduling.Abstractions` | Transport-neutral scheduling contracts | `net8.0`, `net10.0` |
| `BotFramework.Rest` | Tenant-aware ASP.NET Core REST middleware and route-module contract | `net10.0` |
| `BotFramework.Telegram.Abstractions` | Telegram update, routing, handler, and tenant-resolution contracts | `net10.0` |
| `BotFramework.Discord.Abstractions` | Discord tenant and scope resolution contracts | `net10.0` |
| `BotFramework.Client` | Typed tenant-aware REST client and generated OpenAPI client boundary | `net8.0`, `net10.0` |

The source directory `framework/BotFramework.Sdk.Testing` produces the package
`BotFramework.Testing`.

`BotFramework.Host`, `BotFramework.Telegram`, `BotFramework.Discord`,
`BotFramework.Scheduling.Quartz`, and rendering/runtime assemblies are
composition implementations. A module should depend on their abstractions,
not on their persistence or delivery internals.

`BotFramework.GameTemplates` is a separate `dotnet new` consumer artifact,
not a runtime API package.

## 3. Tenant and request identity

### Opaque identifiers

The following value types are the public identity boundary:

```csharp
TenantId.Create(string value);
ScopeId.Create(string value);
PlayerId.Create(string value);
RequestId.Create(string value);
RequestId.New();
```

Each type is a validated opaque string value. Values cannot be empty, contain
whitespace, path separators, or control characters, and are limited to 256
characters. They are serialized as JSON strings.

Internal numeric tenant, scope, and channel keys must not appear in module
DTOs, REST paths, or public event contracts.

### TenantContext

```csharp
public sealed record TenantContext(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId? PlayerId,
    BotChannel Channel,
    RequestId RequestId,
    RequestId CorrelationId);
```

Use `TenantContext.Create(...)` when request and correlation ids should be
generated automatically. `ChannelContainerId` and `ChannelTopicId` are
optional adapter metadata.

The ambient boundary is:

```csharp
public interface ITenantContextAccessor
{
    TenantContext? Current { get; }
    TenantContext RequireCurrent();
    IDisposable Push(TenantContext context);
}
```

Host ingress pushes the context for the duration of an operation and restores
the previous value when the operation finishes. Modules should read the
context, not reconstruct it from Telegram or Discord ids.

### Channel mapping

| Transport | Tenant | Scope |
|---|---|---|
| Telegram forum chat | chat | topic id |
| Telegram ordinary chat | chat | `main` |
| Telegram private chat | private chat container | `main` |
| Discord guild | guild | channel or thread |
| Discord DM | private user container | `main` |
| REST | trusted JWT `tenant_id` | trusted JWT `scope_id` |

`BotChannel` contains the concrete inbound channels `Telegram`, `Discord`, and
`Rest`, plus `System` for transport-neutral or background execution. `System`
is the default for `RequestMetadata.Create(...)` and direct game decisions;
transport adapters must set their concrete channel explicitly.

## 4. Transport-neutral contracts

### Requests and metadata

For logical request/response operations use:

```csharp
public interface IRequest<out TResponse>
{
    string MessageType { get; }
}

public interface IRequestClient
{
    Task<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>;
}
```

`RequestMetadata.FromTenantContext(...)` preserves tenant, scope, player,
channel, request, and correlation identity when a request crosses a process
or transport boundary.

### Pagination and headers

```csharp
public readonly record struct CursorPageRequest(
    string? Cursor = null,
    int Limit = 50);

public sealed record CursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore);
```

`CursorPageRequest.Normalize()` validates a limit from 1 through 100 and
normalizes an empty cursor to `null`. Cursors are opaque and must not be
parsed by clients.

Canonical headers are exposed by `BotFrameworkTransportHeaders`:

```text
X-Request-ID
X-Correlation-ID
Idempotency-Key
Retry-After
```

State-changing operations require a printable `Idempotency-Key` at the REST
boundary.

### Rate limiting

```csharp
public interface IRateLimiter
{
    ValueTask<RateLimitDecision> CheckAsync(
        RateLimitRequest request,
        CancellationToken cancellationToken = default);
}
```

`RateLimitRequest` carries tenant, optional player, channel, stable route key,
and an optional REST IP address. `RateLimitDecision` reports whether the
request is allowed, remaining capacity, retry delay, the denied dimension,
policy version, and whether a bounded local fallback was used.

The framework recognizes these dimensions:

```text
Tenant
TenantPlayer
TenantIp
TenantRoute
TenantPlayerRoute
```

### Cache ports

Read models and other bounded projections may use the framework cache ports:

```csharp
public interface ICacheStore
{
    Task<string?> GetStringAsync(string key, CancellationToken ct);

    Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct);
}

public interface ICacheStoreInvalidator
{
    Task RemoveStringAsync(string key, CancellationToken ct);
}
```

`ICacheStoreInvalidator` is an optional invalidation port. Implementations may
use it to remove distributed entries after a successful state mutation; the
authoritative state remains in the primary store.

## 5. Module SDK

### Module entry point

Every module implements `BotFramework.Sdk.Modules.IModule`:

```csharp
public interface IModule
{
    string Id { get; }
    string DisplayName { get; }
    string Version { get; }
    void ConfigureServices(IModuleServiceCollection services);
    IModuleMigrations? GetMigrations();
    IReadOnlyList<LocaleBundle> GetLocales();
    IReadOnlyList<BotCommand> GetBotCommands();
    Task ShutdownAsync(CancellationToken cancellationToken);
}
```

`Id` is a stable machine identifier used by configuration, event names, and
admin routes. It must not be changed after publication. `Version` is the
module version, independent of the framework package version.

The host-facing registration surface includes:

```csharp
services.BindOptions<TOptions>(section);
services.BindOptions<TOptions, TValidator>(section);
services.AddScoped<TService, TImplementation>();
services.AddSingleton<TService, TImplementation>();
services.AddHandler<THandler>();
services.AddCommandHandler<TCommand, THandler>();
services.AddCommandMiddleware<TMiddleware>();
services.AddAtomicEffectHandler<THandler>();
services.AddAdminEffectHandler<THandler>();
services.AddProjection<TProjection>();
services.AddAdminPage<TPage>();
services.AddBackgroundJob<TJob>();
services.AddRecurringScheduledCommand<TCommand>();
services.AddDomainEventSubscription<TSubscriber>(pattern);
services.AddHealthCheck<TCheck>();
services.RegisterAggregate<TAggregate>(strategy);
```

The methods are narrow host abstractions. A module does not receive the
concrete dependency-injection container, database context, Redis connection,
or transport client through this API.

### Commands

Commands are immutable application inputs:

```csharp
public interface ICommand
{
    string ModuleId { get; }
}

public interface ICommand<TResult> : ICommand;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken);
}

public interface ICommandBus
{
    Task SendAsync(ICommand command, CancellationToken cancellationToken);
    Task<TResult> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken);
}
```

`ICommandMiddleware` receives a `CommandContext` and `Func<Task> next` and may
short-circuit, wrap, or continue execution. Registration order defines the
onion pipeline order.

`CommandContext.Request` is `RequestMetadata`. It carries request and
correlation ids, client/culture, optional legacy user/scope strings, the
logical `BotChannel`, and the typed tenant/player context when available.
Use `RequestMetadata.FromTenantContext(...)` for tenant-bound calls and
`RequestMetadata.System(...)` for transport-neutral or background calls.
There is no public ambient `RequestContext` or `RequestContextAccessor` in the
1.0 surface. `RequestMetadataContext` is an infrastructure bridge used only
while dispatching a request.

### Pure decisions and effects

The game decision boundary is synchronous and deterministic:

```csharp
public interface IGameAction<TCommand, TState, TResult>
{
    GameDecision<TState, TResult> Decide(
        GameActionInput<TState, TCommand> input);
}

public interface IGameEffect;

public enum DecisionStatus
{
    Accepted,
    Rejected
}
```

`GameActionInput` contains command, state, wallet/quota snapshots, entropy,
UTC time, the operation `BotChannel`, and optional tenant-aware wallet/context
data. `GameDecision` returns the new state, result, rejection reason,
economy/quota effects, records, domain events, schedules, and optional custom
effects.

Decision code must not perform I/O. The host executes the declarative effect
set after the decision and applies the selected persistence/concurrency
strategy.

### Aggregates and events

Classical aggregates implement `IAggregateRoot` with a stable string `Id`.
Event-sourced aggregates additionally implement `IEventSourcedAggregate`,
which exposes a monotonic `Version`, pending `IDomainEvent` values, history
loading, and `MarkEventsCommitted()`.

Domain events implement `IDomainEvent`. They are deliberately separate from
executable game effects:

```csharp
public interface IDomainEvent
{
    string EventType { get; }
    long OccurredAt { get; }
}
```

`GameEffectSet.Events` contains domain events, while
`GameEffectSet.MaterializeEffects()` returns only executable effects. This
prevents an event from being accidentally sent through a custom effect
handler, while preserving one decision result and deterministic ordering.

Event type names are stable strings such as `module.action`. Modules may
subscribe through `AddDomainEventSubscription` using exact, module wildcard,
action wildcard, or total wildcard patterns.

Projections implement `IProjection`, declare subscribed event types, and
receive `ProjectionContext`. Projection handlers must be idempotent.

## 6. Scheduling

Modules declare scheduled work through `BotFramework.Scheduling.Abstractions`:

```csharp
public interface IScheduledCommand
{
    string Key { get; }
    Task ExecuteAsync(
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken);
}

public interface IRecurringScheduledCommand : IScheduledCommand
{
    ScheduleDescriptor Schedule { get; }
}

public interface IGameScheduler
{
    Task ScheduleAsync(GameScheduleCommand command, CancellationToken cancellationToken);
    Task TriggerNowAsync(
        string jobKey,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken);
    Task UnscheduleAsync(string scheduleId, CancellationToken cancellationToken);
}
```

`ScheduleDescriptor` supports cron, repeat interval, one-time execution,
timezone, misfire policy, concurrency policy, batch size, max attempts, and
retry backoff. Quartz is an implementation detail; modules depend on the
abstractions package.

## 7. Telegram and Discord abstractions

### Telegram

`BotFramework.Telegram.Abstractions` exposes `UpdateContext`,
`IUpdateHandler`, `IUpdateMiddleware`, and route attributes such as:

```csharp
[Command("/coinflip")]
public sealed class CoinFlipHandler : IUpdateHandler
{
    public Task HandleAsync(UpdateContext context) => ...;
}
```

Available route contracts include `Command`, `TextCommand`, `MessageDice`,
`CallbackPrefix`, `CallbackFallback`, `Message`, `ChannelPost`, `ChatMember`,
and `MyChatMember`. Route matching and priority are owned by the Telegram
adapter.

Tenant resolution is transport-specific but returns the neutral context:

```csharp
public interface ITelegramTenantContextResolver
{
    TenantContext Resolve(
        TelegramContainer container,
        RequestId requestId,
        RequestId correlationId);
}
```

### Discord

`BotFramework.Discord.Abstractions` exposes the equivalent tenant boundary:

```csharp
public interface IDiscordTenantContextResolver
{
    TenantContext Resolve(
        DiscordContainer container,
        RequestId requestId,
        RequestId correlationId);
}
```

Neither abstraction package puts Telegram or Discord types into the neutral
module/domain contracts beyond its own adapter boundary.

## 8. REST API

Register REST infrastructure in an ASP.NET Core host:

```csharp
builder.AddRestFramework();
app.UseRestFramework();
app.MapRestFramework();
```

Modules expose routes through:

```csharp
public interface IRestRouteModule
{
    string ModuleId { get; }
    void Map(IEndpointRouteBuilder endpoints);
}
```

Register the route module with `AddRestRouteModule<TModule>()`. The canonical
route group is:

```text
/api/v1/tenants/{tenantId}/scopes/{scopeId}/{moduleId}/{operation}
```

Use `MapRestGroup(moduleId)` inside a route module. Routes require
authorization and receive a typed `RestRequestContext`. State-changing
requests require `Idempotency-Key`; request and correlation ids are
propagated through `X-Request-ID` and `X-Correlation-ID`.

REST errors use `application/problem+json` and expose a stable `code`,
correlation id, HTTP status, detail, and optional retry metadata. The route
adapter maps transport errors; business modules return application contracts.

## 9. REST client

`BotFramework.Client` provides the shared transport behavior for generated or
hand-written module clients:

```csharp
var client = new BotFrameworkClient(httpClient, new BotFrameworkClientOptions(
    BaseAddress: new Uri("https://api.example/"),
    AccessTokenProvider: GetTokenAsync));

var result = await client.SendAsync<Request, Response>(
    HttpMethod.Post,
    module: "coinflip",
    operation: "play",
    tenantContext: new BotFrameworkTenantContext(tenantId, scopeId, playerId),
    body: request,
    idempotencyKey: commandId,
    cancellationToken: cancellationToken);
```

`BotFrameworkClient` builds the tenant-aware route, bearer token, request and
correlation headers, idempotency header, JSON body, and typed
`BotFrameworkApiException` from RFC 7807 responses.

The generated low-level client is derived from the checked-in
`framework/BotFramework.Client/openapi-v1.json`. It is generated during the
explicit client-generation build and is not a separate business contract.

## 10. Text processing API

`BotFramework.Text` is a reusable processing pipeline, not a moderation
module:

```csharp
services
    .AddTextProcessing()
    .AddTextAnalyzer<MyAnalyzer>()
    .AddTextPolicy<MyPolicy>()
    .AddTextObserver<MyObserver>()
    .AddTextEffectHandler<MyEffectHandler>();
```

Core contracts:

```csharp
public interface ITextProcessingPipeline
{
    ValueTask<TextPipelineResult> ProcessAsync(
        string text,
        TextProcessingContext? context = null,
        CancellationToken cancellationToken = default);

    ValueTask<TextPipelineResult> AnalyzeAsync(
        string text,
        TextProcessingContext? context = null,
        CancellationToken cancellationToken = default);
}

public interface ITextNormalizer
{
    NormalizedText Normalize(string text);
}

public interface ITextAnalyzer
{
    string Name { get; }
    int Order { get; }
    ValueTask<AnalysisResult> AnalyzeAsync(
        TextAnalysisContext context,
        CancellationToken cancellationToken = default);
}

public interface ITextPolicy
{
    string Name { get; }
    int Order { get; }
    ValueTask<PolicyDecision> EvaluateAsync(
        TextPolicyContext context,
        CancellationToken cancellationToken = default);
}
```

The pipeline normalizes once, runs analyzers in deterministic order, evaluates
policies, invokes observers, and optionally executes effects. `AnalyzeAsync`
never executes effects. `ProcessAsync` executes effects when an executor is
registered.

`NormalizedText` contains original/canonical text, tokens, source-span
mappings, and neutral `TextSignal` observations. `Match` is generic and maps
canonical spans back to original spans. Effects are data contracts such as
`ReplyEffect`, `DeleteMessageEffect`, `AddReactionEffect`, `QueueEffect`, and
`LogEffect`; a consumer registers handlers for the effects it supports.

For the complete normalization, matcher, policy, observer, and effect API see
[`botframework-text.md`](botframework-text.md).

## 11. Testing package

`BotFramework.Testing` contains in-memory/test-only implementations such as
repositories and economics fakes. It is a development dependency and must not
be required by production module runtime code.

The public test surface is intentionally smaller than the runtime surface.
Consumers should use their own test project and keep test doubles at the
composition boundary.

## 12. Serialization and wire rules

- Opaque ids serialize as strings.
- Enums in REST JSON use string values through the REST JSON configuration.
- Cursor values are opaque.
- Request and correlation ids are propagated, not regenerated inside modules.
- Integration envelopes carry message id, contract type, schema version,
  correlation/causation ids, tenant scope, player, channel, and serialized
  payload outside the domain event record.
- Public event and request names are stable strings and must be versioned when
  their payload contract changes.
- Removed protobuf fields remain reserved in their owning schema.

## 13. Explicit non-goals

The framework does not provide:

- profanity or forbidden-word dictionaries;
- censorship, spam, scam, or advertising policy;
- warnings, mutes, bans, or delete queues;
- admin pages for rule management;
- application dashboards or ClickHouse schemas;
- a generic scripting language or user-provided executable code;
- persistence implementation requirements for every module;
- Telegram/Discord business behavior inside neutral contracts.

These behaviors belong to consumer modules and application policies.

## 14. `1.0.0` release verification

The following checks are required for the `1.0.0` release:

1. package references use the agreed version set;
2. public API compatibility checks pass;
3. all package-only consumer samples restore without project references;
4. generated REST client verification passes;
5. `dotnet restore`, `dotnet build`, and `dotnet test` pass;
6. package contents, README, license, symbols, SourceLink, and deterministic
   output are verified;
7. release notes describe only the approved API;
8. the release tag is `framework-v1.0.0` and all package artifacts carry
   version `1.0.0`.

After these checks pass, the publishing workflow may push the ten supported
framework packages and create the corresponding GitHub Release.
