using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class DomainContractSurfaceTests
{
    [Fact]
    public void DomainContractsExposeAndRoundTripAllState()
    {
        var chatId = new ChatId(-100);
        var userId = new UserId(20);
        var caseId = ModerationCaseId.New();
        var effectId = EffectId.New();
        var ruleId = new RuleId("rule");
        var appealId = AppealId.New();
        _ = chatId.ToString();
        _ = userId.ToString();
        _ = caseId.ToString();
        _ = effectId.ToString();
        _ = WarningId.New().ToString();
        _ = ruleId.Value;
        _ = new RoleId("custom").Value;
        _ = appealId.ToString();
        _ = new MessageThreadId(7).ToString();

        var settings = new ChatSettings
        {
            Language = "ru",
            TimeZone = "UTC",
            ManualModerationEnabled = true,
            AutoModerationEnabled = true,
            SilentModeration = true,
            RequireReasonForMute = true,
            RequireReasonForWarn = true,
            RequireReasonForBan = true,
            RequireReasonForKick = true,
            WarningLimit = 3,
            WarningLimitAction = ModerationAction.Ban,
            WarningLimitMuteDuration = TimeSpan.FromMinutes(10),
            ModerationEscalation = new ModerationEscalationPolicy
            {
                DeleteThreshold = 4,
                WarningThreshold = 7,
                MuteThreshold = 10,
                BanThreshold = 20,
                MuteDuration = TimeSpan.FromMinutes(10),
                BanDuration = TimeSpan.FromDays(1),
            },
            ModerationRules =
            [
                new ModerationRuleDefinition
                {
                    Id = ruleId,
                    Type = ModerationRuleType.ForbiddenWords,
                    IsEnabled = true,
                    Priority = 1,
                    ScoreOverride = 12,
                },
            ],
            CustomRoles =
            [
                new CustomRoleDefinition
                {
                    Id = new RoleId("custom"),
                    DisplayName = "Custom",
                    Rank = 50,
                    Permissions = new HashSet<Permission> { Permission.MembersWarn },
                },
            ],
            ModerationLogChatId = new ChatId(-200),
            DeleteServiceMessages = true,
            WelcomeEnabled = true,
            GoodbyeEnabled = true,
            WelcomeTemplate = "welcome {user}",
            GoodbyeTemplate = "goodbye {user}",
            RulesText = "rules",
            CaptchaPolicy = new CaptchaPolicy
            {
                Enabled = true,
                Timeout = TimeSpan.FromMinutes(5),
                MaximumAttempts = 3,
                FailureAction = CaptchaFailureAction.Ban,
                DeleteChallengeAfterCompletion = true,
            },
            FloodPolicy = new FloodPolicy { Window = TimeSpan.FromSeconds(10), MaximumMessages = 6 },
            LinkPolicy = new LinkPolicy { Mode = LinkPolicyMode.DenyAll, AllowedDomains = new HashSet<string> { "example.com" } },
            ForbiddenWordsPolicy = new ForbiddenWordsPolicy { Words = new HashSet<string> { "spam" }, CaseInsensitive = true },
            MentionSpamPolicy = new MentionSpamPolicy { Enabled = true, MaximumMentions = 2 },
            ForwardedMessagePolicy = new ForwardedMessagePolicy { Enabled = true, Score = 4 },
            MediaTypePolicy = new MediaTypePolicy { BlockedTypes = new HashSet<MessageContentType> { MessageContentType.Video } },
            NewMemberPolicy = new NewMemberPolicy { Enabled = true, Window = TimeSpan.FromMinutes(10), Score = 2 },
            CommandSpamPolicy = new CommandSpamPolicy { Enabled = true, Window = TimeSpan.FromMinutes(1), MaximumCommands = 3, Score = 4 },
        };
        _ = (settings.Language, settings.TimeZone, settings.ManualModerationEnabled,
            settings.AutoModerationEnabled, settings.SilentModeration,
            settings.RequireReasonForMute, settings.WarningLimit, settings.CaptchaPolicy,
            settings.ModerationLogChatId, settings.DeleteServiceMessages, settings.WelcomeEnabled,
            settings.GoodbyeEnabled, settings.WelcomeTemplate, settings.GoodbyeTemplate,
            settings.RulesText, settings.CaptchaEnabled, settings.DeleteCommandMessages,
            settings.ModerationLogThreadId, settings.MentionSpamPolicy, settings.ForwardedMessagePolicy,
            settings.MediaTypePolicy, settings.NewMemberPolicy, settings.CommandSpamPolicy,
            settings.ModerationEscalation, settings.ModerationRules, settings.CustomRoles);

        var botPermissions = new TelegramBotPermissions
        {
            CanDeleteMessages = true,
            CanRestrictMembers = true,
            CanInviteUsers = true,
            CanPinMessages = true,
            ObservedAt = DateTimeOffset.UtcNow,
        };
        _ = (botPermissions.CanDeleteMessages, botPermissions.CanRestrictMembers,
            botPermissions.CanInviteUsers, botPermissions.CanPinMessages, botPermissions.ObservedAt);

        var restriction = new RestrictionState { CanSendMessages = false, Until = DateTimeOffset.UtcNow };
        var member = new MemberState
        {
            ChatId = chatId,
            UserId = userId,
            Username = "user",
            DisplayName = "User",
            Status = MemberStatus.PendingVerification,
            Roles = new HashSet<ChatMemberRole> { ChatMemberRole.Member },
            CustomRoleIds = new HashSet<RoleId> { new("custom") },
            ExplicitPermissions = new HashSet<Permission> { Permission.MembersView },
            ActiveWarningCount = 2,
            DesiredRestriction = restriction,
            ObservedRestriction = restriction,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            JoinedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LeftAt = null,
            TrustLevel = TrustLevel.New,
        };
        _ = (member.ChatId, member.UserId, member.Username, member.DisplayName, member.Roles,
            member.Status, member.ExplicitPermissions, member.ActiveWarningCount, member.DesiredRestriction,
            member.ObservedRestriction, member.FirstSeenAt, member.LastSeenAt, member.JoinedAt, member.LeftAt,
            member.TrustLevel, member.CustomRoleIds);

        var chat = new ChatState
        {
            Id = chatId,
            Type = ChatType.Supergroup,
            Title = "Chat",
            IsEnabled = true,
            Settings = settings,
            ObservedBotPermissions = botPermissions,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _ = (chat.Id, chat.Type, chat.Title, chat.IsEnabled, chat.Settings,
            chat.ObservedBotPermissions, chat.CreatedAt, chat.UpdatedAt);

        var caseState = new ModerationCaseState
        {
            Id = caseId,
            ChatId = chatId,
            TargetUserId = userId,
            ActorUserId = new UserId(10),
            ActorType = ModerationActorType.Human,
            Action = ModerationAction.Mute,
            Reason = "reason",
            SourceMessageId = 123,
            SourceRuleId = ruleId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = restriction.Until,
            Status = ModerationCaseStatus.Requested,
            CorrelationId = "correlation",
        };
        _ = (caseState.Id, caseState.ChatId, caseState.TargetUserId, caseState.ActorUserId,
            caseState.ActorType, caseState.Action, caseState.Reason, caseState.CreatedAt,
            caseState.SourceMessageId, caseState.SourceRuleId, caseState.ExpiresAt, caseState.Status, caseState.CorrelationId);

        var appeal = new AppealState
        {
            Id = appealId,
            CaseId = caseId,
            AuthorUserId = userId,
            Text = "appeal",
            Status = AppealStatus.Reviewing,
            ResolvedBy = new UserId(10),
            ResolutionComment = "reviewed",
            CreatedAt = DateTimeOffset.UtcNow,
            ResolvedAt = DateTimeOffset.UtcNow,
        };
        _ = (appeal.Id, appeal.CaseId, appeal.AuthorUserId, appeal.Text, appeal.Status,
            appeal.ResolvedBy, appeal.ResolutionComment, appeal.CreatedAt, appeal.ResolvedAt);

        var restrict = new RestrictMemberEffect(chatId, userId, restriction.Until!.Value, caseId, "corr", "cause");
        var unrestrict = new UnrestrictMemberEffect(chatId, userId, caseId, restriction.Until.Value, "corr", "cause");
        var keyboard = new InlineKeyboardSpec([[new InlineKeyboardButtonSpec("yes", "captcha:one:yes")]]);
        var message = new SendMessageEffect(chatId, "text", 123, MessageParseMode.Html, keyboard);
        var answerCallback = new AnswerCallbackQueryEffect("callback", "done", true);
        var moderationLog = new SendModerationLogEffect(new ChatId(-200), "log", "corr", "cause");
        var markCaseRevoked = new MarkModerationCaseRevokedEffect(caseId, "corr", "cause");
        var ban = new BanMemberEffect(chatId, userId, restriction.Until, caseId, "corr", "cause");
        var unban = new UnbanMemberEffect(chatId, userId, caseId, restriction.Until, "corr", "cause");
        var kick = new KickMemberEffect(chatId, userId, caseId, "corr", "cause");
        var delete = new DeleteMessageEffect(chatId, 456, caseId, "corr", "cause", userId);
        var deleteMany = new DeleteMessagesEffect(chatId, [456, 457], caseId, "corr", "cause");
        var edit = new EditMessageEffect(chatId, 456, "edited", MessageParseMode.Html, keyboard, "corr", "cause");
        var pin = new PinMessageEffect(chatId, 456, true, "corr", "cause");
        var unpin = new UnpinMessageEffect(chatId, 456, "corr", "cause");
        var getMember = new GetChatMemberEffect(chatId, userId, "member-observation", "corr", "cause");
        var getAdmins = new GetChatAdministratorsEffect(chatId, "admin-observation", "corr", "cause");
        var getPermissions = new GetBotPermissionsEffect(chatId, userId, "permission-observation", "corr", "cause");
        var scheduleEnvelope = new EffectEnvelope
        {
            Id = effectId,
            EffectType = EffectTypeCatalog.SendMessage,
            Payload = message,
            CorrelationId = "corr",
            CausationId = "cause",
            IdempotencyKey = "schedule-inner",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var schedule = new ScheduleEffect(DateTimeOffset.UtcNow.AddMinutes(1), scheduleEnvelope, "corr", "cause");
        var cancelSchedule = new CancelScheduledEffect(effectId, "corr", "cause");
        var persistAggregate = new PersistAggregateEffect("member", "member-1", "{}", "corr", "cause");
        var appendEvents = new AppendDomainEventsEffect("member-1", [new DomainEventPayload("member.observed", "{}")], "corr", "cause");
        var createCase = new CreateModerationCaseEffect(caseState, "corr", "cause");
        var updateCase = new UpdateModerationCaseEffect(caseId, ModerationCaseStatus.Applying, "applying", "corr", "cause");
        var auditPayload = new AuditEventPayload(chatId, new UserId(10), userId, "mute", "corr", caseId, new Dictionary<string, object?>(), DateTimeOffset.UtcNow);
        var writeAudit = new WriteAuditEventEffect(auditPayload, "corr", "cause");
        var trace = new EmitTraceEventEffect("moderation.effect", new Dictionary<string, string> { ["action"] = "mute" }, "corr", "cause");
        var structuredLog = new WriteStructuredLogEffect("moderation.effect", "Information", new Dictionary<string, object?>(), "corr", "cause");
        var notify = new NotifyAdministratorsEffect(chatId, "permission required", "corr", "cause");
        var warning = new WarningState
        {
            Id = WarningId.New(),
            ChatId = chatId,
            TargetUserId = userId,
            ActorUserId = new UserId(10),
            Reason = "reason",
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };
        var verification = new VerificationSession
        {
            Id = VerificationSessionId.New(),
            ChatId = chatId,
            UserId = userId,
            Status = VerificationStatus.Pending,
            ChallengeType = "buttons",
            CorrectAnswer = "yes",
            Options = ["yes", "no"],
            Attempts = 1,
            MaximumAttempts = 3,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            ChallengeMessageId = 456,
        };
        var saveVerification = new SaveVerificationSessionEffect(verification, "corr", "cause");
        _ = (restrict.ChatId, restrict.UserId, restrict.Until, restrict.CaseId, restrict.CorrelationId, restrict.CausationId);
        _ = (unrestrict.ChatId, unrestrict.UserId, unrestrict.CaseId, unrestrict.ExpectedUntil,
            unrestrict.CorrelationId, unrestrict.CausationId);
        _ = (message.ChatId, message.Text, message.ReplyToMessageId, message.ParseMode, message.InlineKeyboard);
        _ = (keyboard.Rows, keyboard.Rows[0][0].Text, keyboard.Rows[0][0].CallbackData);
        _ = (answerCallback.CallbackQueryId, answerCallback.Text, answerCallback.ShowAlert);
        _ = (moderationLog.ChatId, moderationLog.Text, moderationLog.CorrelationId, moderationLog.CausationId);
        _ = (markCaseRevoked.CaseId, markCaseRevoked.CorrelationId, markCaseRevoked.CausationId);
        _ = (ban.ChatId, ban.UserId, ban.Until, ban.CaseId, ban.CorrelationId, ban.CausationId);
        _ = (unban.ChatId, unban.UserId, unban.CaseId, unban.ExpectedUntil, unban.CorrelationId, unban.CausationId);
        _ = (kick.ChatId, kick.UserId, kick.CaseId, kick.CorrelationId, kick.CausationId);
        _ = (delete.ChatId, delete.MessageId, delete.CaseId, delete.CorrelationId, delete.CausationId, delete.TargetUserId);
        _ = (deleteMany.ChatId, deleteMany.MessageIds, deleteMany.CaseId, deleteMany.CorrelationId, deleteMany.CausationId);
        _ = (edit.ChatId, edit.MessageId, edit.Text, edit.ParseMode, edit.InlineKeyboard, edit.CorrelationId, edit.CausationId,
            pin.ChatId, pin.MessageId, pin.DisableNotification, pin.CorrelationId, pin.CausationId,
            unpin.ChatId, unpin.MessageId, unpin.CorrelationId, unpin.CausationId,
            getMember.ChatId, getMember.UserId, getMember.ObservationKey, getMember.CorrelationId, getMember.CausationId,
            getAdmins.ChatId, getAdmins.ObservationKey, getAdmins.CorrelationId, getAdmins.CausationId,
            getPermissions.ChatId, getPermissions.BotUserId, getPermissions.ObservationKey,
            getPermissions.CorrelationId, getPermissions.CausationId,
            schedule.ExecuteAt, schedule.Effect, schedule.CorrelationId, schedule.CausationId,
            cancelSchedule.ScheduledEffectId, cancelSchedule.CorrelationId, cancelSchedule.CausationId,
            persistAggregate.AggregateType, persistAggregate.AggregateId, persistAggregate.StateJson,
            persistAggregate.CorrelationId, persistAggregate.CausationId,
            appendEvents.AggregateId, appendEvents.Events, appendEvents.CorrelationId, appendEvents.CausationId,
            createCase.Case, createCase.CorrelationId, createCase.CausationId,
            updateCase.CaseId, updateCase.Status, updateCase.Reason, updateCase.CorrelationId, updateCase.CausationId,
            saveVerification.Session, saveVerification.CorrelationId, saveVerification.CausationId,
            writeAudit.Event, writeAudit.CorrelationId, writeAudit.CausationId,
            auditPayload.ChatId, auditPayload.ActorUserId, auditPayload.TargetUserId, auditPayload.Action,
            auditPayload.CorrelationId, auditPayload.CaseId, auditPayload.Metadata, auditPayload.CreatedAt,
            trace.Name, trace.Attributes, trace.CorrelationId, trace.CausationId,
            structuredLog.EventName, structuredLog.Level, structuredLog.Properties,
            structuredLog.CorrelationId, structuredLog.CausationId,
            notify.ChatId, notify.Text, notify.CorrelationId, notify.CausationId);
        _ = (warning.Id, warning.ChatId, warning.TargetUserId, warning.ActorUserId, warning.Reason,
            warning.CreatedAt, warning.ExpiresAt, warning.IsActive);
        _ = (verification.Id, verification.ChatId, verification.UserId, verification.Status,
            verification.ChallengeType, verification.CorrectAnswer, verification.Options,
            verification.Attempts, verification.MaximumAttempts, verification.CreatedAt,
            verification.ExpiresAt, verification.ChallengeMessageId);

        var planned = new PlannedEffect(message, EffectImportance.FollowUp, [effectId], effectId, effectId);
        var plan = new EffectPlan([planned]);
        _ = (planned.Effect, planned.Importance, planned.DependsOn, planned.CompensationEffectId, planned.Id);
        _ = plan.Effects;

        var envelope = new EffectEnvelope
        {
            Id = effectId,
            EffectType = "telegram.send_message",
            Payload = message,
            CorrelationId = "corr",
            CausationId = "cause",
            IdempotencyKey = "idempotency",
            Status = EffectExecutionStatus.Pending,
            Attempt = 1,
            MaximumAttempts = 8,
            CreatedAt = DateTimeOffset.UtcNow,
            NotBefore = DateTimeOffset.UtcNow,
            Dependencies = [effectId],
        };
        _ = (envelope.Id, envelope.EffectType, envelope.Payload, envelope.CorrelationId,
            envelope.CausationId, envelope.IdempotencyKey, envelope.Status, envelope.Attempt,
            envelope.MaximumAttempts, envelope.CreatedAt, envelope.NotBefore, envelope.Dependencies);

        var created = new ModerationCaseCreated(caseState);
        var changed = new RestrictionDesiredStateChanged(chatId, userId, restriction, caseId);
        var warningIssued = new WarningIssued(warning);
        var warningRevoked = new WarningRevoked(chatId, userId, warning.Id, WarningRevocationReason.Manual);
        var roleAssigned = new MemberRoleAssigned(chatId, userId, ChatMemberRole.Moderator);
        var roleRemoved = new MemberRoleRemoved(chatId, userId, ChatMemberRole.Moderator);
        var customRoleAssigned = new CustomRoleAssigned(chatId, userId, new RoleId("custom"));
        var customRoleRemoved = new CustomRoleRemoved(chatId, userId, new RoleId("custom"));
        var settingsUpdated = new ChatSettingsUpdated(chatId, settings, DateTimeOffset.UtcNow);
        var chatRegistered = new ChatRegistered(chat);
        var metadataUpdated = new ChatMetadataUpdated(chatId, ChatType.Supergroup, "Chat", DateTimeOffset.UtcNow);
        var warningLimitReached = new WarningLimitReached(chatId, userId, 3, ModerationAction.Mute);
        var caseRevocationRequested = new ModerationCaseRevocationRequested(caseState);
        var verificationStarted = new VerificationStarted(verification);
        var verificationPassed = new VerificationPassed(verification);
        var verificationFailed = new VerificationFailed(verification, true);
        var verificationExpired = new VerificationExpired(verification);
        var appealOpened = new AppealOpened(appeal);
        var appealApproved = new AppealApproved(appeal);
        var appealRejected = new AppealRejected(appeal);
        var memberJoined = new MemberJoined(member, DateTimeOffset.UtcNow);
        var memberLeft = new MemberLeft(chatId, userId, member.DisplayName, DateTimeOffset.UtcNow);
        var permissionsObserved = new BotPermissionsObserved(chatId, botPermissions, DateTimeOffset.UtcNow);
        var observedChanged = new RestrictionObservedStateChanged(chatId, userId, restriction);
        var desiredRestriction = new DesiredMemberRestriction(chatId, userId, restriction, restriction);
        var metric = new EmitMetricEffect(
            "moderation_cases_created_total",
            new Dictionary<string, string> { ["action"] = "mute" },
            "corr",
            "cause");
        var normalizedMessage = new NormalizedMessage
        {
            ChatId = chatId,
            MessageId = 123,
            AuthorId = userId,
            Text = "text",
            Entities = [new MessageEntity(MessageEntityType.Url, 0, 4, "https://example.com")],
            ContentType = MessageContentType.Text,
            IsForwarded = true,
            IsServiceMessage = false,
            SentAt = DateTimeOffset.UtcNow,
        };
        var history = new ModerationHistorySummary { RecentMessageHashes = ["hash"], ViolationsInWindow = 1 };
        var rateLimits = new RateLimitSnapshot { MessagesInWindow = 2, LinksInWindow = 1, CommandsInWindow = 1 };
        var messageContext = new ModerationMessageContext
        {
            Chat = chat,
            Author = member,
            Message = normalizedMessage,
            History = history,
            RateLimits = rateLimits,
        };
        var violation = new Violation
        {
            RuleId = ruleId,
            Code = "rule",
            Score = 1,
            Severity = ViolationSeverity.Low,
            Reason = "reason",
            Metadata = new Dictionary<string, object?> { ["key"] = "value" },
        };
        var violationDetected = new ViolationDetected(chatId, userId, 123, violation);
        var automatedDecision = AutomatedModerationDecision.Ignore("ignored");
        _ = (normalizedMessage.ChatId, normalizedMessage.MessageId, normalizedMessage.AuthorId, normalizedMessage.Text,
            normalizedMessage.Entities, normalizedMessage.ContentType, normalizedMessage.IsForwarded,
            normalizedMessage.IsServiceMessage, normalizedMessage.SentAt, history.RecentMessageHashes,
            history.ViolationsInWindow, rateLimits.MessagesInWindow, rateLimits.LinksInWindow,
            rateLimits.CommandsInWindow, messageContext.Chat, messageContext.Author, messageContext.Message,
            messageContext.History, messageContext.RateLimits, violation.RuleId, violation.Code, violation.Score,
            violation.Severity, violation.Reason, violation.Metadata, automatedDecision.Accepted,
            automatedDecision.ErrorCode, automatedDecision.Violations, automatedDecision.Case,
            automatedDecision.Events, automatedDecision.EffectPlan);
        var entity = normalizedMessage.Entities[0];
        _ = (entity.Type, entity.Offset, entity.Length, entity.Url);
        _ = (created.Case, created.EventType, changed.ChatId, changed.UserId, changed.State, changed.CaseId, changed.EventType,
            warningIssued.Warning, warningIssued.EventType, warningRevoked.ChatId, warningRevoked.TargetUserId,
            warningRevoked.WarningId, warningRevoked.Reason, warningRevoked.EventType,
            roleAssigned.ChatId, roleAssigned.UserId, roleAssigned.Role, roleAssigned.EventType,
            roleRemoved.ChatId, roleRemoved.UserId, roleRemoved.Role, roleRemoved.EventType,
            customRoleAssigned.ChatId, customRoleAssigned.UserId, customRoleAssigned.RoleId, customRoleAssigned.EventType,
            customRoleRemoved.ChatId, customRoleRemoved.UserId, customRoleRemoved.RoleId, customRoleRemoved.EventType,
            settingsUpdated.ChatId, settingsUpdated.Settings, settingsUpdated.UpdatedAt, settingsUpdated.EventType,
            chatRegistered.Chat, chatRegistered.EventType, metadataUpdated.ChatId, metadataUpdated.Type,
            metadataUpdated.Title, metadataUpdated.OccurredAt, metadataUpdated.EventType,
            warningLimitReached.ChatId, warningLimitReached.UserId, warningLimitReached.ActiveWarningCount,
            warningLimitReached.Action, warningLimitReached.EventType,
            caseRevocationRequested.Case, caseRevocationRequested.EventType,
            violationDetected.ChatId, violationDetected.UserId,
            violationDetected.MessageId, violationDetected.Violation, violationDetected.EventType);
        _ = (verificationStarted.Session, verificationStarted.EventType, verificationPassed.Session,
            verificationPassed.EventType, verificationFailed.Session, verificationFailed.IsFinal,
            verificationFailed.EventType, verificationExpired.Session, verificationExpired.EventType);
        _ = (appealOpened.Appeal, appealOpened.EventType, appealApproved.Appeal,
            appealApproved.EventType, appealRejected.Appeal, appealRejected.EventType);
        _ = (memberJoined.Member, memberJoined.OccurredAt, memberJoined.EventType,
            memberLeft.ChatId, memberLeft.UserId, memberLeft.DisplayName, memberLeft.OccurredAt,
            memberLeft.EventType, permissionsObserved.ChatId, permissionsObserved.Permissions,
            permissionsObserved.OccurredAt, permissionsObserved.EventType,
            metric.Name, metric.Labels, metric.CorrelationId, metric.CausationId);
        _ = (observedChanged.ChatId, observedChanged.UserId, observedChanged.State, observedChanged.EventType,
            desiredRestriction.ChatId, desiredRestriction.UserId, desiredRestriction.State, desiredRestriction.ObservedState);
    }
}
