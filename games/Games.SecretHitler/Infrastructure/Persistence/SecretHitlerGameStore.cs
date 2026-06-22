using BotFramework.Host;
using Dapper;

namespace Games.SecretHitler;

public sealed class SecretHitlerGameStore(INpgsqlConnectionFactory connections) : ISecretHitlerGameStore
{
    private const string SelectColumns =
        "invite_code AS InviteCode, host_user_id AS HostUserId, chat_id AS ChatId, " +
        "status AS Status, phase AS Phase, liberal_policies AS LiberalPolicies, " +
        "fascist_policies AS FascistPolicies, election_tracker AS ElectionTracker, " +
        "current_president_position AS CurrentPresidentPosition, " +
        "nominated_chancellor_position AS NominatedChancellorPosition, " +
        "last_elected_president_position AS LastElectedPresidentPosition, " +
        "last_elected_chancellor_position AS LastElectedChancellorPosition, " +
        "deck_state AS DeckState, discard_state AS DiscardState, " +
        "president_draw AS PresidentDraw, chancellor_received AS ChancellorReceived, " +
        "winner AS Winner, win_reason AS WinReason, " +
        "buy_in AS BuyIn, pot AS Pot, state_message_id AS StateMessageId, " +
        "created_at AS CreatedAt, last_action_at AS LastActionAt";

    public async Task<SecretHitlerGame?> FindAsync(string inviteCode, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<GameRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM secret_hitler_games WHERE invite_code = @inviteCode",
            new { inviteCode },
            cancellationToken: ct));
        return row?.ToEntity();
    }

    public async Task<SecretHitlerGame?> FindOpenByChatAsync(long chatId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<GameRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM secret_hitler_games WHERE chat_id = @chatId AND (status = @lobby OR status = @active) LIMIT 1",
            new { chatId, lobby = (int)ShStatus.Lobby, active = (int)ShStatus.Active },
            cancellationToken: ct));
        return row?.ToEntity();
    }

    public async Task<bool> CodeExistsAsync(string inviteCode, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM secret_hitler_games WHERE invite_code = @inviteCode)",
            new { inviteCode },
            cancellationToken: ct));
    }

    public async Task InsertAsync(SecretHitlerGame g, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO secret_hitler_games
                (invite_code, host_user_id, chat_id, status, phase,
                 liberal_policies, fascist_policies, election_tracker,
                 current_president_position, nominated_chancellor_position,
                 last_elected_president_position, last_elected_chancellor_position,
                 deck_state, discard_state, president_draw, chancellor_received,
                 winner, win_reason, buy_in, pot, state_message_id, created_at, last_action_at)
            VALUES
                (@InviteCode, @HostUserId, @ChatId, @Status, @Phase,
                 @LiberalPolicies, @FascistPolicies, @ElectionTracker,
                 @CurrentPresidentPosition, @NominatedChancellorPosition,
                 @LastElectedPresidentPosition, @LastElectedChancellorPosition,
                 @DeckState, @DiscardState, @PresidentDraw, @ChancellorReceived,
                 @Winner, @WinReason, @BuyIn, @Pot, @StateMessageId, @CreatedAt, @LastActionAt)
            """,
            GameRow.From(g),
            cancellationToken: ct));
    }

    public async Task UpdateAsync(SecretHitlerGame g, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE secret_hitler_games SET
                host_user_id = @HostUserId,
                chat_id = @ChatId,
                status = @Status,
                phase = @Phase,
                liberal_policies = @LiberalPolicies,
                fascist_policies = @FascistPolicies,
                election_tracker = @ElectionTracker,
                current_president_position = @CurrentPresidentPosition,
                nominated_chancellor_position = @NominatedChancellorPosition,
                last_elected_president_position = @LastElectedPresidentPosition,
                last_elected_chancellor_position = @LastElectedChancellorPosition,
                deck_state = @DeckState,
                discard_state = @DiscardState,
                president_draw = @PresidentDraw,
                chancellor_received = @ChancellorReceived,
                winner = @Winner,
                win_reason = @WinReason,
                buy_in = @BuyIn,
                pot = @Pot,
                state_message_id = @StateMessageId,
                last_action_at = @LastActionAt
            WHERE invite_code = @InviteCode
            """,
            GameRow.From(g),
            cancellationToken: ct));
    }

    public async Task UpsertStateMessageAsync(string inviteCode, int messageId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE secret_hitler_games SET state_message_id = @messageId WHERE invite_code = @inviteCode",
            new { inviteCode, messageId },
            cancellationToken: ct));
    }

    private sealed record GameRow(
        string InviteCode, long HostUserId, long ChatId, int Status, int Phase,
        int LiberalPolicies, int FascistPolicies, int ElectionTracker,
        int CurrentPresidentPosition, int NominatedChancellorPosition,
        int LastElectedPresidentPosition, int LastElectedChancellorPosition,
        string DeckState, string DiscardState, string PresidentDraw, string ChancellorReceived,
        int Winner, int WinReason, int BuyIn, int Pot, int? StateMessageId, long CreatedAt, long LastActionAt)
    {
        public static GameRow From(SecretHitlerGame g) => new(
            g.InviteCode, g.HostUserId, g.ChatId, (int)g.Status, (int)g.Phase,
            g.LiberalPolicies, g.FascistPolicies, g.ElectionTracker,
            g.CurrentPresidentPosition, g.NominatedChancellorPosition,
            g.LastElectedPresidentPosition, g.LastElectedChancellorPosition,
            g.DeckState, g.DiscardState, g.PresidentDraw, g.ChancellorReceived,
            (int)g.Winner, (int)g.WinReason, g.BuyIn, g.Pot, g.StateMessageId, g.CreatedAt, g.LastActionAt);

        public SecretHitlerGame ToEntity() => new()
        {
            InviteCode = InviteCode,
            HostUserId = HostUserId,
            ChatId = ChatId,
            Status = (ShStatus)Status,
            Phase = (ShPhase)Phase,
            LiberalPolicies = LiberalPolicies,
            FascistPolicies = FascistPolicies,
            ElectionTracker = ElectionTracker,
            CurrentPresidentPosition = CurrentPresidentPosition,
            NominatedChancellorPosition = NominatedChancellorPosition,
            LastElectedPresidentPosition = LastElectedPresidentPosition,
            LastElectedChancellorPosition = LastElectedChancellorPosition,
            DeckState = DeckState,
            DiscardState = DiscardState,
            PresidentDraw = PresidentDraw,
            ChancellorReceived = ChancellorReceived,
            Winner = (ShWinner)Winner,
            WinReason = (ShWinReason)WinReason,
            BuyIn = BuyIn,
            Pot = Pot,
            StateMessageId = StateMessageId,
            CreatedAt = CreatedAt,
            LastActionAt = LastActionAt,
        };
    }
}
