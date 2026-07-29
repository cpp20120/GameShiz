namespace ChatAdministration.Domain.Effects;

public static class EffectTypeCatalog
{
    public const string RestrictMember = "telegram.restrict_member";
    public const string UnrestrictMember = "telegram.unrestrict_member";
    public const string BanMember = "telegram.ban_member";
    public const string UnbanMember = "telegram.unban_member";
    public const string KickMember = "telegram.kick_member";
    public const string DeleteMessage = "telegram.delete_message";
    public const string DeleteMessages = "telegram.delete_messages";
    public const string EditMessage = "telegram.edit_message";
    public const string PinMessage = "telegram.pin_message";
    public const string UnpinMessage = "telegram.unpin_message";
    public const string AnswerCallbackQuery = "telegram.answer_callback_query";
    public const string SendMessage = "telegram.send_message";
    public const string SendModerationLog = "telegram.send_moderation_log";
    public const string GetChatMember = "telegram.get_chat_member";
    public const string GetChatAdministrators = "telegram.get_chat_administrators";
    public const string GetBotPermissions = "telegram.get_bot_permissions";
    public const string Schedule = "scheduler.schedule";
    public const string CancelScheduled = "scheduler.cancel";
    public const string PersistAggregate = "persistence.persist_aggregate";
    public const string AppendDomainEvents = "persistence.append_domain_events";
    public const string CreateModerationCase = "persistence.create_moderation_case";
    public const string UpdateModerationCase = "persistence.update_moderation_case";
    public const string SaveVerificationSession = "persistence.save_verification_session";
    public const string WriteAuditEvent = "audit.write_event";
    public const string MarkCaseRevoked = "persistence.mark_case_revoked";
    public const string EmitMetric = "telemetry.emit_metric";
    public const string EmitTraceEvent = "telemetry.emit_trace";
    public const string WriteStructuredLog = "telemetry.write_log";
    public const string NotifyAdministrators = "telegram.notify_administrators";
}
