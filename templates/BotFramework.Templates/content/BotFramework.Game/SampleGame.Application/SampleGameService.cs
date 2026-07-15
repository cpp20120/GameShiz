using SampleGame.Contracts;
using SampleGame.Domain;

namespace SampleGame.Application;

public sealed class SampleGameService
{
    public SampleGameReply Execute(SampleGameCommand command, SampleGameState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new SampleGameReply(SampleGameRules.Apply(state).Version);
    }
}
