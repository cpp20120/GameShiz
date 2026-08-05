using System.Collections.Concurrent;
using TextRules.Application.Compilation;
using TextRules.Application.Sources;
using TextRules.Domain.Rules;

namespace TextRules.Infrastructure.Snapshots;

/// <summary>
/// Scope-keyed snapshot cache with per-scope build coordination and atomic publication.
/// </summary>
public sealed class CachedTextRuleSnapshotProvider(
    ITextRuleSource source,
    ITextRuleCompiler compiler) : ITextRuleSnapshotProvider
{
    private readonly ITextRuleSource _source =
        source ?? throw new ArgumentNullException(nameof(source));
    private readonly ITextRuleCompiler _compiler =
        compiler ?? throw new ArgumentNullException(nameof(compiler));
    private readonly ConcurrentDictionary<RuleScope, ScopeEntry> _entries = new();

    public async ValueTask<CompiledRuleSnapshot> GetAsync(
        RuleScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = _entries.GetOrAdd(scope, static _ => new ScopeEntry());
        Task<CompiledRuleSnapshot> build;
        lock (entry)
        {
            if (!entry.Invalidated && entry.Current is not null)
                return entry.Current;

            build = entry.BuildTask ??= BuildLatestAsync(scope, entry, cancellationToken);
        }

        try
        {
            return await build;
        }
        finally
        {
            lock (entry)
            {
                if (ReferenceEquals(entry.BuildTask, build))
                    entry.BuildTask = null;
            }
        }
    }

    public ValueTask InvalidateAsync(
        RuleScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var pair in _entries)
        {
            if (!Affects(scope, pair.Key))
                continue;

            lock (pair.Value)
            {
                pair.Value.Invalidated = true;
                pair.Value.Generation++;
            }
        }

        return ValueTask.CompletedTask;
    }

    private async Task<CompiledRuleSnapshot> BuildLatestAsync(
        RuleScope scope,
        ScopeEntry entry,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long generation;
            lock (entry)
                generation = entry.Generation;

            var ruleSet = await _source.LoadAsync(scope, cancellationToken);
            var snapshot = _compiler.Compile(ruleSet);

            lock (entry)
            {
                if (generation != entry.Generation)
                    continue;

                entry.Current = snapshot;
                entry.Invalidated = false;
                return snapshot;
            }
        }
    }

    private static bool Affects(RuleScope invalidated, RuleScope cached)
    {
        if (invalidated.IsGlobal)
            return true;
        if (invalidated.IsChat)
            return invalidated.Equals(cached);

        return cached.TenantId is not null
            && string.Equals(cached.TenantId, invalidated.TenantId, StringComparison.Ordinal);
    }

    private sealed class ScopeEntry
    {
        public CompiledRuleSnapshot? Current { get; set; }
        public Task<CompiledRuleSnapshot>? BuildTask { get; set; }
        public long Generation { get; set; }
        public bool Invalidated { get; set; } = true;
    }
}
