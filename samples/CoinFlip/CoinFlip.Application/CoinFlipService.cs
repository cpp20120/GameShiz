using CoinFlip.Contracts;
using CoinFlip.Domain;
using BotFramework.Contracts.Tenancy;

namespace CoinFlip.Application;

public interface ICoinFlipStateStore
{
    Task<CoinFlipGameState> LoadAsync(TenantId tenantId, ScopeId scopeId, PlayerId playerId, CancellationToken ct);
    Task SaveAsync(TenantId tenantId, ScopeId scopeId, PlayerId playerId, CoinFlipGameState state, CancellationToken ct);
}

/// <summary>Atomic application boundary: load, decide, and save within one caller-owned transaction.</summary>
public sealed class CoinFlipService(ICoinFlipStateStore store)
{
    public async Task<CoinFlipReply> ExecuteAsync(CoinFlipCommand command, int entropy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = await store.LoadAsync(command.TenantId, command.ScopeId, command.PlayerId, ct).ConfigureAwait(false);
        var result = CoinFlipRules.Flip(state, entropy);
        await store.SaveAsync(command.TenantId, command.ScopeId, command.PlayerId, result.State, ct).ConfigureAwait(false);
        return new CoinFlipReply(result.Side.ToString(), result.State.Flips, result.State.Heads, result.State.Tails);
    }
}

public sealed class InMemoryCoinFlipStateStore : ICoinFlipStateStore
{
    private readonly Dictionary<(TenantId Tenant, ScopeId Scope, PlayerId Player), CoinFlipGameState> states = [];

    public Task<CoinFlipGameState> LoadAsync(TenantId tenantId, ScopeId scopeId, PlayerId playerId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (states)
            return Task.FromResult(states.GetValueOrDefault((tenantId, scopeId, playerId)) ?? CoinFlipGameState.Empty);
    }

    public Task SaveAsync(TenantId tenantId, ScopeId scopeId, PlayerId playerId, CoinFlipGameState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (states)
            states[(tenantId, scopeId, playerId)] = state;
        return Task.CompletedTask;
    }
}
