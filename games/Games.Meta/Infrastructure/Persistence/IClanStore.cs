namespace Games.Meta;

public interface IClanStore
{
    Task<ClanCreateResult> CreateAsync(MetaSeason season, long chatId, long userId, string displayName, string tag, string name, CancellationToken ct);
    Task<ClanJoinResult> JoinAsync(MetaSeason season, long chatId, long userId, string displayName, string tag, CancellationToken ct);
    Task<ClanInfo?> GetUserClanAsync(MetaSeason season, long chatId, long userId, CancellationToken ct);
    Task<ClanInfo?> GetClanByTagAsync(MetaSeason season, long chatId, string tag, CancellationToken ct);
    Task<IReadOnlyList<ClanMemberInfo>> GetMembersAsync(long clanId, CancellationToken ct);
    Task<IReadOnlyList<ClanLeaderboardEntry>> GetTopAsync(MetaSeason season, long chatId, int limit, CancellationToken ct);
    Task ApplyGameCompletedAsync(MetaSeason season, GameCompletedMetaEvent ev, long xpDelta, CancellationToken ct);
}
