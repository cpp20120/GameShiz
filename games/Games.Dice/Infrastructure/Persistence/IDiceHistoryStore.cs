// ─────────────────────────────────────────────────────────────────────────────
// Module-private persistence boundary. Stateless modules don't get an
// IRepository<T> (no aggregate); they append audit rows directly through a
// narrow write-only interface. Dapper keeps us free of DbContext coupling the
// way the live codebase already prefers (see a3457e4 "move to postgres move
// economics to dapper").
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Dapper;

namespace Games.Dice;

public interface IDiceHistoryStore
{
    Task AppendAsync(DiceRoll roll, CancellationToken ct);
}
