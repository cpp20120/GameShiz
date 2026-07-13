using System.Globalization;
using BotFramework.Host.Execution;
using Games.Poker.Application.Execution;

namespace Games.Poker.Application.Services;

/// <summary>Compatibility facade over the atomic poker aggregate.</summary>
public sealed class PokerService(
    IPokerTableStore tables,
    IPokerSeatStore seats,
    IAtomicGameExecutor<PokerCreateCommand, PokerExecutionState, CreateResult> createExecutor,
    IAtomicGameExecutor<PokerJoinCommand, PokerExecutionState, JoinResult> joinExecutor,
    IAtomicGameExecutor<PokerStartCommand, PokerExecutionState, StartResult> startExecutor,
    IAtomicGameExecutor<PokerPlayerTurnCommand, PokerExecutionState, ActionResult> turnExecutor,
    IAtomicGameExecutor<PokerAutoTurnCommand, PokerExecutionState, ActionResult> autoExecutor,
    IAtomicGameExecutor<PokerLeaveCommand, PokerExecutionState, LeaveResult> leaveExecutor,
    IAtomicGameExecutor<PokerSetMessageCommand, PokerExecutionState, bool> messageExecutor,
    IRuntimeTuningAccessor tuning) : IPokerService
{
    private PokerOptions CurrentOptions() => tuning.GetSection<PokerOptions>(PokerOptions.SectionName);

    public async Task<(TableSnapshot? Snapshot, PokerSeat? MySeat)> FindMyTableAsync(
        long userId, long currentChatId, CancellationToken ct)
    {
        var table = await tables.FindOpenByChatAsync(currentChatId, ct).ConfigureAwait(false);
        if (table is null) return (null, null);
        var seat = await seats.FindByUserInTableAsync(userId, table.InviteCode, ct).ConfigureAwait(false);
        if (seat is null) return (null, null);
        var list = await seats.ListByTableAsync(table.InviteCode, ct).ConfigureAwait(false);
        return (new(table, list), list.First(item => item.UserId == userId));
    }

    public Task<CreateResult> CreateTableAsync(
        long userId, string displayName, long chatId, CancellationToken ct) =>
        CreateTableAsync(userId, displayName, chatId, 0, ct);

    public Task<CreateResult> CreateTableAsync(
        long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct)
    {
        var options = CurrentOptions();
        var source = sourceMessageId > 0
            ? sourceMessageId.ToString(CultureInfo.InvariantCulture)
            : Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        return createExecutor.ExecuteAsync(new(new PokerCreateCommand(
            userId, displayName, chatId, $"poker:create:{chatId}:{source}:{userId}",
            options.BuyIn, options.SmallBlind, options.BigBlind, [new(userId, chatId)])), ct);
    }

    public Task<JoinResult> JoinTableAsync(
        long userId, string displayName, long chatId, string code, CancellationToken ct) =>
        JoinTableAsync(userId, displayName, chatId, code, 0, ct);

    public async Task<JoinResult> JoinTableAsync(
        long userId, string displayName, long chatId, string code, int sourceMessageId, CancellationToken ct)
    {
        code = code.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            var groupTable = await tables.FindOpenByChatAsync(chatId, ct).ConfigureAwait(false);
            if (groupTable is null) return new(PokerError.NoTable, null, 0, CurrentOptions().MaxPlayers);
            code = groupTable.InviteCode;
        }
        var expected = await ExpectedWalletsAsync(code, ct).ConfigureAwait(false);
        expected.Add(new(userId, chatId));
        var source = sourceMessageId > 0
            ? sourceMessageId.ToString(CultureInfo.InvariantCulture)
            : Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var options = CurrentOptions();
        return await joinExecutor.ExecuteAsync(new(new PokerJoinCommand(
            code, userId, displayName, chatId, $"poker:join:{chatId}:{source}:{userId}:{code}",
            options.BuyIn, options.MaxPlayers, expected)), ct).ConfigureAwait(false);
    }

    public async Task<StartResult> StartHandAsync(long userId, long currentChatId, CancellationToken ct)
    {
        var table = await tables.FindOpenByChatAsync(currentChatId, ct).ConfigureAwait(false);
        if (table is null) return new(PokerError.NoTable, null);
        var expected = await ExpectedWalletsAsync(table.InviteCode, ct).ConfigureAwait(false);
        return await startExecutor.ExecuteAsync(new(new PokerStartCommand(
            table.InviteCode, userId, "poker player", currentChatId,
            $"poker:start:{table.InviteCode}:{table.LastActionAt}:{userId}", expected)), ct)
            .ConfigureAwait(false);
    }

    public async Task<ActionResult> ApplyPlayerActionAsync(
        long userId, long currentChatId, string verb, int amount, CancellationToken ct)
    {
        var table = await tables.FindOpenByChatAsync(currentChatId, ct).ConfigureAwait(false);
        if (table is null) return ActionFailure(PokerError.NoTable);
        var expected = await ExpectedWalletsAsync(table.InviteCode, ct).ConfigureAwait(false);
        return await turnExecutor.ExecuteAsync(new(new PokerPlayerTurnCommand(
            table.InviteCode, userId, "poker player", currentChatId,
            $"poker:turn:{table.InviteCode}:{table.LastActionAt}:{table.CurrentSeat}:{userId}:{verb}:{amount}",
            verb, amount, expected)), ct).ConfigureAwait(false);
    }

    public async Task<ActionResult?> RunAutoActionAsync(string inviteCode, CancellationToken ct)
    {
        var table = await tables.FindAsync(inviteCode, ct).ConfigureAwait(false);
        if (table is not { Status: PokerTableStatus.HandActive }) return null;
        var list = await seats.ListByTableAsync(inviteCode, ct).ConfigureAwait(false);
        var current = list.FirstOrDefault(seat => seat.Position == table.CurrentSeat);
        if (current is not { Status: PokerSeatStatus.Seated }) return null;
        var expected = list.Select(seat => new PokerWalletRef(seat.UserId, seat.ChatId)).ToList();
        var result = await autoExecutor.ExecuteAsync(new(new PokerAutoTurnCommand(
            inviteCode, current.UserId, current.DisplayName, table.ChatId,
            $"poker:auto:{inviteCode}:{table.LastActionAt}:{table.CurrentSeat}", expected)), ct)
            .ConfigureAwait(false);
        return result.Error == PokerError.NotYourTurn ? null : result;
    }

    public async Task<LeaveResult> LeaveTableAsync(long userId, long currentChatId, CancellationToken ct)
    {
        var table = await tables.FindOpenByChatAsync(currentChatId, ct).ConfigureAwait(false);
        if (table is null) return new(PokerError.NoTable, null, false);
        var list = await seats.ListByTableAsync(table.InviteCode, ct).ConfigureAwait(false);
        var leaving = list.FirstOrDefault(seat => seat.UserId == userId);
        if (leaving is null) return new(PokerError.NoTable, null, false);
        var expected = list.Select(seat => new PokerWalletRef(seat.UserId, seat.ChatId)).ToList();
        return await leaveExecutor.ExecuteAsync(new(new PokerLeaveCommand(
            table.InviteCode, userId, leaving.DisplayName, currentChatId,
            $"poker:leave:{table.InviteCode}:{userId}:{leaving.JoinedAt}", expected)), ct)
            .ConfigureAwait(false);
    }

    public async Task SetTableStateMessageIdAsync(string inviteCode, int messageId, CancellationToken ct)
    {
        var table = await tables.FindAsync(inviteCode, ct).ConfigureAwait(false);
        if (table is null) return;
        await messageExecutor.ExecuteAsync(new(new PokerSetMessageCommand(
            inviteCode, table.HostUserId, "poker", table.ChatId,
            $"poker:message:{inviteCode}:{messageId}", messageId, [])), ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> ListStuckCodesAsync(long cutoffMs, CancellationToken ct) =>
        tables.ListStuckCodesAsync(cutoffMs, ct);

    private async Task<List<PokerWalletRef>> ExpectedWalletsAsync(string code, CancellationToken ct) =>
        (await seats.ListByTableAsync(code, ct).ConfigureAwait(false))
        .Select(seat => new PokerWalletRef(seat.UserId, seat.ChatId)).ToList();

    private static ActionResult ActionFailure(PokerError error) =>
        new(error, null, HandTransition.None, null, null, null);
}
