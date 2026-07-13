// ─────────────────────────────────────────────────────────────────────────────
// ModuleServiceCollectionAdapter — the surface modules see during
// ConfigureServices(). Every call forwards to the real IServiceCollection
// (for DI wiring) and, where relevant, records the registration in a
// ModuleRegistrations bag the Host consumes later.
//
// Why the two-phase split:
//   • DI registration must happen now — `IServiceCollection` is frozen once
//     `builder.Build()` runs.
//   • The CommandBus' handler map, the InProcessEventBus' subscription map,
//     the EventDispatcher's projection list, the AdminMount's page index —
//     all need to be built AFTER modules have had a chance to declare their
//     intent. Those consumers resolve ModuleRegistrations at startup and
//     walk its lists.
//
// The bag carries registration metadata only — no instances. Everything is
// still resolved from DI at request time so Scoped lifetimes work correctly.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BotFramework.Scheduling.Abstractions;
using BotFramework.Host.Configuration.Validation;
using BotFramework.Sdk.Configuration;

namespace BotFramework.Host.Composition.Modules;

public sealed class ModuleServiceCollectionAdapter(
    IServiceCollection services,
    IConfiguration configuration,
    ModuleRegistrations registrations) : IModuleServiceCollection
{
    public IModuleServiceCollection BindOptions<TOptions>(string configSection) where TOptions : class
    {
        services.AddRegisteredConfigurationSection<TOptions>(configuration, configSection);
        return this;
    }

    public IModuleServiceCollection BindOptions<TOptions, TValidator>(string configSection)
        where TOptions : class
        where TValidator : class, IConfigurationValidator<TOptions>
    {
        services.AddRegisteredConfigurationSection<TOptions, TValidator>(configuration, configSection);
        return this;
    }

    public IModuleServiceCollection AddScoped<TService, TImpl>()
        where TService : class
        where TImpl : class, TService
    {
        services.AddScoped<TService, TImpl>();
        return this;
    }

    public IModuleServiceCollection AddScoped<TImplementation>() where TImplementation : class
    {
        services.AddScoped<TImplementation>();
        return this;
    }

    public IModuleServiceCollection AddSingleton<TService, TImpl>() where TImpl : class, TService
    {
        services.AddSingleton(typeof(TService), typeof(TImpl));
        return this;
    }

    public IModuleServiceCollection AddSingleton<TImplementation>() where TImplementation : class
    {
        services.AddSingleton<TImplementation>();
        return this;
    }

    public IModuleServiceCollection RegisterAggregate<TAggregate>(PersistenceStrategy strategy)
        where TAggregate : IAggregateRoot
    {
        registrations.AddAggregate(new AggregateRegistration(typeof(TAggregate), strategy));

        if (strategy != PersistenceStrategy.EventSourced) return this;
        if (!typeof(IEventSourcedAggregate).IsAssignableFrom(typeof(TAggregate)))
        {
            throw new InvalidOperationException(
                $"Aggregate {typeof(TAggregate).FullName} is registered as EventSourced " +
                $"but does not implement {nameof(IEventSourcedAggregate)}.");
        }

        var aggregateType = typeof(TAggregate);
        var repositoryServiceType = typeof(IRepository<>).MakeGenericType(aggregateType);
        var repositoryImplementationType = typeof(EventSourcedRepository<>).MakeGenericType(aggregateType);
        var factoryServiceType = typeof(IAggregateFactory<>).MakeGenericType(aggregateType);
        var factoryImplementationType = typeof(DefaultAggregateFactory<>).MakeGenericType(aggregateType);

        services.TryAddScoped(factoryServiceType, factoryImplementationType);
        services.AddScoped(repositoryServiceType, repositoryImplementationType);

        return this;
    }

    public IModuleServiceCollection AddHandler<THandler>() where THandler : class
    {
        // Handlers are Scoped. UpdateRouter resolves by Type via reflection at dispatch.
        services.AddScoped<THandler>();
        return this;
    }

    public IModuleServiceCollection AddProjection<TProjection>() where TProjection : class, IProjection
    {
        services.AddScoped<TProjection>();
        services.AddScoped<IProjection>(sp => sp.GetRequiredService<TProjection>());
        registrations.AddProjection<TProjection>();
        return this;
    }

    public IModuleServiceCollection AddAdminPage<TPage>() where TPage : class, IAdminPage
    {
        services.AddScoped<TPage>();
        services.AddScoped<IAdminPage>(sp => sp.GetRequiredService<TPage>());
        registrations.AddAdminPage<TPage>();
        return this;
    }

    public IModuleServiceCollection AddBackgroundJob<TJob>() where TJob : class, IBackgroundJob
    {
        services.AddSingleton<TJob>();
        registrations.AddBackgroundJob<TJob>();
        return this;
    }

    public IModuleServiceCollection AddRecurringScheduledCommand<TCommand>()
        where TCommand : class, IRecurringScheduledCommand
    {
        services.AddScoped<TCommand>();
        services.AddScoped<IRecurringScheduledCommand>(sp => sp.GetRequiredService<TCommand>());
        services.AddScoped<IScheduledCommand>(sp => sp.GetRequiredService<TCommand>());
        return this;
    }

    public IModuleServiceCollection AddCommandHandler<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        services.AddScoped<ICommandHandler<TCommand>, THandler>();
        registrations.AddCommandHandler<TCommand, THandler>();
        return this;
    }

    public IModuleServiceCollection AddCommandMiddleware<TMiddleware>()
        where TMiddleware : class, ICommandMiddleware
    {
        services.AddSingleton<TMiddleware>();
        services.AddSingleton<ICommandMiddleware>(sp => sp.GetRequiredService<TMiddleware>());
        registrations.AddCommandMiddleware<TMiddleware>();
        return this;
    }

    public IModuleServiceCollection AddDomainEventSubscription<TSubscriber>(string eventTypePattern)
        where TSubscriber : class, IDomainEventSubscriber
    {
        // Subscribers are singletons because InProcessEventBus holds instance
        // references built at startup. If a subscriber needs per-event scoped
        // state, it takes IServiceProvider and creates its own scope inside
        // HandleAsync.
        services.AddSingleton<TSubscriber>();
        registrations.AddEventSubscription(new EventSubscription(eventTypePattern, typeof(TSubscriber)));
        return this;
    }

    public IModuleServiceCollection AddHealthCheck<TCheck>() where TCheck : class, IHealthCheck
    {
        services.AddSingleton<TCheck>();
        services.AddSingleton<IHealthCheck>(sp => sp.GetRequiredService<TCheck>());
        registrations.AddHealthCheck<TCheck>();
        return this;
    }
}
