namespace TextRules.Domain.Rules;

public readonly record struct TextRuleId
{
    public TextRuleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A text rule id is required.", nameof(value));

        Value = value.Trim();
    }

    public string Value { get; } = string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString() => Value;
}
