using System.Text.Json;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Domain.Entities;
using Games.PixelBattle.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.PixelBattle.Transport.Grpc;

public sealed class PixelBattleGrpcEndpoint(IPixelBattleService service) : PixelBattleApi.PixelBattleApiBase
{
    public override async Task<PixelReply> GetGrid(PixelCall request, ServerCallContext context) =>
        PixelBattleWire.Reply(await service.GetGridAsync(context.CancellationToken));

    public override async Task<PixelReply> Update(PixelCall request, ServerCallContext context)
    {
        var call = JsonSerializer.Deserialize<PixelUpdateCall>(request.PayloadJson, PixelBattleWire.Options)!;
        return PixelBattleWire.Reply(await service.UpdateAsync(call.UserId, call.Index, call.Color, context.CancellationToken));
    }
}
