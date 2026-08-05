using System.Text;

namespace BotFramework.Text;

public sealed record TextNormalizerOptions
{
    public NormalizationForm UnicodeForm { get; init; } = NormalizationForm.FormKC;
    public bool LowerInvariant { get; init; } = true;
    public bool RemoveZeroWidthCharacters { get; init; } = true;
    public bool CollapseWhitespace { get; init; } = true;
    /// <summary>
    /// Trims leading and trailing whitespace when <see cref="CollapseWhitespace"/> is enabled.
    /// </summary>
    public bool TrimWhitespace { get; init; } = true;
}
