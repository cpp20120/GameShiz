using BotFramework.Host.Execution;
using Games.Redeem.Application.Execution;

namespace Games.Redeem.Infrastructure.Persistence;

public sealed class RedeemCompleteStateStore : IGameStateStore<RedeemCompleteCommand, RedeemExecutionState>
{
    public async Task<RedeemExecutionState> LoadAsync(
        RedeemCompleteCommand command, IGameExecutionContext context, CancellationToken ct) =>
        new(await RedeemAtomicSql.LoadAsync(command.Code, context, ct));

    public async Task SaveAsync(
        RedeemCompleteCommand command, RedeemExecutionState state, IGameExecutionContext context, CancellationToken ct)
    {
        var code = state.Code ?? throw new InvalidOperationException("Redeemed code is missing.");
        var updated = await context.ExecuteAsync("""
            UPDATE redeem_codes SET active=false,redeemed_by=@RedeemedBy,redeemed_at=@RedeemedAt
            WHERE code=@Code AND active=true
            """, new { code.Code, code.RedeemedBy, code.RedeemedAt }, ct);
        if (updated != 1) throw new InvalidOperationException("Redeem code changed before commit.");
    }
}
