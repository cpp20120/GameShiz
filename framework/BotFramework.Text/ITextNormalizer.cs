namespace BotFramework.Text;

public interface ITextNormalizer
{
    NormalizedText Normalize(string text);
}
