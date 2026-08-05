namespace TextRules.Domain.Rules;

public sealed record RuleScope
{
    public RuleScope(string? tenantId, string? chatId)
    {
        TenantId = NormalizePart(tenantId, nameof(tenantId));
        ChatId = NormalizePart(chatId, nameof(chatId));

        if (ChatId is not null && TenantId is null)
        {
            throw new ArgumentException(
                "A chat-scoped rule must also specify a tenant id.",
                nameof(tenantId));
        }
    }

    public static RuleScope Global { get; } = new(null, null);

    public string? TenantId { get; }
    public string? ChatId { get; }

    public bool IsGlobal => TenantId is null && ChatId is null;
    public bool IsTenant => TenantId is not null && ChatId is null;
    public bool IsChat => TenantId is not null && ChatId is not null;
    public int Specificity
    {
        get
        {
            if (IsChat)
                return 2;
            return IsTenant ? 1 : 0;
        }
    }
    public bool IsValid => !IsChat || TenantId is not null;

    public static RuleScope ForTenant(string tenantId) => new(tenantId, null);

    public static RuleScope ForChat(string tenantId, string chatId) => new(tenantId, chatId);

    public override string ToString()
    {
        if (IsGlobal)
            return "global";
        if (IsChat)
            return $"tenant:{TenantId}/chat:{ChatId}";
        return $"tenant:{TenantId}";
    }

    private static string? NormalizePart(string? value, string parameterName)
    {
        if (value is null)
            return null;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A scope identifier cannot be empty.", parameterName);

        return value.Trim();
    }
}
