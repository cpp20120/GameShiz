namespace BotFramework.Contracts.Identity;

public interface IPlayerDirectory
{
    Task UpsertAsync(PlayerIdentity identity, CancellationToken ct);
    Task<PlayerIdentity?> GetAsync(long userId, CancellationToken ct);
    Task<PlayerIdentity?> FindByUsernameAsync(string username, CancellationToken ct);
}
