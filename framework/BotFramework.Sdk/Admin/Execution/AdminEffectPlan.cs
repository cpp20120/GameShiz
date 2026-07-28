namespace BotFramework.Sdk.Admin.Execution;

public sealed record AdminEffectPlan<TResult>(
    TResult Result,
    IReadOnlyList<IAdminEffect> Effects,
    Func<IReadOnlyDictionary<string, object?>, TResult>? ResultFactory = null);