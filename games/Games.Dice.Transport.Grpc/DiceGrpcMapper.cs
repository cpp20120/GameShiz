using BotFramework.Contracts.Messaging;
using Games.Dice.Contracts.Play;
using Games.Dice.Transport.Grpc.Wire;

namespace Games.Dice.Transport.Grpc;

internal static class DiceGrpcMapper
{
    public static DicePlayRequest ToContract(this DicePlayGrpcRequest request) => new(
        request.UserId,
        request.DisplayName,
        request.SlotValue,
        request.BalanceScopeId,
        request.OperationSourceId,
        request.IsForwarded);

    public static RequestMetadata ToContract(this RequestMetadataGrpc? metadata)
    {
        if (metadata is null)
            throw new ArgumentException("Request metadata is required.", nameof(metadata));

        return new RequestMetadata(
            metadata.RequestId,
            metadata.CorrelationId,
            metadata.ClientId,
            EmptyToNull(metadata.UserId),
            EmptyToNull(metadata.ScopeId),
            metadata.Culture,
            new Dictionary<string, string>(metadata.Baggage, StringComparer.Ordinal));
    }

    public static DicePlayGrpcResponse ToGrpc(this DicePlayResponse response) => new()
    {
        Status = response.Status switch
        {
            DicePlayStatus.Played => DicePlayStatusGrpc.DicePlayStatusPlayed,
            DicePlayStatus.Forwarded => DicePlayStatusGrpc.DicePlayStatusForwarded,
            DicePlayStatus.NotEnoughCoins => DicePlayStatusGrpc.DicePlayStatusNotEnoughCoins,
            DicePlayStatus.DailyRollLimitExceeded => DicePlayStatusGrpc.DicePlayStatusDailyRollLimitExceeded,
            _ => DicePlayStatusGrpc.DicePlayStatusUnspecified,
        },
        Prize = response.Prize,
        Stake = response.Stake,
        Balance = response.Balance,
        Tax = response.Tax,
        DailyRollsUsed = response.DailyRollsUsed,
        DailyRollLimit = response.DailyRollLimit,
    };

    public static RequestMetadataGrpc ToGrpc(this RequestMetadata metadata)
    {
        var result = new RequestMetadataGrpc
        {
            RequestId = metadata.RequestId,
            CorrelationId = metadata.CorrelationId,
            ClientId = metadata.ClientId,
            UserId = metadata.UserId ?? "",
            ScopeId = metadata.ScopeId ?? "",
            Culture = metadata.Culture,
        };
        foreach (var (key, value) in metadata.Baggage)
            result.Baggage.Add(key, value);
        return result;
    }

    public static DicePlayGrpcRequest ToGrpc(this DicePlayRequest request, RequestMetadata metadata) => new()
    {
        UserId = request.UserId,
        DisplayName = request.DisplayName,
        SlotValue = request.SlotValue,
        BalanceScopeId = request.BalanceScopeId,
        OperationSourceId = request.OperationSourceId,
        IsForwarded = request.IsForwarded,
        Metadata = metadata.ToGrpc(),
    };

    public static DicePlayResponse ToContract(this DicePlayGrpcResponse response) => new(
        response.Status switch
        {
            DicePlayStatusGrpc.DicePlayStatusPlayed => DicePlayStatus.Played,
            DicePlayStatusGrpc.DicePlayStatusForwarded => DicePlayStatus.Forwarded,
            DicePlayStatusGrpc.DicePlayStatusNotEnoughCoins => DicePlayStatus.NotEnoughCoins,
            DicePlayStatusGrpc.DicePlayStatusDailyRollLimitExceeded => DicePlayStatus.DailyRollLimitExceeded,
            _ => throw new InvalidOperationException("Backend returned an unspecified Dice status."),
        },
        response.Prize,
        response.Stake,
        response.Balance,
        response.Tax,
        response.DailyRollsUsed,
        response.DailyRollLimit);

    private static string? EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
}
