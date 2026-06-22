// ─────────────────────────────────────────────────────────────────────────────
// DiceCubeBetStore — Dapper-backed persistence for pending cube bets. A bet
// is keyed by (user_id, chat_id) so the same user can have independent bets
// in multiple group chats.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Dapper;

namespace Games.DiceCube;

public sealed record DiceCubeBet(
    long UserId,
    long ChatId,
    int Amount,
    DateTimeOffset CreatedAt,
    int Mult4,
    int Mult5,
    int Mult6);
