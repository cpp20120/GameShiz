namespace BotFramework.Host.Workflows;

public sealed record DurableWorkflowReplayResult(bool Found, bool Enqueued, string Message);
