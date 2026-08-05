namespace BotFramework.Text;

/// <summary>
/// Converts analyzer facts into consumer-defined effects. Policies are composed in deterministic order.
/// </summary>
public interface ITextPolicy
{
    string Name { get; }
    int Order { get; }

    ValueTask<PolicyDecision> EvaluateAsync(
        TextPolicyContext context,
        CancellationToken cancellationToken = default);
}
