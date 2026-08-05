namespace TextRules.Domain.Rules;

public sealed class TextRuleValidationException : InvalidOperationException
{
    public TextRuleValidationException()
        : this([])
    {
    }

    public TextRuleValidationException(string message)
        : base(message)
    {
        Errors = [];
    }

    public TextRuleValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [];
    }

    public TextRuleValidationException(IReadOnlyList<TextRuleValidationError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public IReadOnlyList<TextRuleValidationError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<TextRuleValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
            return "Text rule validation failed.";

        return "Text rule validation failed: "
            + string.Join("; ", errors.Select(error => $"[{error.Code}] {error.Message}"));
    }
}
