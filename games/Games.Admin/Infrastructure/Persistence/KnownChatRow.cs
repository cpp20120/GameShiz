// ─────────────────────────────────────────────────────────────────────────────
// ChatsStore — read-only query layer for the admin /chats command.
//
// Joins the framework-owned `known_chats` table (populated by
// KnownChatsMiddleware on every update) with the `users` table so the admin
// list shows engagement at a glance: how many wallets that chat has and the
// summed balance per chat. No mutation methods live here — the migrations
// for `known_chats` itself ship inside the framework.
// ─────────────────────────────────────────────────────────────────────────────

namespace Games.Admin.Infrastructure.Persistence;

public sealed record KnownChatRow(
    long ChatId,
    string ChatType,
    string? Title,
    string? Username,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    int UserCount,
    long TotalCoins);
