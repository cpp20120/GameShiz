namespace BotFramework.Sdk.Execution;

/// <summary>State whose mutations are protected by an aggregate revision.</summary>
public interface IVersionedGameState
{
    long Revision { get; }
}
