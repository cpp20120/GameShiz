namespace BotFramework.Host.Workflows;

/// <summary>
/// Marker for commands that may be persisted and replayed by the durable
/// workflow host. Commands should be immutable records with bounded payloads.
/// </summary>
public interface IDurableWorkflowCommand
{
}
