namespace BotFramework.Text;

/// <summary>
/// An extensible observation about text. Signals do not imply any business decision.
/// </summary>
public readonly record struct TextSignal
{
    public TextSignal(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A signal name is required.", nameof(name));

        Name = name.Trim();
    }

    public string Name { get; }

    public static readonly TextSignal MixedScripts = new("mixed_scripts");
    public static readonly TextSignal ZeroWidth = new("zero_width");
    public static readonly TextSignal RepeatedCharacters = new("repeated_characters");
    public static readonly TextSignal UnicodeNormalization = new("unicode_normalization");
    public static readonly TextSignal Bidirectional = new("bidirectional");
    public static readonly TextSignal SuspiciousFormatting = new("suspicious_formatting");

    public override string ToString() => Name;
}
