using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace CoinFlip.Contracts;

public sealed record CoinFlipCommand(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId PlayerId,
    string OperationId) : IRequest<CoinFlipReply>
{
    public string MessageType => "coin-flip.flip.v1";
}

public sealed record CoinFlipReply(
    string Side,
    int Flips,
    int Heads,
    int Tails,
    bool Pending = false,
    string? CommandId = null);
