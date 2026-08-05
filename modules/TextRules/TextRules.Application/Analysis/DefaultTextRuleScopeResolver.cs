using System.Globalization;
using BotFramework.Text;
using TextRules.Domain.Rules;

namespace TextRules.Application.Analysis;

public sealed class DefaultTextRuleScopeResolver : ITextRuleScopeResolver
{
    public RuleScope Resolve(TextProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenantId = ReadIdentifier(context, TextProcessingKeys.TenantId);
        var chatId = ReadIdentifier(context, TextProcessingKeys.ChatId);
        return new RuleScope(tenantId, chatId);
    }

    private static string? ReadIdentifier(TextProcessingContext context, string key)
    {
        if (!context.Properties.TryGetValue(key, out var value) || value is null)
            return null;
        if (value is string text)
            return text;
        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString();
    }
}
