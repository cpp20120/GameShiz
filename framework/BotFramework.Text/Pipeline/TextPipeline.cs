namespace BotFramework.Text;

/// <summary>
/// Normalizes once, executes analyzers and policies deterministically, notifies observers,
/// and optionally executes the resulting platform-neutral effects.
/// </summary>
public sealed class TextPipeline : ITextProcessingPipeline
{
    private readonly ITextNormalizer _normalizer;
    private readonly ITextAnalyzer[] _analyzers;
    private readonly IDecisionEngine _decisionEngine;
    private readonly IMessageEffectExecutor? _effectExecutor;
    private readonly IAnalysisObserver[] _observers;

    public TextPipeline(
        ITextNormalizer normalizer,
        IEnumerable<ITextAnalyzer>? analyzers = null,
        IDecisionEngine? decisionEngine = null,
        IMessageEffectExecutor? effectExecutor = null,
        IEnumerable<IAnalysisObserver>? observers = null)
    {
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        var registeredAnalyzers = (analyzers ?? []).ToArray();
        if (registeredAnalyzers.Any(static analyzer => analyzer is null))
            throw new ArgumentException("The analyzer collection cannot contain null.", nameof(analyzers));

        ValidateUniqueAnalyzerNames(registeredAnalyzers);
        _analyzers = registeredAnalyzers
            .OrderBy(analyzer => analyzer.Order)
            .ThenBy(analyzer => analyzer.Name, StringComparer.Ordinal)
            .ThenBy(analyzer => analyzer.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
        _decisionEngine = decisionEngine ?? new CompositeDecisionEngine();
        _effectExecutor = effectExecutor;
        var registeredObservers = (observers ?? []).ToArray();
        if (registeredObservers.Any(static observer => observer is null))
            throw new ArgumentException("The observer collection cannot contain null.", nameof(observers));

        _observers = registeredObservers
            .OrderBy(observer => observer.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Runs the complete pipeline and executes decision effects when an executor is available.
    /// </summary>
    public ValueTask<TextPipelineResult> ProcessAsync(
        string text,
        TextProcessingContext? context = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(text, context, executeEffects: true, cancellationToken: cancellationToken);

    /// <summary>
    /// Runs normalization, analyzers, policies, and observers without executing effects.
    /// Useful for previews, diagnostics, and dry-run commands.
    /// </summary>
    public ValueTask<TextPipelineResult> AnalyzeAsync(
        string text,
        TextProcessingContext? context = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(text, context, executeEffects: false, cancellationToken: cancellationToken);

    private async ValueTask<TextPipelineResult> RunAsync(
        string text,
        TextProcessingContext? context,
        bool executeEffects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        var processingContext = context ?? new TextProcessingContext();
        var normalized = _normalizer.Normalize(text);
        var analyzerContext = new TextAnalysisContext
        {
            Text = normalized,
            ProcessingContext = processingContext,
        };
        var results = new List<AnalysisResult>(_analyzers.Length);

        foreach (var analyzer in _analyzers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await analyzer.AnalyzeAsync(analyzerContext, cancellationToken);
            if (result is null)
                throw new InvalidOperationException($"Analyzer '{analyzer.Name}' returned null.");
            if (!string.Equals(result.AnalyzerId, analyzer.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Analyzer '{analyzer.Name}' returned result id '{result.AnalyzerId}'. Analyzer ids must be stable.");
            }

            results.Add(result);
        }

        var analysis = new TextAnalysis
        {
            ProcessingContext = processingContext,
            Text = normalized,
            Results = results.ToArray(),
        };
        var decision = await _decisionEngine.DecideAsync(analysis, cancellationToken);
        if (decision is null)
            throw new InvalidOperationException("The decision engine returned null.");

        var resultBeforeEffects = new TextPipelineResult
        {
            Context = processingContext,
            Text = normalized,
            Analysis = analysis,
            Decision = decision,
        };

        foreach (var observer in _observers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await observer.ObserveAsync(resultBeforeEffects, cancellationToken);
        }

        var execution = !executeEffects || _effectExecutor is null || decision.Effects.Count == 0
            ? MessageEffectExecutionReport.Empty
            : await _effectExecutor.ExecuteAsync(decision.Effects, processingContext, cancellationToken);

        return resultBeforeEffects with { EffectExecution = execution };
    }
    private static void ValidateUniqueAnalyzerNames(IReadOnlyList<ITextAnalyzer> analyzers)
    {
        var invalid = analyzers
            .Where(static analyzer => string.IsNullOrWhiteSpace(analyzer.Name))
            .Select(static analyzer => analyzer.GetType().FullName ?? analyzer.GetType().Name)
            .ToArray();
        if (invalid.Length > 0)
        {
            throw new InvalidOperationException(
                $"Text analyzers must have non-empty names: {string.Join(", ", invalid)}.");
        }

        var duplicates = analyzers
            .GroupBy(static analyzer => analyzer.Name, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Text analyzer names must be unique: {string.Join(", ", duplicates)}.");
        }
    }

}
