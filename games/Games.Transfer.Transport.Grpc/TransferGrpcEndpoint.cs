using Games.Transfer.Application.Results;
using Games.Transfer.Application.Services;
using Games.Transfer.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Transfer.Transport.Grpc;

public sealed class TransferGrpcEndpoint(ITransferService service) : TransferApi.TransferApiBase
{
    public override async Task<TransferResponse> TryTransfer(TransferRequest request, ServerCallContext context) =>
        Map(await service.TryTransferAsync(
            request.FromUserId, request.ToUserId, request.ChatId,
            request.SenderDisplayName, request.RecipientDisplayName,
            request.NetToRecipient, request.SourceMessageId, context.CancellationToken));

    internal static TransferResponse Map(TransferAttemptResult result) => new()
    {
        Error = (int)result.Error,
        NetToRecipient = result.NetToRecipient,
        FeeCoins = result.FeeCoins,
        TotalDebited = result.TotalDebited,
        SenderBalance = result.SenderBalance,
        RecipientBalance = result.RecipientBalance,
    };
}
