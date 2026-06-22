// ─────────────────────────────────────────────────────────────────────────────
// PokerStores — Dapper-backed access for poker_tables + poker_seats. The
// domain mutates PokerTable/PokerSeat instances in place, so updates
// overwrite every field from the in-memory object.
//
// Concurrency: PokerService serializes all mutating operations behind a
// process-local SemaphoreSlim (same shape as the monolith). Distributed hosts
// would require a proper per-table lock or optimistic concurrency — out of
// scope for this port.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Dapper;

namespace Games.Poker.Infrastructure.Persistence;

public sealed class PokerTableStore(INpgsqlConnectionFactory connections) : IPokerTableStore
{
    private const string SelectColumns =
        "invite_code AS InviteCode, chat_id AS ChatId, host_user_id AS HostUserId, status AS Status, phase AS Phase, " +
        "small_blind AS SmallBlind, big_blind AS BigBlind, pot AS Pot, community_cards AS CommunityCards, " +
        "deck_state AS DeckState, button_seat AS ButtonSeat, current_seat AS CurrentSeat, current_bet AS CurrentBet, " +
        "min_raise AS MinRaise, state_message_id AS StateMessageId, last_action_at AS LastActionAt, created_at AS CreatedAt";

    public async Task<PokerTable?> FindAsync(string inviteCode, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<TableRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM poker_tables WHERE invite_code = @inviteCode",
            new { inviteCode },
            cancellationToken: ct));
        return row == null ? null : row.ToEntity();
    }

    public async Task<PokerTable?> FindOpenByChatAsync(long chatId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<TableRow>(new CommandDefinition(
            $"""
            SELECT {SelectColumns}
            FROM poker_tables
            WHERE chat_id = @chatId AND status <> @closed
            ORDER BY created_at DESC
            LIMIT 1
            """,
            new { chatId, closed = (int)PokerTableStatus.Closed },
            cancellationToken: ct));
        return row?.ToEntity();
    }

    public async Task<bool> CodeExistsAsync(string inviteCode, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM poker_tables WHERE invite_code = @inviteCode)",
            new { inviteCode },
            cancellationToken: ct));
    }

    public async Task InsertAsync(PokerTable t, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO poker_tables
                (invite_code, chat_id, host_user_id, status, phase, small_blind, big_blind, pot,
                 community_cards, deck_state, button_seat, current_seat, current_bet,
                 min_raise, state_message_id, last_action_at, created_at)
            VALUES
                (@InviteCode, @ChatId, @HostUserId, @Status, @Phase, @SmallBlind, @BigBlind, @Pot,
                 @CommunityCards, @DeckState, @ButtonSeat, @CurrentSeat, @CurrentBet,
                 @MinRaise, @StateMessageId, @LastActionAt, @CreatedAt)
            """,
            new TableRow(
                t.InviteCode, t.ChatId, t.HostUserId, (int)t.Status, (int)t.Phase,
                t.SmallBlind, t.BigBlind, t.Pot, t.CommunityCards, t.DeckState,
                t.ButtonSeat, t.CurrentSeat, t.CurrentBet, t.MinRaise,
                t.StateMessageId, t.LastActionAt, t.CreatedAt),
            cancellationToken: ct));
    }

    public async Task UpdateAsync(PokerTable t, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE poker_tables SET
                chat_id = @ChatId,
                host_user_id = @HostUserId,
                status = @Status,
                phase = @Phase,
                small_blind = @SmallBlind,
                big_blind = @BigBlind,
                pot = @Pot,
                community_cards = @CommunityCards,
                deck_state = @DeckState,
                button_seat = @ButtonSeat,
                current_seat = @CurrentSeat,
                current_bet = @CurrentBet,
                min_raise = @MinRaise,
                state_message_id = @StateMessageId,
                last_action_at = @LastActionAt
            WHERE invite_code = @InviteCode
            """,
            new TableRow(
                t.InviteCode, t.ChatId, t.HostUserId, (int)t.Status, (int)t.Phase,
                t.SmallBlind, t.BigBlind, t.Pot, t.CommunityCards, t.DeckState,
                t.ButtonSeat, t.CurrentSeat, t.CurrentBet, t.MinRaise,
                t.StateMessageId, t.LastActionAt, t.CreatedAt),
            cancellationToken: ct));
    }

    public async Task UpsertStateMessageAsync(string inviteCode, int messageId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE poker_tables SET state_message_id = @messageId WHERE invite_code = @inviteCode",
            new { inviteCode, messageId },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<string>> ListStuckCodesAsync(long cutoffMs, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var codes = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT invite_code FROM poker_tables WHERE status = @active AND last_action_at < @cutoff",
            new { active = (int)PokerTableStatus.HandActive, cutoff = cutoffMs },
            cancellationToken: ct));
        return [.. codes];
    }

    private sealed record TableRow(
        string InviteCode, long ChatId, long HostUserId, int Status, int Phase,
        int SmallBlind, int BigBlind, int Pot, string CommunityCards, string DeckState,
        int ButtonSeat, int CurrentSeat, int CurrentBet, int MinRaise,
        int? StateMessageId, long LastActionAt, long CreatedAt)
    {
        public PokerTable ToEntity() => new()
        {
            InviteCode = InviteCode,
            ChatId = ChatId,
            HostUserId = HostUserId,
            Status = (PokerTableStatus)Status,
            Phase = (PokerPhase)Phase,
            SmallBlind = SmallBlind,
            BigBlind = BigBlind,
            Pot = Pot,
            CommunityCards = CommunityCards,
            DeckState = DeckState,
            ButtonSeat = ButtonSeat,
            CurrentSeat = CurrentSeat,
            CurrentBet = CurrentBet,
            MinRaise = MinRaise,
            StateMessageId = StateMessageId,
            LastActionAt = LastActionAt,
            CreatedAt = CreatedAt,
        };
    }
}
