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

public sealed class GrpcTransferService(TransferApi.TransferApiClient client) : ITransferService
{
    public Task<TransferAttemptResult> TryTransferAsync(long fromUserId, long toUserId, long chatId,
        string senderDisplayName, string recipientDisplayName, int netToRecipient, CancellationToken ct) =>
        TryTransferAsync(fromUserId, toUserId, chatId, senderDisplayName, recipientDisplayName,
            netToRecipient, 0, ct);

    public async Task<TransferAttemptResult> TryTransferAsync(long fromUserId, long toUserId, long chatId,
        string senderDisplayName, string recipientDisplayName, int netToRecipient, int sourceMessageId,
        CancellationToken ct)
    {
        var result = await client.TryTransferAsync(new TransferRequest
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            ChatId = chatId,
            SenderDisplayName = senderDisplayName,
            RecipientDisplayName = recipientDisplayName,
            NetToRecipient = netToRecipient,
            SourceMessageId = sourceMessageId,
        }, cancellationToken: ct);
        return new TransferAttemptResult((TransferError)result.Error, result.NetToRecipient,
            result.FeeCoins, result.TotalDebited, result.SenderBalance, result.RecipientBalance);
    }
}
