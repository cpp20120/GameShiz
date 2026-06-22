using BotFramework.Host;
using Dapper;

namespace Games.SecretHitler;

public sealed class SecretHitlerPlayerStore(INpgsqlConnectionFactory connections) : ISecretHitlerPlayerStore
{
    private const string SelectColumns =
        "invite_code AS InviteCode, position AS Position, user_id AS UserId, " +
        "display_name AS DisplayName, chat_id AS ChatId, role AS Role, " +
        "is_alive AS IsAlive, last_vote AS LastVote, " +
        "state_message_id AS StateMessageId, joined_at AS JoinedAt";

    public async Task<SecretHitlerPlayer?> FindByUserAsync(long userId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<PlayerRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM secret_hitler_players WHERE user_id = @userId LIMIT 1",
            new { userId },
            cancellationToken: ct));
        return row?.ToEntity();
    }

    public async Task<List<SecretHitlerPlayer>> ListByGameAsync(string inviteCode, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<PlayerRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM secret_hitler_players WHERE invite_code = @inviteCode",
            new { inviteCode },
            cancellationToken: ct));
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<bool> AnyForUserAsync(long userId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM secret_hitler_players WHERE user_id = @userId)",
            new { userId },
            cancellationToken: ct));
    }

    public async Task<int> CountByGameAsync(string inviteCode, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM secret_hitler_players WHERE invite_code = @inviteCode",
            new { inviteCode },
            cancellationToken: ct));
    }

    public async Task InsertAsync(SecretHitlerPlayer p, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO secret_hitler_players
                (invite_code, position, user_id, display_name, chat_id, role,
                 is_alive, last_vote, state_message_id, joined_at)
            VALUES
                (@InviteCode, @Position, @UserId, @DisplayName, @ChatId, @Role,
                 @IsAlive, @LastVote, @StateMessageId, @JoinedAt)
            """,
            PlayerRow.From(p),
            cancellationToken: ct));
    }

    public async Task UpdateAsync(SecretHitlerPlayer p, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE secret_hitler_players SET
                user_id = @UserId,
                display_name = @DisplayName,
                chat_id = @ChatId,
                role = @Role,
                is_alive = @IsAlive,
                last_vote = @LastVote,
                state_message_id = @StateMessageId
            WHERE invite_code = @InviteCode AND position = @Position
            """,
            PlayerRow.From(p),
            cancellationToken: ct));
    }

    public async Task DeleteAsync(string inviteCode, int position, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM secret_hitler_players WHERE invite_code = @inviteCode AND position = @position",
            new { inviteCode, position },
            cancellationToken: ct));
    }

    public async Task UpsertStateMessageAsync(long userId, int messageId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE secret_hitler_players SET state_message_id = @messageId WHERE user_id = @userId",
            new { userId, messageId },
            cancellationToken: ct));
    }

    private sealed record PlayerRow(
        string InviteCode, int Position, long UserId, string DisplayName, long ChatId,
        int Role, bool IsAlive, int LastVote, int? StateMessageId, long JoinedAt)
    {
        public static PlayerRow From(SecretHitlerPlayer p) => new(
            p.InviteCode, p.Position, p.UserId, p.DisplayName, p.ChatId,
            (int)p.Role, p.IsAlive, (int)p.LastVote, p.StateMessageId, p.JoinedAt);

        public SecretHitlerPlayer ToEntity() => new()
        {
            InviteCode = InviteCode,
            Position = Position,
            UserId = UserId,
            DisplayName = DisplayName,
            ChatId = ChatId,
            Role = (ShRole)Role,
            IsAlive = IsAlive,
            LastVote = (ShVote)LastVote,
            StateMessageId = StateMessageId,
            JoinedAt = JoinedAt,
        };
    }
}
