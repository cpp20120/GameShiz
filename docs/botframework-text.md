# BotFramework.Text

`BotFramework.Text` is the platform-independent message-analysis substrate for BotFramework. It supplies reusable text primitives and an execution pipeline; it does not implement censorship, spam rules, moderation policy, persistence, or an administration UI.

## Boundary

The framework owns:

-   safe Unicode normalization;
-   tokenization and canonical-to-original span mapping;
-   analyzer and matcher contracts;
-   deterministic policy composition;
-   transport-neutral message effects;
-   effect dispatch and observability hooks;
-   immutable compiled-snapshot contracts.

Consumer modules own:

-   dictionaries and patterns;
-   chat/tenant settings;
-   persistence and cache invalidation;
-   decisions such as allow, reject, queue, warn, or delete;
-   delayed jobs and queues;
-   analytics storage and dashboards;
-   platform-specific effect handlers.

The intended dependency direction is:

```text
BotFramework.Text
        ↑
consumer analysis module
        ↑
Telegram / Discord / REST composition
```

The framework must never reference concepts such as profanity, forbidden words, spam, moderation cases, warnings, or bans.

## Processing model

```text
source text + TextProcessingContext
                │
                ▼
          ITextNormalizer
                │
                ▼
          NormalizedText
                │
                ▼
      ordered ITextAnalyzer[]
                │
                ▼
            TextAnalysis
                │
                ▼
   CompositeDecisionEngine
                │
        ordered ITextPolicy[]
                │
                ▼
             Decision
                │
                ├── IAnalysisObserver[]
                │
                ▼
     IMessageEffectExecutor
                │
                ▼
 platform/consumer effect handlers
```

Normalization and tokenization happen once per pipeline execution. Analyzers produce facts; policies turn facts into effects. This separation allows the same URL, keyword, or entity analyzer to be reused by several policies without repeating parsing work.

## Core API

### Normalization

```csharp
public interface ITextNormalizer
{
    NormalizedText Normalize(string text);
}

public interface ITextTokenizer
{
    IReadOnlyList<Token> Tokenize(string canonicalText);
}
```

`DefaultTextNormalizer` performs business-neutral transformations only:

-   Unicode NFKC by default;
-   invariant case folding;
-   removal of zero-width, bidi-control, and other format characters;
-   replacement and signaling of malformed UTF-16 input instead of propagating normalization faults;
-   whitespace collapse and trimming;
-   conservative normalization of common quote and dash characters;
-   generic formatting signals;
-   source-span preservation.

It deliberately does not fold homoglyphs, transliteration, or leetspeak. Those transforms are semantically ambiguous and belong in a consumer analyzer.

`NormalizedText.MapToOriginal` maps analyzer matches back to the smallest original UTF-16 span that produced them. This lets a future module explain a match using the actual user input rather than a rewritten canonical string.

### Analyzers

```csharp
public interface ITextAnalyzer
{
    string Name { get; }
    int Order { get; }

    ValueTask<AnalysisResult> AnalyzeAsync(
        TextAnalysisContext context,
        CancellationToken cancellationToken = default);
}
```

Analyzers receive both normalized text and `TextProcessingContext`. The latter carries generic message metadata such as source, message id, request/correlation ids, and a typed property bag. Standard property keys include tenant, scope, player, chat, user, content type, thread, and sent time. An analyzer may use tenant-aware repositories or compiled snapshots, but it should return facts only.

Analyzer order is deterministic: `Order`, then `Name`, then implementation type name. Analyzer names must be non-empty and unique inside a pipeline. Analyzer ids are validated against returned `AnalysisResult.AnalyzerId` to prevent unstable telemetry and policy lookups.

### Matchers

```csharp
public interface IMatcher<TPattern>
{
    IReadOnlyList<Match> Match(
        NormalizedText text,
        IReadOnlyList<TPattern> patterns);
}
```

`IMatcher<TPattern>` is intentionally a low-level synchronous CPU contract. Loading patterns, settings, or snapshots is the analyzer's responsibility. A matcher must not perform network or storage IO.

