// ─────────────────────────────────────────────────────────────────────────────
// PokerService — application service for /poker.
//
// Port of src/CasinoShiz.Core/Services/Poker/Application/PokerService.cs.
// Differences vs. the monolith:
//   • Dapper stores (IPokerTableStore / IPokerSeatStore) replace EF Core.
//   • IEconomicsService is userId-based (no UserState entity round-trips).
//   • Analytics goes through IAnalyticsService instead of ClickHouseReporter.
//   • Domain events (PokerHandStarted / PokerHandEnded) are published on the
//     IDomainEventBus for cross-module subscribers.
//
// Concurrency: per-table SemaphoreSlim gates keyed by invite code. Table-creating
// operations gate by "u:{userId}" until a code is assigned.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Concurrent;
using BotFramework.Host;
using BotFramework.Sdk;
using Games.Poker.Domain;
using static Games.Poker.Domain.Rules.PokerResultHelpers;

namespace Games.Poker.Application.Services;

public interface IPokerService
{
    Task<(TableSnapshot? Snapshot, PokerSeat? MySeat)> FindMyTableAsync(
        long userId, long currentChatId, CancellationToken ct);
    Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, CancellationToken ct);
    Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct);
    Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, CancellationToken ct);
    Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, int sourceMessageId, CancellationToken ct);
    Task<StartResult> StartHandAsync(long userId, long currentChatId, CancellationToken ct);
    Task<ActionResult> ApplyPlayerActionAsync(
        long userId, long currentChatId, string verb, int amount, CancellationToken ct);
    Task<ActionResult?> RunAutoActionAsync(string inviteCode, CancellationToken ct);
    Task<LeaveResult> LeaveTableAsync(long userId, long currentChatId, CancellationToken ct);
    Task SetTableStateMessageIdAsync(string inviteCode, int messageId, CancellationToken ct);
    Task<IReadOnlyList<string>> ListStuckCodesAsync(long cutoffMs, CancellationToken ct);
}
