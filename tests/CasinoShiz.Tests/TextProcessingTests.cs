using BotFramework.Text;
using BotFramework.Telegram.Text;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class TextProcessingTests
{
    [Fact]
    public void NormalizerBuildsCanonicalTextTokensAndSourceMapping()
    {
        var normalized = new DefaultTextNormalizer().Normalize("  Hello\u200B   Мир  ");

        Assert.Equal("hello мир", normalized.CanonicalText);
        Assert.Equal(["hello", "мир"], normalized.Tokens.Select(token => token.Text));
        Assert.Contains(TextSignal.ZeroWidth, normalized.Signals);
        Assert.Equal("Hello", normalized.MapToOriginal(normalized.Tokens[0].Span)!.Value.Slice(normalized.OriginalText));
        Assert.Equal("Мир", normalized.MapToOriginal(normalized.Tokens[1].Span)!.Value.Slice(normalized.OriginalText));
    }

    [Fact]
    public void NormalizerRemovesFormatAndBidirectionalControlsAndPreservesRuneSpans()
    {
        var normalized = new DefaultTextNormalizer().Normalize("A\u202E😀\u2069B");

        Assert.Equal("a😀b", normalized.CanonicalText);
        Assert.Contains(TextSignal.Bidirectional, normalized.Signals);
        var emojiSpan = new TextSpan(1, 2);
        Assert.Equal("😀", normalized.MapToOriginal(emojiSpan)!.Value.Slice(normalized.OriginalText));
    }

    [Fact]
    public void NormalizerReportsGenericFormattingSignals()
    {
        var normalized = new DefaultTextNormalizer().Normalize("abc!!! Привет");

        Assert.Contains(TextSignal.RepeatedCharacters, normalized.Signals);
        Assert.Contains(TextSignal.MixedScripts, normalized.Signals);
    }

    [Fact]
    public void NormalizerPreservesCollapsedWhitespaceWhenTrimmingIsDisabled()
    {
        var normalizer = new DefaultTextNormalizer(
            new TextNormalizerOptions { TrimWhitespace = false });

        var normalized = normalizer.Normalize("   ");

        Assert.Equal(" ", normalized.CanonicalText);
        Assert.Equal("   ", normalized.MapToOriginal(new TextSpan(0, 1))!.Value.Slice(normalized.OriginalText));
    }

    [Fact]
    public void NormalizerReplacesMalformedUtf16InsteadOfThrowing()
    {
        var normalized = new DefaultTextNormalizer().Normalize("\uD800A");

        Assert.Equal("�a", normalized.CanonicalText);
        Assert.Contains(TextSignal.InvalidUnicode, normalized.Signals);
        Assert.Equal("\uD800", normalized.MapToOriginal(new TextSpan(0, 1))!.Value.Slice(normalized.OriginalText));
    }

    [Fact]
    public void NormalizationIsIdempotentForCanonicalText()
    {
        var normalizer = new DefaultTextNormalizer();
        var first = normalizer.Normalize("  Ｈｅｌｌｏ—МИР\u200B  ");
        var second = normalizer.Normalize(first.CanonicalText);

        Assert.Equal(first.CanonicalText, second.CanonicalText);
    }

    [Fact]
    public async Task PipelineRunsAnalyzersInDeterministicOrderAndExecutesDecisionEffects()
    {
        var events = new List<string>();
        var pipeline = new TextPipeline(
            new DefaultTextNormalizer(),
            [
                new RecordingAnalyzer("second", 20, events),
                new RecordingAnalyzer("first", 10, events),
            ],
            new RecordingDecisionEngine(events),
            new MessageEffectExecutor([new ReplyEffectHandler(events)]),
            [new RecordingObserver(events)]);

        var result = await pipeline.ProcessAsync(
            "Hello",
            new TextProcessingContext
            {
                MessageId = "message-1",
                Properties = new Dictionary<string, object?> { ["chat_id"] = 42L },
            });

        Assert.Equal(["first:42", "second:42", "decision", "observer", "reply"], events);
        Assert.Equal("message-1", result.Context.MessageId);
        Assert.Equal(2, result.Analysis.Results.Count);
        Assert.Single(result.Decision.Effects);
        Assert.Equal(MessageEffectExecutionStatus.Executed, Assert.Single(result.EffectExecution.Items).Status);
    }


    [Fact]
    public async Task AnalyzeAsyncProducesDecisionWithoutExecutingEffects()
    {
        var events = new List<string>();
        var pipeline = new TextPipeline(
            new DefaultTextNormalizer(),
            decisionEngine: new RecordingDecisionEngine(events),
            effectExecutor: new MessageEffectExecutor([new ReplyEffectHandler(events)]));

        var result = await pipeline.AnalyzeAsync("hello");

        Assert.Equal(["decision"], events);
        Assert.Single(result.Decision.Effects);
        Assert.Empty(result.EffectExecution.Items);
    }

    [Fact]
    public async Task CompositeDecisionEngineComposesPoliciesInOrderAndStopsAfterTerminalDecision()
    {
        var events = new List<string>();
        var pipeline = new TextPipeline(
            new DefaultTextNormalizer(),
            decisionEngine: new CompositeDecisionEngine(
            [
                new RecordingPolicy("last", 30, events),
                new RecordingPolicy("terminal", 20, events, terminal: true),
                new RecordingPolicy("first", 10, events),
            ]));

        var result = await pipeline.ProcessAsync("hello");

        Assert.Equal(["first", "terminal"], events);
        Assert.Equal(["first", "terminal"], result.Decision.PolicyDecisions.Select(decision => decision.PolicyId));
        Assert.Equal(2, result.Decision.Effects.Count);
        Assert.DoesNotContain(result.Decision.PolicyDecisions, decision => decision.PolicyId == "last");
    }

    [Fact]
    public void PipelineRejectsDuplicateAnalyzerNames()
    {
        var events = new List<string>();

        var error = Assert.Throws<InvalidOperationException>(() => new TextPipeline(
            new DefaultTextNormalizer(),
            [
                new RecordingAnalyzer("duplicate", 10, events),
                new RecordingAnalyzer("duplicate", 20, events),
            ]));

        Assert.Contains("unique", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecisionEngineRejectsDuplicatePolicyNames()
    {
        var events = new List<string>();

        var error = Assert.Throws<InvalidOperationException>(() => new CompositeDecisionEngine(
        [
            new RecordingPolicy("duplicate", 10, events),
            new RecordingPolicy("duplicate", 20, events),
        ]));

        Assert.Contains("unique", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EffectExecutorCanSkipUnknownEffectsWhenConfigured()
    {
        var executor = new MessageEffectExecutor(
            options: new MessageEffectExecutorOptions
            {
                MissingHandlerBehavior = MissingMessageEffectHandlerBehavior.Skip,
            });

        var report = await executor.ExecuteAsync(
            [new QueueEffect("consumer-owned")],
            new TextProcessingContext());

        Assert.Equal(MessageEffectExecutionStatus.Skipped, Assert.Single(report.Items).Status);
    }

    [Fact]
    public void DependencyInjectionComposesAnalyzersPoliciesAndEffectsFromIndependentModules()
    {
        var services = new ServiceCollection();
        services
            .AddTextProcessing()
            .AddTextAnalyzer<DiAnalyzer>()
            .AddTextPolicy<DiPolicy>()
            .AddTextEffectHandler<DiReplyEffectHandler>();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<TextPipeline>();
        var contract = scope.ServiceProvider.GetRequiredService<ITextProcessingPipeline>();

        Assert.NotNull(pipeline);
        Assert.Same(pipeline, contract);
        Assert.IsType<CompositeDecisionEngine>(scope.ServiceProvider.GetRequiredService<IDecisionEngine>());
        Assert.Single(scope.ServiceProvider.GetServices<ITextAnalyzer>());
        Assert.Single(scope.ServiceProvider.GetServices<ITextPolicy>());
    }

    [Fact]
    public void MatcherAndCompiledSnapshotRemainConsumerGeneric()
    {
        var normalized = new DefaultTextNormalizer().Normalize("hello world");
        var pattern = new Pattern { Id = "greeting", Kind = "literal", Value = "hello" };
        var matches = new LiteralMatcher().Match(normalized, [pattern]);
        var snapshot = new CompiledSnapshot<IReadOnlyDictionary<string, string>>
        {
            Value = new Dictionary<string, string> { ["greeting"] = "hello" },
            Version = "v1",
        };

        Assert.Equal("greeting", Assert.IsType<Pattern>(Assert.Single(matches).Pattern).Id);
        Assert.Equal("v1", snapshot.Version);
    }

    [Fact]
    public async Task TelegramAdapterPreservesInheritedTenantAndRequestContext()
    {
        var adapter = new TelegramTextPipelineAdapter(
            new TextPipeline(new DefaultTextNormalizer()));
        var result = await adapter.AnalyzeAsync(
            new Message
            {
                Id = 42,
                Chat = new Chat { Id = 123 },
                Text = "Hello",
            },
            new TextProcessingContext
            {
                RequestId = "request-1",
                CorrelationId = "correlation-1",
                Properties = new Dictionary<string, object?>
                {
                    [TextProcessingKeys.TenantId] = "tenant-a",
                    [TextProcessingKeys.ScopeId] = "main",
                    [TextProcessingKeys.ChatId] = -1L,
                },
            });

        Assert.Equal("request-1", result.Context.RequestId);
        Assert.Equal("correlation-1", result.Context.CorrelationId);
        Assert.Equal("tenant-a", result.Context.Properties[TextProcessingKeys.TenantId]);
        Assert.Equal("main", result.Context.Properties[TextProcessingKeys.ScopeId]);
        Assert.Equal(123L, result.Context.Properties[TextProcessingKeys.ChatId]);
    }

    [Fact]
    public void TelegramEffectHandlersAreExplicitlyOptIn()
    {
        var services = new ServiceCollection();
        services.AddTelegramTextProcessing();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ImplementationType == typeof(TelegramReplyEffectHandler));

        services.AddTelegramTextEffectHandlers();

        Assert.Contains(
            services,
            descriptor => descriptor.ImplementationType == typeof(TelegramReplyEffectHandler));
        Assert.Contains(
            services,
            descriptor => descriptor.ImplementationType == typeof(TelegramDeleteMessageEffectHandler));
        Assert.Contains(
            services,
            descriptor => descriptor.ImplementationType == typeof(TelegramAddReactionEffectHandler));
        Assert.Contains(
            services,
            descriptor => descriptor.ImplementationType == typeof(TelegramSetMessageReactionsEffectHandler));
    }

    [Fact]
    public async Task TelegramAdapterProcessesCaptionTextWithoutAddingBusinessRules()
    {
        var adapter = new TelegramTextPipelineAdapter(
            new TextPipeline(new DefaultTextNormalizer()));
        var result = await adapter.ProcessAsync(new Message
        {
            Id = 42,
            Chat = new Chat { Id = 123 },
            Caption = "Image caption",
        });

        Assert.Equal("image caption", result.Text.CanonicalText);
        Assert.Equal("telegram", result.Context.Source);
        Assert.Equal("caption", result.Context.Properties["content_type"]);
    }

    private sealed class RecordingAnalyzer(string name, int order, ICollection<string> events) : ITextAnalyzer
    {
        public string Name { get; } = name;
        public int Order { get; } = order;

        public ValueTask<AnalysisResult> AnalyzeAsync(
            TextAnalysisContext context,
            CancellationToken cancellationToken = default)
        {
            events.Add($"{Name}:{context.ProcessingContext.GetRequiredProperty<long>("chat_id")}");
            return ValueTask.FromResult(new AnalysisResult { AnalyzerId = Name });
        }
    }

    private sealed class RecordingDecisionEngine(ICollection<string> events) : IDecisionEngine
    {
        public ValueTask<Decision> DecideAsync(
            TextAnalysis analysis,
            CancellationToken cancellationToken = default)
        {
            events.Add("decision");
            return ValueTask.FromResult<Decision>(new Decision { Effects = [new ReplyEffect("ok")] });
        }
    }

    private sealed class RecordingPolicy(
        string name,
        int order,
        ICollection<string> events,
        bool terminal = false) : ITextPolicy
    {
        public string Name { get; } = name;
        public int Order { get; } = order;

        public ValueTask<PolicyDecision> EvaluateAsync(
            TextPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            events.Add(Name);
            return ValueTask.FromResult(new PolicyDecision
            {
                PolicyId = Name,
                Effects = [new LogEffect(Name)],
                IsTerminal = terminal,
            });
        }
    }

    private sealed class RecordingObserver(ICollection<string> events) : IAnalysisObserver
    {
        public ValueTask ObserveAsync(
            TextPipelineResult result,
            CancellationToken cancellationToken = default)
        {
            events.Add("observer");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ReplyEffectHandler(ICollection<string> events) : MessageEffectHandler<ReplyEffect>
    {
        protected override ValueTask ExecuteAsync(
            ReplyEffect effect,
            TextProcessingContext context,
            CancellationToken cancellationToken)
        {
            events.Add("reply");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LiteralMatcher : IMatcher<Pattern>
    {
        public IReadOnlyList<Match> Match(
            NormalizedText text,
            IReadOnlyList<Pattern> patterns) => patterns
            .SelectMany(pattern => Find(text.CanonicalText, pattern))
            .ToArray();

        private static IEnumerable<BotFramework.Text.Match> Find(string text, Pattern pattern)
        {
            var start = text.IndexOf(pattern.Value, StringComparison.Ordinal);
            if (start >= 0)
            {
                yield return new BotFramework.Text.Match
                {
                    Pattern = pattern,
                    Span = new TextSpan(start, pattern.Value.Length),
                };
            }
        }
    }

    public sealed class DiAnalyzer : ITextAnalyzer
    {
        public string Name => "di";
        public int Order => 0;

        public ValueTask<AnalysisResult> AnalyzeAsync(
            TextAnalysisContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AnalysisResult { AnalyzerId = Name });
    }

    public sealed class DiPolicy : ITextPolicy
    {
        public string Name => "di";
        public int Order => 0;

        public ValueTask<PolicyDecision> EvaluateAsync(
            TextPolicyContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PolicyDecision { PolicyId = Name });
    }

    public sealed class DiReplyEffectHandler : MessageEffectHandler<ReplyEffect>
    {
        protected override ValueTask ExecuteAsync(
            ReplyEffect effect,
            TextProcessingContext context,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
