namespace BotFramework.Text;

public interface IDecisionComposer
{
    Decision Compose(
        TextAnalysis analysis,
        IReadOnlyList<PolicyDecision> policyDecisions);
}
