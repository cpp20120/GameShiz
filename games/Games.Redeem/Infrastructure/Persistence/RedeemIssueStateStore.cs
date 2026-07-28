using BotFramework.Host.Execution;
using Games.Redeem.Application.Execution;

namespace Games.Redeem.Infrastructure.Persistence;

public sealed class RedeemIssueStateStore : IGameStateStore<RedeemIssueCommand, RedeemExecutionState>
{
    public async Task<RedeemExecutionState> LoadAsync(
        RedeemIssueCommand command, IGameExecutionContext context, CancellationToken ct) =>
        new(await RedeemAtomicSql.LoadAsync(command.Code, context, ct));

    public async Task SaveAsync(
        RedeemIssueCommand command, RedeemExecutionState state, IGameExecutionContext context, CancellationToken ct)
    {
        var code = state.Code ?? throw new InvalidOperationException("Issued redeem code is missing.");
        var inserted = await context.ExecuteAsync("""
            INSERT INTO redeem_codes (code,active,issued_by,issued_at,free_spin_game_id)
            VALUES (@Code,@Active,@IssuedBy,@IssuedAt,@FreeSpinGameId)
            """, new { code.Code, code.Active, code.IssuedBy, code.IssuedAt, code.FreeSpinGameId }, ct);
        if (inserted != 1) throw new InvalidOperationException("Redeem code was not inserted.");
    }
}
