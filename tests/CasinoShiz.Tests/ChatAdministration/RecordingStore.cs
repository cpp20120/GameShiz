using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using IDomainEvent = ChatAdministration.Domain.Policies.IDomainEvent;

namespace CasinoShiz.Tests.ChatAdministration;

internal sealed class RecordingStore : IChatAdministrationStore
{
    private readonly ModerationCaseId caseId = ModerationCaseId.New();
    private bool hasCommand;

    public int PersistCalls { get; private set; }
    public int CreatedCases { get; private set; }
    public ChatMemberRole ActorRole { get; set; } = ChatMemberRole.Moderator;
    public ResolvedTarget? MemberByUsername { get; set; }
    public ResolvedTarget? MessageAuthor { get; set; }
    public ChatSettings? UpdatedSettings { get; private set; }
    public int? LastResponseReplyToMessageId { get; private set; }
    public string? LastResponseText { get; private set; }

    public Task UpsertChatMetadataAsync(ChatMetadataCommand command, CancellationToken ct) => Task.CompletedTask;

    public Task<ResolvedTarget?> FindMemberByUsernameAsync(ChatId chatId, string username, CancellationToken ct) =>
        Task.FromResult(MemberByUsername);

    public Task<ResolvedTarget?> FindMessageAuthorAsync(ChatId chatId, int messageId, CancellationToken ct) =>
        Task.FromResult(MessageAuthor);

    public Task<ModerationContext> LoadContextAsync(
        ChatId chatId,
        UserId actorUserId,
        UserId targetUserId,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
        CancellationToken ct) =>
        Task.FromResult(new ModerationContext(
            Chat(chatId.Value),
            Member(chatId.Value, actorUserId.Value, ActorRole),
            Member(chatId.Value, targetUserId.Value, ChatMemberRole.Member)));

