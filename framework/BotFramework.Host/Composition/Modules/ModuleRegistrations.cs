namespace BotFramework.Host.Composition;

/// <summary>Mutable registration bag populated during module loading and consumed at app start.</summary>
public sealed class ModuleRegistrations
{
    public List<AggregateRegistration> Aggregates { get; } = [];
    public List<Type> Projections { get; } = [];
    public List<Type> AdminPages { get; } = [];
    public List<Type> BackgroundJobs { get; } = [];
    public Dictionary<Type, Type> CommandHandlers { get; } = new();
    public List<Type> CommandMiddleware { get; } = [];
    public List<EventSubscription> EventSubscriptions { get; } = [];
    public List<Type> HealthChecks { get; } = [];
}
