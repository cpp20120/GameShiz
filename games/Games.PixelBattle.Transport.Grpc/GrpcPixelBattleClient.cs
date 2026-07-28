using System.Text.Json;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Domain.Entities;
using Games.PixelBattle.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.PixelBattle.Transport.Grpc;

internal sealed class GrpcPixelBattleClient(PixelBattleApi.PixelBattleApiClient client) : IPixelBattleService
{
    public async Task<PixelBattleGrid> GetGridAsync(CancellationToken ct) =>
        (await client.GetGridAsync(new PixelCall(), cancellationToken: ct)).Read<PixelBattleGrid>();

    public async Task<PixelUpdateResult> UpdateAsync(long userId, int index, string color, CancellationToken ct) =>
        (await client.UpdateAsync(new PixelCall
        {
            PayloadJson = JsonSerializer.Serialize(new PixelUpdateCall(userId, index, color), PixelBattleWire.Options),
        }, cancellationToken: ct)).Read<PixelUpdateResult>();
}