`Pattern` is a minimal optional common model. Consumers may use their own strongly typed pattern records instead.

### Policies

```csharp
public interface ITextPolicy
{
    string Name { get; }
    int Order { get; }

    ValueTask<PolicyDecision> EvaluateAsync(
        TextPolicyContext context,
        CancellationToken cancellationToken = default);
}
```

Several modules can contribute policies to one pipeline. Policy names must be non-empty and unique. `CompositeDecisionEngine` evaluates them in deterministic order and delegates final composition to `IDecisionComposer`.

A policy can mark its decision terminal. This prevents lower-priority policies from running while preserving decisions already produced. Terminal behavior should be used sparingly because it is a cross-module control-flow decision.

`DefaultDecisionComposer` concatenates effects and namespaces policy values. It does not attempt to infer whether two effects conflict or are duplicates. Semantic resolution is consumer-specific; replace `IDecisionComposer` when an application needs conflict handling.

### Effects

Effects are data contracts. They do not call Telegram, Discord, a database, or a queue themselves. The core package currently provides generic contracts such as:

-   `ReplyEffect`;
-   `DeleteMessageEffect`;
-   `AddReactionEffect`;
-   `SetMessageReactionsEffect`;
-   `QueueEffect`;
-   `LogEffect`;
-   `IgnoreEffect`.

A platform or consumer registers an `IMessageEffectHandler` for each effect it supports. The `MessageEffectHandler<TEffect>` base class provides type-safe dispatch.

`MessageEffectExecutor` executes effects serially and returns a `MessageEffectExecutionReport`. The default missing-handler behavior is to throw. A host may select `Skip` when effects are optional or when a shared decision can target several transports with different capabilities.

The core does not provide transport implementations. `BotFramework.Telegram` offers optional handlers for `ReplyEffect`, `DeleteMessageEffect`, `AddReactionEffect`, and `SetMessageReactionsEffect`; applications opt into them with `AddTelegramTextEffectHandlers()`. Durable queues, retry policy, logging backends, and other business effects remain module-owned.

### Observers

`IAnalysisObserver` receives the complete pipeline result after decisions are produced and before effects execute. Observers can bridge to OpenTelemetry, metrics, structured logs, or an analytical event sink. They must not be required for correctness.

### Compiled snapshots

```csharp
public sealed record CompiledSnapshot<T>
{
    public required T Value { get; init; }
    public required string Version { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public interface ICompiledSnapshotProvider<in TKey, TValue>
{
    ValueTask<CompiledSnapshot<TValue>> GetAsync(
        TKey key,
        CancellationToken cancellationToken = default);
}
```

The consumer owns storage, compilation, invalidation, and publication. The framework only defines the immutable hand-off boundary. A future word-filter module can store tries, compiled regexes, or fuzzy indexes in a snapshot without adding those algorithms to BotFramework itself.

## Dependency injection

```csharp
services
    .AddTextProcessing()
    .AddTextAnalyzer<UrlAnalyzer>()
    .AddTextAnalyzer<KeywordAnalyzer>()
    .AddTextPolicy<LinkPolicy>()
    .AddTextPolicy<ResponsePolicy>()
    .AddTextEffectHandler<TelegramReplyEffectHandler>()
    .AddTextObserver<TextTelemetryObserver>();
```

Consumers normally depend on `ITextProcessingPipeline`; the concrete `TextPipeline` remains available for composition and direct tests. Both resolve to the same scoped instance.

The normalizer, tokenizer, options, and default composer are singleton-safe. The pipeline, decision engine, analyzers, policies, observers, effect executor, and effect handlers default to scoped lifetimes so they may depend on tenant context, repositories, or other request-scoped services. Pure implementations may explicitly opt into singleton registration.

Customizations:

```csharp
services.AddTextTokenizer<MyTokenizer>();
services.AddTextNormalizer<MyNormalizer>();
services.AddTextDecisionComposer<MyComposer>();
services.AddTextDecisionEngine<MyDecisionEngine>();
```

