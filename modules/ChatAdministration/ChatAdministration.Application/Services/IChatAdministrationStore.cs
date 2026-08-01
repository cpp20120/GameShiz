using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public interface IChatAdministrationStore
{
    Task UpsertChatMetadataAsync(ChatMetadataCommand command, CancellationToken ct);
    Task<ResolvedTarget?> FindMemberByUsernameAsync(ChatId chatId, string username, CancellationToken ct);
    Task<ResolvedTarget?> FindMessageAuthorAsync(ChatId chatId, int messageId, CancellationToken ct);
    Task<ModerationContext> LoadContextAsync(
        ChatId chatId,
        UserId actorUserId,
        UserId targetUserId,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
        CancellationToken ct);

    Task RecordMessageAsync(MessageIndexEntry entry, CancellationToken ct);
    Task<IReadOnlyList<int>> ListMessageIdsAsync(ChatId chatId, UserId? targetUserId, int limit, CancellationToken ct);
    Task<PersistCommandResult> PersistPurgeAsync(PurgeMessagesCommand command, DeleteMessagesEffect effect, int messageCount, CancellationToken ct);
    Task<PersistCommandResult> PersistAsync(PersistModerationCommand command, CancellationToken ct);
    Task<IReadOnlyList<WarningState>> ListWarningsAsync(ChatId chatId, UserId targetUserId, bool activeOnly, CancellationToken ct);
    Task<PersistCommandResult> PersistWarningMutationAsync(WarningMutationCommand command, CancellationToken ct);
    Task<int> ExpireWarningsAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<PersistCommandResult> PersistRoleMutationAsync(RoleMutationCommand command, CancellationToken ct);
    Task<PersistCommandResult> PersistMemberJoinedAsync(MemberJoinedCommand command, MemberLifecycleDecision decision, CancellationToken ct);
    Task<PersistCommandResult> PersistMemberLeftAsync(MemberLeftCommand command, MemberLifecycleDecision decision, CancellationToken ct);
    Task<IReadOnlyList<ModerationCaseState>> ListCasesAsync(ChatId chatId, UserId? targetUserId, int limit, CancellationToken ct);
    Task<ModerationAnalytics> LoadAnalyticsAsync(ChatId chatId, CancellationToken ct);
    Task<ModerationCaseState?> LoadCaseAsync(ChatId chatId, ModerationCaseId caseId, CancellationToken ct);
    Task<PersistCommandResult> PersistCaseRevocationAsync(RevokeModerationCaseCommand command, CaseRevocationDecision decision, CancellationToken ct);
    Task<AppealState?> LoadAppealAsync(ChatId chatId, AppealId appealId, CancellationToken ct);
    Task<PersistCommandResult> PersistAppealOpenAsync(OpenAppealCommand command, AppealDecision decision, CancellationToken ct);
    Task<PersistCommandResult> PersistAppealResolutionAsync(ResolveAppealCommand command, AppealDecision decision, CaseRevocationDecision? revocation, CancellationToken ct);
    Task UpdateChatSettingsAsync(ChatId chatId, ChatSettings settings, UserId actorUserId, string correlationId, CancellationToken ct);
    Task<IReadOnlyList<ChatState>> ListEnabledChatsAsync(CancellationToken ct);
    Task<IReadOnlyList<ChatState>> ListRegisteredChatsAsync(CancellationToken ct);
    Task<int> CleanupRetentionAsync(ChatId chatId, RetentionPolicy policy, DateTimeOffset now, int batchSize, CancellationToken ct);
    Task<IReadOnlyList<DesiredMemberRestriction>> ListDesiredRestrictionsAsync(ChatId chatId, int limit, CancellationToken ct);
    Task UpdateObservedRestrictionAsync(ChatId chatId, UserId userId, RestrictionState? state, string correlationId, CancellationToken ct);
    Task UpdateBotPermissionsAsync(ChatId chatId, TelegramBotPermissions permissions, string correlationId, CancellationToken ct);
    Task<string> CreateSettingsCallbackAsync(ChatId chatId, string key, string value, DateTimeOffset expiresAt, CancellationToken ct);
    Task<SettingsCallbackState?> ConsumeSettingsCallbackAsync(string token, ChatId chatId, UserId actorUserId, CancellationToken ct);
    Task EnqueueEffectAsync(IModerationEffect effect, string idempotencyKey, EffectImportance importance, CancellationToken ct);
    Task EnqueueScheduledEffectAsync(IModerationEffect effect, DateTimeOffset executeAt, string idempotencyKey, EffectImportance importance, CancellationToken ct);
    Task CancelEffectAsync(EffectId effectId, CancellationToken ct);

    Task<VerificationSession?> LoadVerificationAsync(VerificationSessionId sessionId, CancellationToken ct);
    Task<VerificationPersistenceResult> PersistVerificationAsync(
        VerificationSession session,
        VerificationStatus expectedStatus,
        IReadOnlyCollection<IDomainEvent> events,
        EffectPlan effectPlan,
        string correlationId,
        string causationId,
        CancellationToken ct);
    Task<IReadOnlyList<VerificationSession>> ListExpiredVerificationsAsync(DateTimeOffset now, int limit, CancellationToken ct);

    Task EnqueueResponseAsync(ChatId chatId, string text, int? replyToMessageId, CancellationToken ct);

    Task<IReadOnlyList<StoredModerationEffect>> ClaimDueEffectsAsync(int limit, TimeSpan lease, CancellationToken ct);
    Task MarkEffectAppliedAsync(StoredModerationEffect effect, CancellationToken ct);
    Task MarkEffectFailedAsync(StoredModerationEffect effect, string code, string message, bool retryable, TimeSpan? retryAfter, CancellationToken ct);
    Task MarkEffectUnknownAsync(StoredModerationEffect effect, string code, string message, CancellationToken ct);
    Task<IReadOnlyList<StoredModerationEffect>> ListUnknownEffectsAsync(int limit, CancellationToken ct);
    Task RequeueUnknownAsync(StoredModerationEffect effect, CancellationToken ct);
    Task ConfirmUnknownAppliedAsync(StoredModerationEffect effect, CancellationToken ct);
}
