using Dapper;

namespace Games.Redeem.Infrastructure.Persistence;

public interface IRedeemStore
{
    Task<RedeemCode?> FindAsync(Guid code, CancellationToken ct);
    Task InsertAsync(RedeemCode code, CancellationToken ct);
    Task<bool> MarkRedeemedAsync(Guid code, long redeemedBy, long redeemedAt, CancellationToken ct);
}
