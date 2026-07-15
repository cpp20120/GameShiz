// ─────────────────────────────────────────────────────────────────────────────
// PokerService — application service for /poker.
//
// Port of src/CasinoShiz.Core/Services/Poker/Application/PokerService.cs.
// Public compatibility contract. Mutation implementations run through the
// framework atomic executor; reads remain on the Dapper projections.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Concurrent;
using static Games.Poker.Domain.Rules.PokerResultHelpers;

namespace Games.Poker.Application.Services;

public interface IPokerService
{
    Task<(TableSnapshot? Snapshot, PokerSeat? MySeat)> FindMyTableAsync(
        long userId, long currentChatId, CancellationToken ct);
    Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, CancellationToken ct);
    Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct);
    Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, string operationId, CancellationToken ct) =>
        CreateTableAsync(userId, displayName, chatId, ct);
    Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, CancellationToken ct);
    Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, int sourceMessageId, CancellationToken ct);
    Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, string operationId, CancellationToken ct) =>
        JoinTableAsync(userId, displayName, chatId, code, ct);
    Task<StartResult> StartHandAsync(long userId, long currentChatId, CancellationToken ct);
    Task<StartResult> StartHandAsync(long userId, long currentChatId, string operationId, CancellationToken ct) =>
        StartHandAsync(userId, currentChatId, ct);
    Task<ActionResult> ApplyPlayerActionAsync(
        long userId, long currentChatId, string verb, int amount, CancellationToken ct);
    Task<ActionResult> ApplyPlayerActionAsync(
        long userId, long currentChatId, string verb, int amount, string operationId, CancellationToken ct) =>
        ApplyPlayerActionAsync(userId, currentChatId, verb, amount, ct);
    Task<ActionResult?> RunAutoActionAsync(string inviteCode, CancellationToken ct);
    Task<LeaveResult> LeaveTableAsync(long userId, long currentChatId, CancellationToken ct);
    Task<LeaveResult> LeaveTableAsync(long userId, long currentChatId, string operationId, CancellationToken ct) =>
        LeaveTableAsync(userId, currentChatId, ct);
    Task SetTableStateMessageIdAsync(string inviteCode, int messageId, CancellationToken ct);
    Task<IReadOnlyList<string>> ListStuckCodesAsync(long cutoffMs, CancellationToken ct);
}
