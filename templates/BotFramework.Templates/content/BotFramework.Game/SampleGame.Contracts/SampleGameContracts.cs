using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace SampleGame.Contracts;

public sealed record SampleGameCommand(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId PlayerId,
    string OperationId) : IRequest<SampleGameReply>
{
    public string MessageType => "sample-game.command.v1";
}

public sealed record SampleGameReply(int Version);
