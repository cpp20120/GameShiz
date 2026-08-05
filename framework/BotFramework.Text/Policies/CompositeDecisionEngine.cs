namespace BotFramework.Text;

/// <summary>
/// Default decision engine used by the framework. Independent modules contribute policies through DI.
/// </summary>
public sealed class CompositeDecisionEngine : IDecisionEngine
{
    private readonly ITextPolicy[] _policies;
    private readonly IDecisionComposer _composer;

    public CompositeDecisionEngine(
        IEnumerable<ITextPolicy>? policies = null,
        IDecisionComposer? composer = null)
    {
        var registeredPolicies = (policies ?? []).ToArray();
        if (registeredPolicies.Any(static policy => policy is null))
            throw new ArgumentException("The policy collection cannot contain null.", nameof(policies));

        ValidateUniquePolicyNames(registeredPolicies);
        _policies = registeredPolicies
            .OrderBy(policy => policy.Order)
            .ThenBy(policy => policy.Name, StringComparer.Ordinal)
            .ThenBy(policy => policy.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
        _composer = composer ?? new DefaultDecisionComposer();
    }

    public async ValueTask<Decision> DecideAsync(
        TextAnalysis analysis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        if (_policies.Length == 0)
            return Decision.Empty;

        var context = new TextPolicyContext { Analysis = analysis };
        var decisions = new List<PolicyDecision>(_policies.Length);

        foreach (var policy in _policies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = await policy.EvaluateAsync(context, cancellationToken);
            if (decision is null)
                throw new InvalidOperationException($"Policy '{policy.Name}' returned null.");
            if (!string.Equals(decision.PolicyId, policy.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Policy '{policy.Name}' returned decision id '{decision.PolicyId}'. Policy ids must be stable.");
            }

            decisions.Add(decision);
            if (decision.IsTerminal)
                break;
        }

        return _composer.Compose(analysis, decisions)
            ?? throw new InvalidOperationException("The decision composer returned null.");
    }

    private static void ValidateUniquePolicyNames(IReadOnlyList<ITextPolicy> policies)
    {
        var invalid = policies
            .Where(static policy => string.IsNullOrWhiteSpace(policy.Name))
            .Select(static policy => policy.GetType().FullName ?? policy.GetType().Name)
            .ToArray();
        if (invalid.Length > 0)
        {
            throw new InvalidOperationException(
                $"Text policies must have non-empty names: {string.Join(", ", invalid)}.");
        }

        var duplicates = policies
            .GroupBy(static policy => policy.Name, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Text policy names must be unique: {string.Join(", ", duplicates)}.");
        }
    }
}
