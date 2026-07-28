namespace BotFramework.Sdk.Execution;

public sealed record AtomicEffectPlan<TResult>(
    TResult Result,
    IReadOnlyList<IAtomicEffect> Effects,
    Func<IReadOnlyDictionary<string, object?>, TResult>? ResultFactory = null);