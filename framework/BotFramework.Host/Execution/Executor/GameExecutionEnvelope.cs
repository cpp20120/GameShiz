namespace BotFramework.Host.Execution;

public sealed record GameExecutionEnvelope<TCommand>(TCommand Command);
