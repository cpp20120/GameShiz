namespace BotFramework.Text;

public interface IMatcher<TPattern>
{
    IReadOnlyList<Match> Match(
        NormalizedText text,
        IReadOnlyList<TPattern> patterns);
}
