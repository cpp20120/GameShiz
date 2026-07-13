namespace BotFramework.Sdk.Execution;

/// <summary>A player command addressed to one exact aggregate revision.</summary>
public interface ITurnGameCommand<out TPlayerId>
{
    TPlayerId PlayerId { get; }

    long ExpectedRevision { get; }
}
