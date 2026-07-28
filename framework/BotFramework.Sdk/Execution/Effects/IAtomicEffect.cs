namespace BotFramework.Sdk.Execution;

/// <summary>
/// A mutation that is applied by the Host inside one transaction. Unlike a
/// game decision, an atomic effect is useful for workflows that do not have a
/// user-facing pure action (claims, tournaments and scheduled settlement).
/// </summary>
public interface IAtomicEffect;
