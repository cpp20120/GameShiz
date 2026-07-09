using Microsoft.EntityFrameworkCore;

namespace Games.Challenges.Infrastructure.Persistence;

public sealed class ChallengeStore(
    ChallengeDbContext db,
    IPlayerDirectory players,
    IWalletReadService wallets) : IChallengeStore
{
    public async Task<ChallengeUser?> FindKnownUserByUsernameAsync(long chatId, string username, CancellationToken ct)
    {
        var identity = await players.FindByUsernameAsync(username, ct);
        return identity is null || await wallets.GetAsync(identity.UserId, chatId, ct) is null
            ? null : new ChallengeUser(identity.UserId, identity.DisplayName);
    }

    public Task<bool> HasPendingAsync(long challengerId, long targetId, long chatId, CancellationToken ct) =>
        db.Challenges.AsNoTracking().AnyAsync(x => x.ChatId == chatId && x.Status == "Pending"
            && x.ExpiresAt > DateTimeOffset.UtcNow
            && ((x.ChallengerId == challengerId && x.TargetId == targetId)
                || (x.ChallengerId == targetId && x.TargetId == challengerId)), ct);

    public async Task InsertAsync(Challenge challenge, CancellationToken ct)
    {
        db.Challenges.Add(ChallengeEntity.From(challenge));
        await db.SaveChangesAsync(ct);
    }

    public async Task<Challenge?> FindAsync(Guid id, CancellationToken ct) =>
        (await db.Challenges.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct))?.ToDomain();

    public async Task<bool> TryMarkStatusAsync(Guid id, ChallengeStatus expected, ChallengeStatus next, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var terminal = next is ChallengeStatus.Completed or ChallengeStatus.Failed or ChallengeStatus.Declined;
        var changed = await db.Challenges.Where(x => x.Id == id && x.Status == expected.ToString())
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, next.ToString())
                .SetProperty(x => x.RespondedAt, x => x.RespondedAt ?? now)
                .SetProperty(x => x.CompletedAt, x => terminal ? now : x.CompletedAt), ct);
        return changed > 0;
    }
}
