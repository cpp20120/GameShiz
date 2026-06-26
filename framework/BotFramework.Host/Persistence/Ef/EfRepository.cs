using Microsoft.EntityFrameworkCore;

namespace BotFramework.Host.Persistence.Ef;

public sealed class EfRepository<TAggregate, TContext>(TContext db) : IRepository<TAggregate>
    where TAggregate : class, IAggregateRoot
    where TContext : DbContext
{
    public async Task<TAggregate?> FindAsync(string id, CancellationToken ct)
    {
        return await db.Set<TAggregate>().FindAsync([id], ct);
    }

    public async Task SaveAsync(TAggregate aggregate, CancellationToken ct)
    {
        var existing = await db.Set<TAggregate>().FindAsync([aggregate.Id], ct);
        if (existing is null)
        {
            db.Set<TAggregate>().Add(aggregate);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(aggregate);
        }
        await db.SaveChangesAsync(ct);
    }
}
