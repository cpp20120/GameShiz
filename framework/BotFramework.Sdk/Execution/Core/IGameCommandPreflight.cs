namespace BotFramework.Sdk.Execution;

/// <summary>
/// Optional pure command validation that can reject a command before the
/// executor loads wallet and aggregate state. The executor still preserves
/// command idempotency and records entropy for a rejected command.
/// </summary>
public interface IGameCommandPreflight<in TCommand, TResult>
{
    bool TryReject(TCommand command, out TResult result, out string rejectionReason);
}
