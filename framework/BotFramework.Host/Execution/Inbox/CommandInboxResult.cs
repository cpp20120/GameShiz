namespace BotFramework.Host.Execution;

internal sealed record CommandInboxResult<TResult>(CommandInboxStatus Status, TResult? Result);
