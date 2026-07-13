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

internal static class RedeemAtomicSql
{
    public static async Task<RedeemCode?> LoadAsync(
        Guid code, IGameExecutionContext context, CancellationToken ct)
    {
        var row = await context.QuerySingleOrDefaultAsync<Row>("""
            SELECT code AS Code,active AS Active,issued_by AS IssuedBy,issued_at AS IssuedAt,
                   free_spin_game_id AS FreeSpinGameId,redeemed_by AS RedeemedBy,redeemed_at AS RedeemedAt
            FROM redeem_codes WHERE code=@code FOR UPDATE
            """, new { code }, ct);
        return row?.ToDomain();
    }

    private sealed record Row(Guid Code, bool Active, long IssuedBy, long IssuedAt,
        string FreeSpinGameId, long? RedeemedBy, long? RedeemedAt)
    {
        public RedeemCode ToDomain() => new()
        {
            Code = Code, Active = Active, IssuedBy = IssuedBy, IssuedAt = IssuedAt,
            FreeSpinGameId = FreeSpinGameId, RedeemedBy = RedeemedBy, RedeemedAt = RedeemedAt,
        };
    }
}
