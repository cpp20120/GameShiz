namespace BotFramework.Text;

public sealed class TextPipeline
{
    private readonly ITextNormalizer _normalizer;
    private readonly ITextAnalyzer[] _analyzers;
    private readonly IDecisionEngine? _decisionEngine;
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
        _analyzers = (analyzers ?? [])
            .OrderBy(analyzer => analyzer.Order)
            .ThenBy(analyzer => analyzer.Name, StringComparer.Ordinal)
            .ThenBy(analyzer => analyzer.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
        _decisionEngine = decisionEngine;
        _effectExecutor = effectExecutor;
        _observers = (observers ?? []).ToArray();
    }

    public async ValueTask<TextPipelineResult> ProcessAsync(
        string text,
        TextProcessingContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var processingContext = context ?? new TextProcessingContext();
        var normalized = _normalizer.Normalize(text);
        var results = new List<AnalysisResult>(_analyzers.Length);

        foreach (var analyzer in _analyzers)
        {
            var result = await analyzer.AnalyzeAsync(normalized, cancellationToken);
            results.Add(result);
        }

        var analysis = new TextAnalysis
        {
            Text = normalized,
            Results = results.ToArray(),
        };
        var decision = _decisionEngine is null
            ? Decision.Empty
            : await _decisionEngine.DecideAsync(analysis, cancellationToken);
        var pipelineResult = new TextPipelineResult
        {
            Context = processingContext,
            Text = normalized,
            Analysis = analysis,
            Decision = decision,
        };

        foreach (var observer in _observers)
            await observer.ObserveAsync(pipelineResult, cancellationToken);

        if (_effectExecutor is not null && decision.Effects.Count > 0)
            await _effectExecutor.ExecuteAsync(decision.Effects, processingContext, cancellationToken);

        return pipelineResult;
    }
}
