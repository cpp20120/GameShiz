using System.Text.Json;
using Games.Blackjack.Contracts;
using Games.Blackjack.Domain.Results;
using Games.Blackjack.Transport.Grpc.Wire;
using Grpc.Core;
namespace Games.Blackjack.Transport.Grpc;
internal sealed class EmptyReply
{
    public static EmptyReply Create() => new();
}