`AddTelegramTextProcessing` registers the common pipeline and `TelegramTextPipelineAdapter`. The adapter maps text/caption and trusted Telegram metadata; it does not register analyzers or policies. An overload accepts an inherited `TextProcessingContext`, allowing the composition root to attach resolved tenant/scope and request metadata before analysis. Telegram-owned fields replace conflicting property-bag values. Standard Telegram effect handlers are explicitly opt-in:

```csharp
services
    .AddTelegramTextProcessing()
    .AddTelegramTextEffectHandlers();
```

A consumer may omit that call and register custom handlers instead.

## Example consumer module

The following sketch demonstrates the intended amount of module-owned glue. It is deliberately not a censorship implementation.

```csharp
public sealed class KeywordAnalyzer(
    ICompiledSnapshotProvider<long, KeywordIndex> snapshots) : ITextAnalyzer
{
    public string Name => "keywords";
    public int Order => 100;

    public async ValueTask<AnalysisResult> AnalyzeAsync(
        TextAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var chatId = context.ProcessingContext.GetRequiredProperty<long>(TextProcessingKeys.ChatId);
        var snapshot = await snapshots.GetAsync(chatId, cancellationToken);
        var matches = snapshot.Value.Match(context.Text);
        return new AnalysisResult
        {
            AnalyzerId = Name,
            Matches = matches,
            Values = new Dictionary<string, object?>
            {
                ["snapshot_version"] = snapshot.Version,
            },
        };
    }
}

public sealed class KeywordResponsePolicy : ITextPolicy
{
    public string Name => "keyword_responses";
    public int Order => 100;

    public ValueTask<PolicyDecision> EvaluateAsync(
        TextPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        var matches = context.Analysis.Results
            .FirstOrDefault(result => result.AnalyzerId == "keywords")?
            .Matches ?? [];

        return ValueTask.FromResult(new PolicyDecision
        {
            PolicyId = Name,
            Effects = matches.Count == 0
                ? []
                : [new ReplyEffect("A configured keyword matched.")],
        });
    }
}
```

The module remains responsible for its rules, permissions, persistence, settings, and platform handler registrations.

## Concurrency and failure semantics

-   `TextPipeline` is scoped and can be invoked once or several times inside one request scope.
-   Normalized models and decisions are immutable publication objects.
-   Analyzers and policies run serially in deterministic order.
-   Cancellation is checked between every stage and passed to all asynchronous extensions.
-   Analyzer and policy exceptions fail the pipeline. The framework does not silently convert programming, storage, or configuration faults into an empty decision.
-   Observer exceptions currently fail the pipeline; production observers should isolate optional exporters internally.
-   Effect handlers run serially. This preserves declared effect order and avoids pretending that arbitrary side effects are commutative.
-   A consumer requiring transactions, retries, scheduling, or durable execution should emit one durable-queue effect whose handler owns those semantics.

## Non-goals

`BotFramework.Text` does not provide:

-   a general-purpose workflow engine;
-   a persistence model for rules;
-   a universal policy language;
-   fuzzy matching or a built-in regex rule engine;
-   moderation commands or permissions;
-   delayed deletion;
-   automatic update interception;
-   transport-specific retry behavior;
-   analytics storage.

These omissions are intentional. The package defines stable seams where modules can implement those capabilities without modifying the framework.

## Acceptance criteria

The subsystem is considered complete when:

1.  Text and captions can be adapted into one platform-neutral pipeline input.
2.  The default normalizer is Unicode-safe and preserves original spans.
3.  Normalization and tokenization execute once per pipeline run.
4.  Independent modules can register scoped analyzers and policies through DI.
5.  Policy order and analyzer order are deterministic.
6.  Effects are contracts and can be handled by arbitrary transports or modules.
7.  Missing effect handlers have explicit fail or skip semantics.
8.  Observability is available without coupling the core to a backend.
9.  Compiled indexes can be published through consumer-owned immutable snapshots.
10.  A future censorship or anti-spam module can be implemented entirely outside `BotFramework.Text`.