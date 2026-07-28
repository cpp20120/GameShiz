using BotFramework.Host.Execution;
using Games.Redeem.Application.Execution;

namespace Games.Redeem.Infrastructure.Persistence;

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
