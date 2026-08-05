using BotFramework.Text;
using Microsoft.Extensions.DependencyInjection;
using TextRules.Application.Analysis;
using TextRules.Application.Compilation;
using TextRules.Application.Matching;
using TextRules.Application.Sources;
using TextRules.Domain.Matches;
using TextRules.Domain.Rules;
using TextRules.Infrastructure.Composition;
using TextRules.Infrastructure.Snapshots;
using TextRules.Infrastructure.Sources;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class TextRulesTests
{
    [Fact]
    public void TokenRuleMatchesCompleteNormalizedTokenAndMapsOriginalSpan()
    {
        var result = Match(
            "CASINO casinorama",
            Rule("token", "casino", TextRuleKind.Token, RuleDisposition.Deny));

        var match = Assert.Single(result.Matches);
        Assert.Equal("token", match.RuleId.Value);
        Assert.Equal(RuleMatchKind.Token, match.MatchKind);
        Assert.Equal(new TextSpan(0, 6), match.CanonicalSpan);
        Assert.Equal(new TextSpan(0, 6), match.OriginalSpan);
        Assert.Single(result.EffectiveMatches);
    }

    [Fact]
    public void TokenRuleCanOptIntoSubstringMatching()
    {
        var result = Match(
            "casinorama",
            Rule(
                "partial",
                "casino",
                TextRuleKind.Token,
                RuleDisposition.Observe,
                options: new TextRuleOptions { MatchWholeToken = false }));

        var match = Assert.Single(result.Matches);
        Assert.Equal(new TextSpan(0, 6), match.CanonicalSpan);
        Assert.Equal(RuleDisposition.Observe, match.Disposition);
    }

    [Fact]
    public void PhraseRuleUsesTokenBoundariesInsteadOfSubstringSearch()
    {
        var result = Match(
            "buy, account buyer account",
            Rule("phrase", "buy account", TextRuleKind.Phrase, RuleDisposition.Deny));

        var match = Assert.Single(result.Matches);
        Assert.Equal(RuleMatchKind.Phrase, match.MatchKind);
        Assert.Equal(new TextSpan(0, 12), match.CanonicalSpan);
        Assert.Equal("buy, account", match.OriginalSpan.Slice("buy, account buyer account"));
    }

    [Fact]
    public void RegexRuleIsCompiledAndMapsCanonicalSpanToOriginalText()
    {
        var result = Match(
            "Contact Example.COM today",
            Rule("email-host", @"example\.com", TextRuleKind.Regex, RuleDisposition.Observe));

        var match = Assert.Single(result.Matches);
        Assert.Equal(RuleMatchKind.Regex, match.MatchKind);
        Assert.Equal("Example.COM", match.OriginalSpan.Slice("Contact Example.COM today"));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void DisabledRulesRemainInDefinitionsButAreNotIndexed()
    {
        var compiler = CreateCompiler();
        var snapshot = compiler.Compile(RuleSetOf(
            Rule("disabled", "casino", TextRuleKind.Token, RuleDisposition.Deny) with { Enabled = false },
            Rule("enabled", "house", TextRuleKind.Token, RuleDisposition.Deny)));

        Assert.Equal(2, snapshot.RuleDefinitions.Length);
        Assert.DoesNotContain("casino", snapshot.TokenRules.Keys, StringComparer.Ordinal);
        Assert.Contains("house", snapshot.TokenRules.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void CompilerIsDeterministicForShuffledRuleInput()
    {
        var rules = new[]
        {
            Rule("z", "zulu", TextRuleKind.Token, RuleDisposition.Deny),
            Rule("a", "alpha", TextRuleKind.Token, RuleDisposition.Allow),
            Rule("m", "middle", TextRuleKind.Token, RuleDisposition.Observe),
        };
        var compiler = CreateCompiler();
        var first = compiler.Compile(RuleSetOf(rules));
        var second = compiler.Compile(RuleSetOf(rules.Reverse().ToArray()));

        Assert.Equal(
            first.RuleDefinitions.Select(rule => rule.Id.Value).ToArray(),
            second.RuleDefinitions.Select(rule => rule.Id.Value).ToArray(),
            StringComparer.Ordinal);
        Assert.Equal(
            first.TokenRules["alpha"].Select(rule => rule.RuleId.Value).ToArray(),
            second.TokenRules["alpha"].Select(rule => rule.RuleId.Value).ToArray(),
            StringComparer.Ordinal);
    }

    [Fact]
    public void ValidationRejectsDuplicateIdsAndInvalidVersions()
    {
        var errors = TextRuleValidator.Validate(new RuleSet
        {
            Version = 0,
            Rules =
            [
                Rule("same", "one", TextRuleKind.Token, RuleDisposition.Deny),
                Rule("same", "two", TextRuleKind.Token, RuleDisposition.Deny),
            ],
        });

        Assert.Contains(errors, error => string.Equals(error.Code, "version", StringComparison.Ordinal));
        Assert.Contains(errors, error => string.Equals(error.Code, "duplicate_id", StringComparison.Ordinal));
    }

    [Fact]
    public void CompilerRejectsInvalidTokenPhraseRegexAndOptions()
    {
        var compiler = CreateCompiler();

        var tokenError = Assert.Throws<TextRuleValidationException>(() => compiler.Compile(RuleSetOf(
            Rule("token", "two words", TextRuleKind.Token, RuleDisposition.Deny))));
        Assert.Contains(tokenError.Errors, error => string.Equals(error.Code, "token_pattern", StringComparison.Ordinal));

        var regexError = Assert.Throws<TextRuleValidationException>(() => compiler.Compile(RuleSetOf(
            Rule("regex", "[", TextRuleKind.Regex, RuleDisposition.Deny))));
        Assert.Contains(regexError.Errors, error => string.Equals(error.Code, "regex_pattern", StringComparison.Ordinal));

        var optionsError = Assert.Throws<TextRuleValidationException>(() => compiler.Compile(RuleSetOf(
            Rule(
                "phrase",
                "buy account",
                TextRuleKind.Phrase,
                RuleDisposition.Deny,
                options: new TextRuleOptions { MatchWholeToken = false }))));
        Assert.Contains(optionsError.Errors, error => string.Equals(error.Code, "options", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolverUsesScopePriorityDispositionLengthAndRuleIdDeterministically()
    {
        var sameSpan = new TextSpan(0, 6);
        var globalAllow = CreateMatch("allow", RuleDisposition.Allow, RuleScope.Global, 100, sameSpan);
        var globalDeny = CreateMatch("deny", RuleDisposition.Deny, RuleScope.Global, 100, sameSpan);
        var highPriorityDeny = CreateMatch("high", RuleDisposition.Deny, RuleScope.Global, 200, sameSpan);

        var allowWins = RuleMatchResolver.Resolve([globalDeny, globalAllow]);
        Assert.Equal("allow", Assert.Single(allowWins.EffectiveMatches).RuleId.Value);

        var priorityWins = RuleMatchResolver.Resolve([globalAllow, highPriorityDeny]);
        Assert.Equal("high", Assert.Single(priorityWins.EffectiveMatches).RuleId.Value);

        var chatDeny = CreateMatch("chat", RuleDisposition.Deny, RuleScope.ForChat("tenant", "chat"), 1, sameSpan);
        var tenantAllow = CreateMatch("tenant", RuleDisposition.Allow, RuleScope.ForTenant("tenant"), 1000, sameSpan);
        var scopeWins = RuleMatchResolver.Resolve([tenantAllow, chatDeny]);
        Assert.Equal("chat", Assert.Single(scopeWins.EffectiveMatches).RuleId.Value);

        var observe = CreateMatch("observe", RuleDisposition.Observe, RuleScope.Global, 1, sameSpan);
        var withObserve = RuleMatchResolver.Resolve([globalDeny, observe]);
        Assert.Equal(2, withObserve.EffectiveMatches.Count);
    }

    [Fact]
    public void ResolverKeepsUnrelatedDecisionsAndUsesPatternLengthAndRuleIdTies()
    {
        var longDeny = CreateMatch("long", RuleDisposition.Deny, RuleScope.Global, 100, new TextSpan(0, 6));
        var shortDeny = CreateMatch("short", RuleDisposition.Deny, RuleScope.Global, 100, new TextSpan(2, 2));
        var unrelated = CreateMatch("other", RuleDisposition.Deny, RuleScope.Global, 100, new TextSpan(10, 2));

        var result = RuleMatchResolver.Resolve([shortDeny, unrelated, longDeny]);

        Assert.Equal(
            ["long", "other"],
            result.EffectiveMatches.Select(match => match.RuleId.Value).ToArray(),
            StringComparer.Ordinal);

        var idA = CreateMatch("a", RuleDisposition.Deny, RuleScope.Global, 100, new TextSpan(0, 4));
        var idB = CreateMatch("b", RuleDisposition.Deny, RuleScope.Global, 100, new TextSpan(0, 4));
        Assert.Equal("a", Assert.Single(RuleMatchResolver.Resolve([idB, idA]).EffectiveMatches).RuleId.Value);
    }

    [Fact]
    public async Task InMemorySourceCombinesGlobalTenantAndChatRules()
    {
        var source = new InMemoryTextRuleSource();
        source.Replace(RuleScope.Global, 10, [Rule("global", "global", TextRuleKind.Token, RuleDisposition.Observe)]);
        source.Replace(RuleScope.ForTenant("tenant"), 20, [Rule("tenant", "tenant", TextRuleKind.Token, RuleDisposition.Observe)]);
        source.Replace(RuleScope.ForChat("tenant", "chat"), 30, [Rule("chat", "chat", TextRuleKind.Token, RuleDisposition.Observe)]);

        var result = await source.LoadAsync(RuleScope.ForChat("tenant", "chat"));

        Assert.Equal(30, result.Version);
        Assert.Equal(
            ["global", "tenant", "chat"],
            result.Rules.Select(rule => rule.Id.Value).ToArray(),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task SnapshotProviderBuildsOneSnapshotPerScopeAndReplacesAtomically()
    {
        var source = new InMemoryTextRuleSource();
        source.Replace(RuleScope.Global, 1, [Rule("one", "one", TextRuleKind.Token, RuleDisposition.Deny)]);
        var countingSource = new CountingSource(source);
        var countingCompiler = new CountingCompiler(CreateCompiler());
        var provider = new CachedTextRuleSnapshotProvider(countingSource, countingCompiler);

        var snapshots = await Task.WhenAll(
            Enumerable.Range(0, 16)
                .Select(_ => provider.GetAsync(RuleScope.Global).AsTask()));

        Assert.Equal(1, countingSource.LoadCount);
        Assert.Equal(1, countingCompiler.CompileCount);
        Assert.All(snapshots, snapshot => Assert.Same(snapshots[0], snapshot));

        source.Replace(RuleScope.Global, 2, [Rule("two", "two", TextRuleKind.Token, RuleDisposition.Deny)]);
        await provider.InvalidateAsync(RuleScope.Global);
        var replaced = await provider.GetAsync(RuleScope.Global);

        Assert.Equal(2, replaced.Version);
        Assert.NotSame(snapshots[0], replaced);
    }

    [Fact]
    public async Task FailedRebuildLeavesPreviousSnapshotUsableAndRetryable()
    {
        var source = new ToggleSource(RuleSetOf(Rule("one", "one", TextRuleKind.Token, RuleDisposition.Deny)));
        var provider = new CachedTextRuleSnapshotProvider(source, CreateCompiler());
        var previous = await provider.GetAsync(RuleScope.Global);

        source.Fail = true;
        await provider.InvalidateAsync(RuleScope.Global);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAsync(RuleScope.Global).AsTask());
        Assert.Equal(1, previous.Version);

        source.Fail = false;
        source.Current = RuleSetOf(2, Rule("two", "two", TextRuleKind.Token, RuleDisposition.Deny));
        var current = await provider.GetAsync(RuleScope.Global);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task GlobalInvalidationRebuildsDependentTenantCache()
    {
        var source = new InMemoryTextRuleSource();
        source.Replace(RuleScope.Global, 1, [Rule("one", "one", TextRuleKind.Token, RuleDisposition.Deny)]);
        source.Replace(RuleScope.ForTenant("tenant"), 1, []);
        var provider = new CachedTextRuleSnapshotProvider(source, CreateCompiler());
        var old = await provider.GetAsync(RuleScope.ForChat("tenant", "chat"));

        source.Replace(RuleScope.Global, 2, [Rule("two", "two", TextRuleKind.Token, RuleDisposition.Deny)]);
        await provider.InvalidateAsync(RuleScope.Global);
        var current = await provider.GetAsync(RuleScope.ForChat("tenant", "chat"));

        Assert.Equal(1, old.Version);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task AnalyzerFactsFlowIntoConsumerPolicyAndEffectWithoutTelegram()
    {
        var services = new ServiceCollection()
            .AddTextProcessing()
            .AddTextRules()
            .AddTextPolicy<DenyReplyPolicy>()
            .AddSingleton<EffectSink>()
            .AddTextEffectHandler<RecordingReplyHandler>();
        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<InMemoryTextRuleSource>();
        source.Replace(
            RuleScope.ForTenant("tenant"),
            1,
            [Rule("deny-casino", "casino", TextRuleKind.Token, RuleDisposition.Deny)]);

        await using var scope = provider.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<ITextProcessingPipeline>();
        var result = await pipeline.ProcessAsync(
            "CASINO",
            new TextProcessingContext
            {
                Properties = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [TextProcessingKeys.TenantId] = "tenant",
                },
            });

        var analysis = Assert.Single(
            result.Analysis.Results,
            item => string.Equals(item.AnalyzerId, "text-rules", StringComparison.Ordinal));
        var fact = Assert.Single(TextRuleFacts.GetEffectiveMatches(analysis));
        Assert.Equal("deny-casino", fact.Match.RuleId.Value);
        Assert.Equal("reply", Assert.Single(result.Decision.Effects).Kind);
        var sink = scope.ServiceProvider.GetRequiredService<EffectSink>();
        Assert.Equal("matched", Assert.Single(sink.Replies));
    }

    private static RuleMatchResult Match(string text, params TextRule[] rules)
    {
        var normalizer = new DefaultTextNormalizer();
        var compiler = new TextRuleCompiler(normalizer);
        var matcher = new TextRuleMatcher();
        var normalized = normalizer.Normalize(text);
        return matcher.Match(normalized, compiler.Compile(RuleSetOf(rules)));
    }

    private static TextRuleCompiler CreateCompiler() => new(new DefaultTextNormalizer());

    private static RuleSet RuleSetOf(params TextRule[] rules) => new()
    {
        Version = 1,
        Rules = rules,
    };

    private static RuleSet RuleSetOf(long version, params TextRule[] rules) => new()
    {
        Version = version,
        Rules = rules,
    };

    private static TextRule Rule(
        string id,
        string pattern,
        TextRuleKind kind,
        RuleDisposition disposition,
        RuleScope? scope = null,
        int priority = 100,
        TextRuleOptions? options = null) => new()
        {
            Id = new TextRuleId(id),
            Pattern = pattern,
            Kind = kind,
            Disposition = disposition,
            Scope = scope ?? RuleScope.Global,
            Priority = priority,
            Options = options ?? new TextRuleOptions(),
        };

    private static RuleMatch CreateMatch(
        string id,
        RuleDisposition disposition,
        RuleScope scope,
        int priority,
        TextSpan span) => new()
        {
            RuleId = new TextRuleId(id),
            Disposition = disposition,
            Scope = scope,
            Priority = priority,
            PatternLength = span.Length,
            CanonicalSpan = span,
            OriginalSpan = span,
            MatchKind = RuleMatchKind.Token,
            Confidence = 1d,
        };

    private sealed class CountingSource(ITextRuleSource inner) : ITextRuleSource
    {
        private int _loadCount;

        public int LoadCount => Volatile.Read(ref _loadCount);

        public async ValueTask<RuleSet> LoadAsync(
            RuleScope scope,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _loadCount);
            await Task.Delay(20, cancellationToken);
            return await inner.LoadAsync(scope, cancellationToken);
        }
    }

    private sealed class CountingCompiler(ITextRuleCompiler inner) : ITextRuleCompiler
    {
        private int _compileCount;

        public int CompileCount => Volatile.Read(ref _compileCount);

        public CompiledRuleSnapshot Compile(RuleSet ruleSet)
        {
            Interlocked.Increment(ref _compileCount);
            return inner.Compile(ruleSet);
        }
    }

    private sealed class ToggleSource(RuleSet current) : ITextRuleSource
    {
        public RuleSet Current { get; set; } = current;
        public bool Fail { get; set; }

        public ValueTask<RuleSet> LoadAsync(
            RuleScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail)
                throw new InvalidOperationException("source failed");
            return ValueTask.FromResult(Current);
        }
    }

    private sealed class DenyReplyPolicy : ITextPolicy
    {
        public string Name => "deny-reply";
        public int Order => 1;

        public ValueTask<PolicyDecision> EvaluateAsync(
            TextPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            var analysis = context.Analysis.Results.Single(
                result => string.Equals(result.AnalyzerId, "text-rules", StringComparison.Ordinal));
            var shouldReply = TextRuleFacts.GetEffectiveMatches(analysis)
                .Any(fact => fact.Match.Disposition == RuleDisposition.Deny);
            return ValueTask.FromResult(new PolicyDecision
            {
                PolicyId = Name,
                Effects = shouldReply ? [new ReplyEffect("matched")] : [],
            });
        }
    }

    private sealed class EffectSink
    {
        public List<string> Replies { get; } = [];
    }

    private sealed class RecordingReplyHandler(EffectSink sink) : MessageEffectHandler<ReplyEffect>
    {
        protected override ValueTask ExecuteAsync(
            ReplyEffect effect,
            TextProcessingContext context,
            CancellationToken cancellationToken)
        {
            sink.Replies.Add(effect.Text);
            return ValueTask.CompletedTask;
        }
    }
}
