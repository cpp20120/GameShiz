namespace BotFramework.Text;

/// <summary>
/// Conventional property keys used by platform adapters. Consumers may add arbitrary keys of their own.
/// </summary>
public static class TextProcessingKeys
{
    public const string TenantId = "tenant_id";
    public const string ScopeId = "scope_id";
    public const string PlayerId = "player_id";
    public const string ChatId = "chat_id";
    public const string UserId = "user_id";
    public const string ContentType = "content_type";
    public const string ThreadId = "thread_id";
    public const string SentAt = "sent_at";
}
