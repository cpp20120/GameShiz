// ─────────────────────────────────────────────────────────────────────────────
// Legacy read shape retained for source compatibility. New writes use the
// transaction-neutral DiceRollRecord through DiceRollRecordWriter.
// ─────────────────────────────────────────────────────────────────────────────

namespace Games.Dice.Infrastructure.Models;

public sealed record DiceRoll(
    Guid Id,
    long UserId,
    int DiceValue,
    int Prize,
    int Loss,
    DateTimeOffset RolledAt);
