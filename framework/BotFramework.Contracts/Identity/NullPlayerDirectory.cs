namespace BotFramework.Contracts.Identity;

public sealed class NullPlayerDirectory : IPlayerDirectory
{
    public Task UpsertAsync(PlayerIdentity identity, CancellationToken ct) => Task.CompletedTask;
    public Task<PlayerIdentity?> GetAsync(long userId, CancellationToken ct) => Task.FromResult<PlayerIdentity?>(null);
    public Task<PlayerIdentity?> FindByUsernameAsync(string username, CancellationToken ct) => Task.FromResult<PlayerIdentity?>(null);
}
