# TextRules module

`TextRules` is an application module built on top of `BotFramework.Text`. The framework owns
normalization, tokenization, analysis contexts, policies, and transport-neutral effects; this
module owns rule definitions, compilation, matching, scope resolution, and immutable snapshots.
There are no Telegram dependencies in the rule engine.

## Projects

```text
modules/TextRules/
├── TextRules.Domain/          # rules, scopes, matches, validation and conflict resolution
├── TextRules.Application/     # compiler, matcher, source/provider contracts, analyzer and facts
└── TextRules.Infrastructure/  # in-memory source, cached snapshot provider and DI composition
```

Tests live in the repository test project, `tests/CasinoShiz.Tests`.

## Supported rules

The initial engine supports exactly three kinds:

- `Token` matches one normalized token. `MatchWholeToken` is enabled by default; setting it to
  `false` explicitly enables substring matching inside a single normalized token.
- `Phrase` matches an ordered sequence of normalized tokens. Punctuation between tokens is not a
  token and therefore does not prevent a phrase match.
- `Regex` matches canonical normalized text. Regexes are compiled while creating a snapshot,
  use a finite timeout, and prefer `RegexOptions.NonBacktracking` when compatible.

Rules have one of three dispositions: `Allow`, `Deny`, or `Observe`. A disposition never emits an
effect. A consumer policy decides what to do with the typed `TextRuleMatchedFact` values.

## Scope and resolution

Scopes are `global`, `tenant`, or `chat`. A chat scope must include a tenant id. The in-memory
source combines global, tenant, and chat definitions for a requested chat scope. Rule ids must be
unique in the effective rule set.

For overlapping non-observe matches, the effective winner is selected by:

1. scope specificity descending (`chat > tenant > global`);
2. priority descending;
3. disposition precedence (`Allow > Deny > Observe`);
4. canonical pattern length descending;
5. rule id ascending.

Observe matches remain available as facts. Intervals use strict overlap: touching spans do not
conflict. Raw matches and effective matches are both preserved in `RuleMatchResult`.

## Snapshots

`ITextRuleCompiler` validates definitions and creates frozen token and phrase indexes plus an
immutable compiled regex collection. `CachedTextRuleSnapshotProvider` caches snapshots per scope,
coordinates builds independently per scope, and publishes a completed snapshot atomically.

`InvalidateAsync` marks the requested scope stale. Global invalidation affects all cached scopes;
tenant invalidation affects that tenant and its chats; chat invalidation affects only that chat.
An old snapshot remains valid for callers that already hold it. A failed rebuild is not published,
and the previous snapshot remains retained for in-flight work and a later retry.

## Dependency injection

The infrastructure composition is:

```csharp
services
    .AddTextProcessing()
    .AddTextRules();
```

`AddTextRules` registers the compiler, matcher, `TextRuleAnalyzer`, in-memory source, and cached
snapshot provider. The in-memory source is empty by default and can be populated by a host:

```csharp
var source = serviceProvider.GetRequiredService<InMemoryTextRuleSource>();
source.Replace(
    RuleScope.ForTenant("tenant-1"),
    version: 1,
    rules: [new TextRule
    {
        Id = new TextRuleId("example"),
        Pattern = "casino",
        Kind = TextRuleKind.Token,
        Disposition = RuleDisposition.Observe,
        Scope = RuleScope.ForTenant("tenant-1"),
    }]);
```

An application with another source can replace the default:

```csharp
services
    .AddTextProcessing()
    .AddTextRules()
    .UseTextRuleSource<ConfiguredTextRuleSource>();
```

The source loads definitions; it never participates in message matching. The matcher receives a
normalized text and a compiled snapshot and performs CPU-only work.

## Policy integration

`TextRuleAnalyzer` resolves tenant/chat scope from `TextProcessingContext` using
`TextProcessingKeys.TenantId` and `TextProcessingKeys.ChatId`. It returns typed
`TextRuleMatchedFact` values under `TextRuleFacts.MatchesKey`, with `IsEffective` identifying
matches that survived conflict resolution. A policy can consume them without depending on
Telegram:

```csharp
public sealed class AuditPolicy : ITextPolicy
{
    public string Name => "text-rule-audit";
    public int Order => 100;

    public ValueTask<PolicyDecision> EvaluateAsync(
        TextPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        var result = context.Analysis.Results
            .Single(facts => facts.AnalyzerId == "text-rules");
        var matches = TextRuleFacts.GetEffectiveMatches(result);

        return ValueTask.FromResult(new PolicyDecision
        {
            PolicyId = Name,
            Values = new Dictionary<string, object?>
            {
                ["match_count"] = matches.Count(),
            },
        });
    }
}
```

Whether a `Deny` fact should be logged, replied to, deleted, queued, or ignored remains a
consumer policy and effect-handler decision.

## Current non-goals

The module does not provide profanity dictionaries, moderation commands, warnings, mutes, bans,
Telegram callbacks, persistence migrations, analytics, fuzzy matching, homoglyph folding,
separator obfuscation, repeated-character matching, leetspeak, transliteration, scripting, or
user-defined executable rules.
