namespace Games.Meta;

public interface IClanService
{
    Task<ClanCreateResult> CreateAsync(long chatId, long userId, string displayName, string tag, string name, CancellationToken ct);
    Task<ClanJoinResult> JoinAsync(long chatId, long userId, string displayName, string tag, CancellationToken ct);
    Task<ClanInfo?> GetUserClanAsync(long chatId, long userId, CancellationToken ct);
    Task<ClanInfo?> GetClanByTagAsync(long chatId, string tag, CancellationToken ct);
    Task<IReadOnlyList<ClanMemberInfo>> GetMembersAsync(long clanId, CancellationToken ct);
    Task<IReadOnlyList<ClanLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct);
    Task ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct);
}
