using System.Security.Cryptography;
using System.Text;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed class DuplicateMessageRule(RuleId id, int maximumDuplicates = 1) : IModerationRule
{
    public RuleId Id { get; } = id;

    public Violation? Evaluate(ModerationMessageContext context)
    {
        var hash = Hash(context.Message.Text);
        var duplicates = context.History.RecentMessageHashes.Count(value => string.Equals(value, hash, StringComparison.Ordinal));
        if (maximumDuplicates < 0 || duplicates <= maximumDuplicates)
            return null;

        return new Violation
        {
            RuleId = Id,
            Code = "duplicate_message",
            Score = 6,
            Severity = ViolationSeverity.Medium,
            Reason = "Повторяющееся сообщение.",
            Metadata = new Dictionary<string, object?> { ["duplicates"] = duplicates },
        };
    }

    private static string Hash(string? text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(text))));

    private static string Normalize(string? text) => string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
