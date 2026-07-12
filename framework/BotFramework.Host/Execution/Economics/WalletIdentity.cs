namespace BotFramework.Host.Execution;

public sealed record WalletIdentity(long UserId, long BalanceScopeId)
{
    public string LockKey => $"wallet:{BalanceScopeId}:{UserId}";
}
