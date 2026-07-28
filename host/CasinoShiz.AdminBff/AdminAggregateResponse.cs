using BotFramework.Contracts.Operations;
using BotFramework.Host.Contracts.Economics;

namespace CasinoShiz.AdminBff;

internal sealed record AdminAggregateResponse(
    IReadOnlyList<OperationFailure> Failures,
    IReadOnlyList<OperationOutbox> Outbox,
    IReadOnlyList<OperationJob> Jobs,
    WalletHealth WalletHealth);
