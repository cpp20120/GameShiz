namespace BotFramework.Contracts.Messaging;

public sealed record IntegrationInboxResult<TResult>(bool AlreadyProcessed, TResult? Result);

public sealed record IntegrationInboxResult(bool AlreadyProcessed);
