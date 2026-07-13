using System.Text.Json;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Domain.Entities;
using Games.PixelBattle.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.PixelBattle.Transport.Grpc;

internal sealed record PixelUpdateCall(long UserId, int Index, string Color);

internal static class PixelBattleWire
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    internal static PixelReply Reply<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    internal static T Read<T>(this PixelReply value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}

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