    public Task RecordMessageAsync(MessageIndexEntry entry, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<int>> ListMessageIdsAsync(ChatId chatId, UserId? targetUserId, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<int>>([]);

    public Task<PersistCommandResult> PersistPurgeAsync(
        PurgeMessagesCommand command,
        DeleteMessagesEffect effect,
        int messageCount,
        CancellationToken ct) =>
        Task.FromResult(new PersistCommandResult(false, null));

    public Task<PersistCommandResult> PersistAsync(PersistModerationCommand command, CancellationToken ct)
    {
        PersistCalls++;
        if (hasCommand)
            return Task.FromResult(new PersistCommandResult(true, caseId));
        hasCommand = true;
        CreatedCases++;
        return Task.FromResult(new PersistCommandResult(false, caseId));
    }

    public Task<IReadOnlyList<WarningState>> ListWarningsAsync(ChatId chatId, UserId targetUserId, bool activeOnly, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WarningState>>([]);

    public Task<PersistCommandResult> PersistWarningMutationAsync(WarningMutationCommand command, CancellationToken ct) =>
        Task.FromResult(new PersistCommandResult(false, null));

    public Task<int> ExpireWarningsAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        Task.FromResult(0);

    public Task<PersistCommandResult> PersistRoleMutationAsync(RoleMutationCommand command, CancellationToken ct) =>
        Task.FromResult(new PersistCommandResult(false, null));

    public Task<PersistCommandResult> PersistMemberJoinedAsync(MemberJoinedCommand command, MemberLifecycleDecision decision, CancellationToken ct) =>
        Task.FromResult(new PersistCommandResult(false, null));

    public Task<PersistCommandResult> PersistMemberLeftAsync(MemberLeftCommand command, MemberLifecycleDecision decision, CancellationToken ct) =>
        Task.FromResult(new PersistCommandResult(false, null));

    public Task<IReadOnlyList<ModerationCaseState>> ListCasesAsync(ChatId chatId, UserId? targetUserId, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ModerationCaseState>>([]);

    public Task<ModerationAnalytics> LoadAnalyticsAsync(ChatId chatId, CancellationToken ct) =>
        Task.FromResult(new ModerationAnalytics(chatId, 0, 0, 0, 0, 0, 0, new Dictionary<string, long>()));

    public Task<ModerationCaseState?> LoadCaseAsync(ChatId chatId, ModerationCaseId caseId, CancellationToken ct) =>
        Task.FromResult<ModerationCaseState?>(null);

    public Task<PersistCommandResult> PersistCaseRevocationAsync(RevokeModerationCaseCommand command, CaseRevocationDecision decision, CancellationToken ct) =>
        Task.FromResult(new PersistCommandResult(false, command.CaseId));

    public Task<AppealState?> LoadAppealAsync(ChatId chatId, AppealId appealId, CancellationToken ct) =>
        Task.FromResult<AppealState?>(null);

    public Task<PersistCommandResult> PersistAppealOpenAsync(OpenAppealCommand command, AppealDecision decision, CancellationToken ct) =>
        Task.FromResult(new PersistCommandResult(false, null));

    public Task<PersistCommandResult> PersistAppealResolutionAsync(ResolveAppealCommand command, AppealDecision decision, CaseRevocationDecision? revocation, CancellationToken ct) =>
        Task.FromResult(new PersistCommandResult(false, null));

    public Task UpdateChatSettingsAsync(ChatId chatId, ChatSettings settings, UserId actorUserId, string correlationId, CancellationToken ct)
    {
        UpdatedSettings = settings;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatState>> ListEnabledChatsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ChatState>>([]);

    public Task<int> CleanupRetentionAsync(ChatId chatId, RetentionPolicy policy, DateTimeOffset now, int batchSize, CancellationToken ct) =>
        Task.FromResult(0);

    public Task<IReadOnlyList<ChatState>> ListRegisteredChatsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ChatState>>([]);

    public Task<IReadOnlyList<DesiredMemberRestriction>> ListDesiredRestrictionsAsync(ChatId chatId, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DesiredMemberRestriction>>([]);

    public Task UpdateObservedRestrictionAsync(ChatId chatId, UserId userId, RestrictionState? state, string correlationId, CancellationToken ct) =>
        Task.CompletedTask;

    public Task UpdateBotPermissionsAsync(ChatId chatId, TelegramBotPermissions permissions, string correlationId, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<string> CreateSettingsCallbackAsync(ChatId chatId, string key, string value, DateTimeOffset expiresAt, CancellationToken ct) =>
        Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task<SettingsCallbackState?> ConsumeSettingsCallbackAsync(string token, ChatId chatId, UserId actorUserId, CancellationToken ct) =>
        Task.FromResult<SettingsCallbackState?>(null);

    public Task EnqueueEffectAsync(IModerationEffect effect, string idempotencyKey, EffectImportance importance, CancellationToken ct) =>
        Task.CompletedTask;

    public Task EnqueueScheduledEffectAsync(IModerationEffect effect, DateTimeOffset executeAt, string idempotencyKey, EffectImportance importance, CancellationToken ct) =>
        Task.CompletedTask;

    public Task CancelEffectAsync(EffectId effectId, CancellationToken ct) => Task.CompletedTask;

    public Task<VerificationSession?> LoadVerificationAsync(VerificationSessionId sessionId, CancellationToken ct) =>
        Task.FromResult<VerificationSession?>(null);

    public Task<VerificationPersistenceResult> PersistVerificationAsync(
        VerificationSession session,
        VerificationStatus expectedStatus,
        IReadOnlyCollection<IDomainEvent> events,
        EffectPlan effectPlan,
        string correlationId,
        string causationId,
        CancellationToken ct) =>
        Task.FromResult(new VerificationPersistenceResult(true, false));

    public Task<IReadOnlyList<VerificationSession>> ListExpiredVerificationsAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VerificationSession>>([]);

    public Task EnqueueResponseAsync(ChatId chatId, string text, int? replyToMessageId, CancellationToken ct)
    {
        LastResponseText = text;
        LastResponseReplyToMessageId = replyToMessageId;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<StoredModerationEffect>> ClaimDueEffectsAsync(int limit, TimeSpan lease, CancellationToken ct) => Task.FromResult<IReadOnlyList<StoredModerationEffect>>([]);
    public Task MarkEffectAppliedAsync(StoredModerationEffect effect, CancellationToken ct) => Task.CompletedTask;
    public Task MarkEffectFailedAsync(StoredModerationEffect effect, string code, string message, bool retryable, TimeSpan? retryAfter, CancellationToken ct) => Task.CompletedTask;
    public Task MarkEffectUnknownAsync(StoredModerationEffect effect, string code, string message, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<StoredModerationEffect>> ListUnknownEffectsAsync(int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<StoredModerationEffect>>([]);
    public Task RequeueUnknownAsync(StoredModerationEffect effect, CancellationToken ct) => Task.CompletedTask;
    public Task ConfirmUnknownAppliedAsync(StoredModerationEffect effect, CancellationToken ct) => Task.CompletedTask;

    private static ChatState Chat(long chatId) => new()
    {
        Id = new ChatId(chatId),
        Type = ChatType.Supergroup,
        Title = "test",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static MemberState Member(long chatId, long userId, ChatMemberRole role) => new()
    {
        ChatId = new ChatId(chatId),
        UserId = new UserId(userId),
        DisplayName = $"user-{userId}",
        Roles = new HashSet<ChatMemberRole> { role },
        FirstSeenAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow,
    };
}
