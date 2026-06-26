namespace BotFramework.Host.Composition.Modules;

/// <summary>
/// Mutable registration bag populated during module loading
/// and consumed during application startup.
/// </summary>
public sealed class ModuleRegistrations
{
    private readonly List<AggregateRegistration> _aggregates = [];
    private readonly List<Type> _projections = [];
    private readonly List<Type> _adminPages = [];
    private readonly List<Type> _backgroundJobs = [];
    private readonly Dictionary<Type, Type> _commandHandlers = [];
    private readonly List<Type> _commandMiddleware = [];
    private readonly List<EventSubscription> _eventSubscriptions = [];
    private readonly List<Type> _healthChecks = [];

    public IReadOnlyList<AggregateRegistration> Aggregates => _aggregates;

    public IReadOnlyList<Type> Projections => _projections;

    public IReadOnlyList<Type> AdminPages => _adminPages;

    public IReadOnlyList<Type> BackgroundJobs => _backgroundJobs;

    public IReadOnlyDictionary<Type, Type> CommandHandlers => _commandHandlers;

    public IReadOnlyList<Type> CommandMiddleware => _commandMiddleware;

    public IReadOnlyList<EventSubscription> EventSubscriptions =>
        _eventSubscriptions;

    public IReadOnlyList<Type> HealthChecks => _healthChecks;

    public void AddAggregate(AggregateRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        _aggregates.Add(registration);
    }

    private void AddProjection(Type projectionType)
    {
        ArgumentNullException.ThrowIfNull(projectionType);

        _projections.Add(projectionType);
    }

    public void AddProjection<TProjection>()
    {
        AddProjection(typeof(TProjection));
    }

    private void AddAdminPage(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        _adminPages.Add(pageType);
    }

    public void AddAdminPage<TPage>()
    {
        AddAdminPage(typeof(TPage));
    }

    private void AddBackgroundJob(Type jobType)
    {
        ArgumentNullException.ThrowIfNull(jobType);

        _backgroundJobs.Add(jobType);
    }

    public void AddBackgroundJob<TJob>()
    {
        AddBackgroundJob(typeof(TJob));
    }

    private void AddCommandHandler(
        Type commandType,
        Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(handlerType);

        _commandHandlers.Add(commandType, handlerType);
    }

    public void AddCommandHandler<TCommand, THandler>()
    {
        AddCommandHandler(typeof(TCommand), typeof(THandler));
    }

    private void AddCommandMiddleware(Type middlewareType)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);

        _commandMiddleware.Add(middlewareType);
    }

    public void AddCommandMiddleware<TMiddleware>()
    {
        AddCommandMiddleware(typeof(TMiddleware));
    }

    public void AddEventSubscription(EventSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        _eventSubscriptions.Add(subscription);
    }

    private void AddHealthCheck(Type healthCheckType)
    {
        ArgumentNullException.ThrowIfNull(healthCheckType);

        _healthChecks.Add(healthCheckType);
    }

    public void AddHealthCheck<THealthCheck>()
    {
        AddHealthCheck(typeof(THealthCheck));
    }
}