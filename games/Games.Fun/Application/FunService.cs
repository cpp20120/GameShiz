using Games.Fun.Domain;

namespace Games.Fun.Application;

public sealed class FunService(IRandomSource random)
{
    public RollOutcome Roll(string? question) =>
        RollRules.Create(random.NextInt(0, 101), question);

    public ChoiceDecision Choose(string? raw)
    {
        if (!ChoiceRules.TryParse(raw, out var options, out var error))
            return new ChoiceDecision([], -1, error);

        return new ChoiceDecision(options, random.NextInt(0, options.Count), null);
    }

    public BenAnimationChoice SelectBen() =>
        BenRules.Select(random.NextInt(0, BenRules.TotalWeight));
}
