using Microsoft.EntityFrameworkCore;

namespace Games.SecretHitler.Infrastructure.Persistence;

public sealed class SecretHitlerGameStore(SecretHitlerDbContext db) : ISecretHitlerGameStore
{
    public Task<SecretHitlerGame?> FindAsync(string inviteCode, CancellationToken ct) =>
        db.Games.AsNoTracking().SingleOrDefaultAsync(x => x.InviteCode == inviteCode, ct);

    public Task<SecretHitlerGame?> FindOpenByChatAsync(long chatId, CancellationToken ct) =>
        db.Games.AsNoTracking().FirstOrDefaultAsync(
            x => x.ChatId == chatId && (x.Status == ShStatus.Lobby || x.Status == ShStatus.Active), ct);

    public Task<bool> CodeExistsAsync(string inviteCode, CancellationToken ct) =>
        db.Games.AsNoTracking().AnyAsync(x => x.InviteCode == inviteCode, ct);

    public async Task InsertAsync(SecretHitlerGame game, CancellationToken ct)
    {
        db.Games.Add(game);
        await db.SaveChangesAsync(ct);
        db.Entry(game).State = EntityState.Detached;
    }

    public async Task UpdateAsync(SecretHitlerGame game, CancellationToken ct)
    {
        db.Games.Update(game);
        await db.SaveChangesAsync(ct);
        db.Entry(game).State = EntityState.Detached;
    }

    public Task UpsertStateMessageAsync(string inviteCode, int messageId, CancellationToken ct) =>
        db.Games.Where(x => x.InviteCode == inviteCode)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.StateMessageId, messageId), ct);
}
