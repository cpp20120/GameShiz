// ─────────────────────────────────────────────────────────────────────────────
// IModule — the contract every game implements.
//
// A module is a self-contained feature: its DI registrations, entity
// configurations, locales, route handlers, and config options all live here.
// Adding a game to a Host = reference the module's assembly + register it once.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Scheduling.Abstractions;

namespace BotFramework.Sdk.Modules;
// ─────────────────────────────────────────────────────────────────────────────
// Host-supplied abstractions that modules bind against. Modules never see the
// concrete DI container type — they see these narrower views. That keeps them
// portable across Host implementations and testable in isolation.
// ─────────────────────────────────────────────────────────────────────────────

public interface IModuleServiceCollection
{
    /// <summary>
    /// Binds a section of Host config (e.g. Games:poker) into a typed options
    /// class. Host wraps IConfiguration so modules don't take a dependency on
    /// Microsoft.Extensions.Configuration directly.
    /// </summary>
    IModuleServiceCollection BindOptions<TOptions>(string configSection) where TOptions : class;

    /// <summary>
    /// Binds and validates a configuration section. The Host runs the validator
    /// during startup and for every admin runtime-configuration patch.
    /// </summary>
    IModuleServiceCollection BindOptions<TOptions, TValidator>(string configSection)
        where TOptions : class
        where TValidator : class, IConfigurationValidator<TOptions>;

    IModuleServiceCollection AddScoped<TService, TImpl>()
        where TService : class
        where TImpl : class, TService;
    IModuleServiceCollection AddScoped<TImplementation>() where TImplementation : class;
    IModuleServiceCollection AddSingleton<TService, TImpl>() where TImpl : class, TService;

    /// <summary>Registers a concrete type as its own singleton implementation (e.g. background workers).</summary>
    IModuleServiceCollection AddSingleton<TImplementation>() where TImplementation : class;

    /// <summary>
    /// 
    /// </summary>
    /// Registers a domain aggregate with its persistence strategy. Host picks
    /// the right IRepository<TAggregate> implementation based on the strategy.
    IModuleServiceCollection RegisterAggregate<TAggregate>(PersistenceStrategy strategy)
        where TAggregate : IAggregateRoot;

    /// <summary>
    /// Registers an IUpdateHandler type; Host adds it to the attribute-scanned
    /// handler set so routing picks up the handler's [Command]/[CallbackPrefix]
    /// attributes automatically.
    /// </summary>
    IModuleServiceCollection AddHandler<THandler>() where THandler : class;

    /// <summary>
    /// Registers a read-model projection. Host wires it into the event
    /// dispatcher so events this projection subscribes to flow through it in
    /// the same transaction as the event-store append.
    /// </summary>
    IModuleServiceCollection AddProjection<TProjection>() where TProjection : class, IProjection;

    /// <summary>
    /// 
    /// </summary>
    /// Registers an admin page. Host mounts it at <c>/admin/&lt;moduleId&gt;/&lt;page.Route&gt;</c>
    /// after AdminWebToken middleware passes.
    IModuleServiceCollection AddAdminPage<TPage>() where TPage : class, IAdminPage;

    /// <summary>
    /// Registers a background job. Host hosts it as an IHostedService, runs
    /// RunAsync on startup, signals cancellation on shutdown.
    /// </summary>
    IModuleServiceCollection AddBackgroundJob<TJob>() where TJob : class, IBackgroundJob;

    /// <summary>Registers a module-owned recurring job run by the host scheduler.</summary>
    IModuleServiceCollection AddRecurringScheduledCommand<TCommand>()
        where TCommand : class, IRecurringScheduledCommand;

    /// <summary>
    /// Registers a command handler. Bus dispatches through every middleware
    /// registered with AddCommandMiddleware before reaching this handler.
    /// </summary>
    IModuleServiceCollection AddCommandHandler<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>;

    /// <summary>
    /// Registers a command-pipeline middleware. Host-level concerns (logging,
    /// metrics, rate-limit) come from the Host; modules add their own only
    /// when they need per-module behavior that can't be parameterized.
    /// </summary>
    IModuleServiceCollection AddCommandMiddleware<TMiddleware>() where TMiddleware : class, ICommandMiddleware;

    /// <summary>
    /// Subscribes a handler to a cross-module domain-event pattern. Pattern
    /// grammar: exact ("sh.game_ended"), module wildcard ("sh.*"), action
    /// wildcard ("*.game_ended"), total wildcard ("*").
    /// </summary>
    IModuleServiceCollection AddDomainEventSubscription<TSubscriber>(string eventTypePattern)
        where TSubscriber : class, IDomainEventSubscriber;

    /// <summary>
    /// Registers a health check. Host aggregates them at /health (liveness +
    /// readiness). Slow checks belong in the background job, not here.
    /// </summary>
    IModuleServiceCollection AddHealthCheck<TCheck>() where TCheck : class, IHealthCheck;
}
