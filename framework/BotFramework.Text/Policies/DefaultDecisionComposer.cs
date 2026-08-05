namespace BotFramework.Text;

/// <summary>
/// Preserves policy order, concatenates effects, and namespaces policy values by policy id.
/// It deliberately performs no semantic effect de-duplication.
/// </summary>
public sealed class DefaultDecisionComposer : IDecisionComposer
{
    public Decision Compose(
        TextAnalysis analysis,
        IReadOnlyList<PolicyDecision> policyDecisions)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(policyDecisions);

        if (policyDecisions.Count == 0)
            return Decision.Empty;

        var effects = new List<IMessageEffect>();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var policyDecision in policyDecisions)
        {
            effects.AddRange(policyDecision.Effects);
            foreach (var pair in policyDecision.Values)
                values[$"{policyDecision.PolicyId}.{pair.Key}"] = pair.Value;
        }

        return new Decision
        {
            Effects = effects.ToArray(),
            PolicyDecisions = policyDecisions.ToArray(),
            Values = values,
        };
    }
}
