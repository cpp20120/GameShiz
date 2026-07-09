using Microsoft.EntityFrameworkCore;

namespace Games.SecretHitler.Infrastructure.Persistence;

public sealed class SecretHitlerPlayerStore(SecretHitlerDbContext db) : ISecretHitlerPlayerStore
{
    public Task<SecretHitlerPlayer?> FindByUserAsync(long userId, CancellationToken ct) =>
        db.Players.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public Task<List<SecretHitlerPlayer>> ListByGameAsync(string inviteCode, CancellationToken ct) =>
        db.Players.AsNoTracking().Where(x => x.InviteCode == inviteCode).ToListAsync(ct);

    public Task<bool> AnyForUserAsync(long userId, CancellationToken ct) =>
        db.Players.AsNoTracking().AnyAsync(x => x.UserId == userId, ct);

    public Task<int> CountByGameAsync(string inviteCode, CancellationToken ct) =>
        db.Players.AsNoTracking().CountAsync(x => x.InviteCode == inviteCode, ct);

    public async Task InsertAsync(SecretHitlerPlayer player, CancellationToken ct)
    {
        db.Players.Add(player);
        await db.SaveChangesAsync(ct);
        db.Entry(player).State = EntityState.Detached;
    }

    public async Task UpdateAsync(SecretHitlerPlayer player, CancellationToken ct)
    {
        db.Players.Update(player);
        await db.SaveChangesAsync(ct);
        db.Entry(player).State = EntityState.Detached;
    }

    public Task DeleteAsync(string inviteCode, int position, CancellationToken ct) =>
        db.Players.Where(x => x.InviteCode == inviteCode && x.Position == position).ExecuteDeleteAsync(ct);

    public Task UpsertStateMessageAsync(long userId, int messageId, CancellationToken ct) =>
        db.Players.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.StateMessageId, messageId), ct);
}
