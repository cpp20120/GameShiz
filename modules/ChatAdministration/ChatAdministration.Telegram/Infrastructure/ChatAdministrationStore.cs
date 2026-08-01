using System.Text.Json;
using System.Text.Json.Serialization;
using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Persistence.Connections;
using Dapper;
using Microsoft.Extensions.Options;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class ChatAdministrationStore(
    INpgsqlConnectionFactory connections,
    IOptions<BotFrameworkOptions>? botOptions = null) : IChatAdministrationStore
{
    private const string RestrictEffectType = EffectTypeCatalog.RestrictMember;
    private const string UnrestrictEffectType = EffectTypeCatalog.UnrestrictMember;
    private const string BanEffectType = EffectTypeCatalog.BanMember;
    private const string UnbanEffectType = EffectTypeCatalog.UnbanMember;
    private const string KickEffectType = EffectTypeCatalog.KickMember;
    private const string DeleteEffectType = EffectTypeCatalog.DeleteMessage;
    private const string DeleteMessagesEffectType = EffectTypeCatalog.DeleteMessages;
    private const string AnswerCallbackEffectType = EffectTypeCatalog.AnswerCallbackQuery;
    private const string SendMessageEffectType = EffectTypeCatalog.SendMessage;
    private const string ModerationLogEffectType = EffectTypeCatalog.SendModerationLog;
    private const string MarkCaseRevokedEffectType = EffectTypeCatalog.MarkCaseRevoked;
    private const string EmitMetricEffectType = EffectTypeCatalog.EmitMetric;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new ReadOnlySetJsonConverterFactory(), new JsonStringEnumConverter() },
    };
    private readonly SemaphoreSlim schemaGate = new(1, 1);
    private volatile bool schemaReady;
    private readonly string tenantKey = NormalizeTenantKey(botOptions?.Value.TenantKey);

    public async Task UpsertChatMetadataAsync(ChatMetadataCommand command, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<ChatMetadataRow>(new CommandDefinition(
            """
            SELECT chat_type AS "ChatType", title AS "Title", is_enabled AS "IsEnabled",
                   settings::text AS "SettingsJson", bot_permissions::text AS "BotPermissionsJson",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM chat_admin_chats WHERE chat_id = @chatId
            """,
            new { chatId = command.ChatId.Value }, tx, cancellationToken: ct));

        if (row is null)
        {
            var chat = new ChatState
            {
                Id = command.ChatId,
                Type = command.Type,
                Title = command.Title,
                IsEnabled = true,
                CreatedAt = command.CreatedAt,
                UpdatedAt = command.CreatedAt,
            };
            await conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO chat_admin_chats
                    (chat_id, chat_type, title, is_enabled, settings, created_at, updated_at)
                VALUES (@chatId, @chatType, @title, true, '{}'::jsonb, @createdAt, @createdAt)
                """,
                new
                {
                    chatId = command.ChatId.Value,
                    chatType = ToDb(command.Type),
                    command.Title,
                    command.CreatedAt,
                }, tx, cancellationToken: ct));
            await AppendStandaloneDomainEventAsync(conn, tx, new ChatRegistered(chat), command.CreatedAt, ct);
        }
        else if (!string.Equals(row.ChatType, ToDb(command.Type), StringComparison.Ordinal)
            || !string.Equals(row.Title, command.Title, StringComparison.Ordinal))
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE chat_admin_chats
                SET chat_type = @chatType, title = @title, updated_at = @updatedAt
                WHERE chat_id = @chatId
                """,
                new
                {
                    chatId = command.ChatId.Value,
                    chatType = ToDb(command.Type),
                    command.Title,
                    updatedAt = command.CreatedAt,
                }, tx, cancellationToken: ct));
            await AppendStandaloneDomainEventAsync(
                conn,
                tx,
                new ChatMetadataUpdated(command.ChatId, command.Type, command.Title, command.CreatedAt),
                command.CreatedAt,
                ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<ResolvedTarget?> FindMemberByUsernameAsync(ChatId chatId, string username, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<MemberTargetRow>(new CommandDefinition(
            """
            SELECT user_id AS "UserId", username AS "Username", display_name AS "DisplayName"
            FROM chat_admin_members
            WHERE chat_id = @chatId AND lower(username) = lower(@username)
            ORDER BY last_seen_at DESC
            LIMIT 1
            """,
            new { chatId = chatId.Value, username = username.TrimStart('@') }, cancellationToken: ct));
        return row is null
            ? null
            : new ResolvedTarget(new UserId(row.UserId), row.Username, row.DisplayName);
    }

    public async Task<ResolvedTarget?> FindMessageAuthorAsync(ChatId chatId, int messageId, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<MemberTargetRow>(new CommandDefinition(
            """
            SELECT i.author_user_id AS "UserId",
                   m.username AS "Username",
                   COALESCE(m.display_name, 'User ' || i.author_user_id::text) AS "DisplayName"
            FROM chat_admin_message_index i
            LEFT JOIN chat_admin_members m
              ON m.chat_id = i.chat_id AND m.user_id = i.author_user_id
            WHERE i.chat_id = @chatId AND i.message_id = @messageId
            LIMIT 1
            """,
            new { chatId = chatId.Value, messageId }, cancellationToken: ct));
        return row is null
            ? null
            : new ResolvedTarget(new UserId(row.UserId), row.Username, row.DisplayName);
    }

    public async Task<ModerationContext> LoadContextAsync(
        ChatId chatId,
        UserId actorUserId,
        UserId targetUserId,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);

        var chatRow = await conn.QuerySingleOrDefaultAsync<ChatRow>(new CommandDefinition(
            """
            SELECT chat_id AS "ChatId", chat_type AS "ChatType", title AS "Title",
                   is_enabled AS "IsEnabled", settings::text AS "SettingsJson",
                   bot_permissions::text AS "BotPermissionsJson",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM chat_admin_chats WHERE chat_id = @chatId
            """,
            new { chatId = chatId.Value }, cancellationToken: ct));

        var chat = chatRow is null
            ? NewChat(chatId)
            : new ChatState
            {
                Id = chatId,
                Type = ParseChatType(chatRow.ChatType),
                Title = chatRow.Title,
                IsEnabled = chatRow.IsEnabled,
                Settings = Deserialize(chatRow.SettingsJson, new ChatSettings()),
                ObservedBotPermissions = Deserialize(chatRow.BotPermissionsJson, new TelegramBotPermissions()),
                CreatedAt = chatRow.CreatedAt,
                UpdatedAt = chatRow.UpdatedAt,
            };

        var rows = (await conn.QueryAsync<MemberRow>(new CommandDefinition(
            """
            SELECT chat_id AS "ChatId", user_id AS "UserId", username AS "Username",
                   display_name AS "DisplayName", status AS "Status", roles::text AS "RolesJson",
                   custom_roles::text AS "CustomRolesJson",
                   explicit_permissions::text AS "ExplicitPermissionsJson", trust_level AS "TrustLevel",
                   desired_restriction::text AS "DesiredRestrictionJson",
                   observed_restriction::text AS "ObservedRestrictionJson",
                   first_seen_at AS "FirstSeenAt", last_seen_at AS "LastSeenAt",
                   joined_at AS "JoinedAt", left_at AS "LeftAt"
            FROM chat_admin_members
            WHERE chat_id = @chatId AND user_id = ANY(@userIds)
            """,
            new { chatId = chatId.Value, userIds = new[] { actorUserId.Value, targetUserId.Value } },
            cancellationToken: ct))).ToDictionary(row => row.UserId);
        var warningCounts = (await conn.QueryAsync<WarningCountRow>(new CommandDefinition(
            """
            SELECT target_user_id AS "UserId", COUNT(*)::int AS "ActiveWarningCount"
            FROM chat_admin_warnings
            WHERE chat_id = @chatId AND target_user_id = ANY(@userIds)
              AND is_active AND (expires_at IS NULL OR expires_at > now())
            GROUP BY target_user_id
            """,
            new { chatId = chatId.Value, userIds = new[] { actorUserId.Value, targetUserId.Value } },
            cancellationToken: ct))).ToDictionary(row => row.UserId);

        var actor = MergeMember(
            rows.GetValueOrDefault(actorUserId.Value),
            warningCounts.GetValueOrDefault(actorUserId.Value)?.ActiveWarningCount ?? 0,
            chatId,
            actorUserId,
            actorObservedRole,
            actorDisplayName);
        var target = MergeMember(
            rows.GetValueOrDefault(targetUserId.Value),
            warningCounts.GetValueOrDefault(targetUserId.Value)?.ActiveWarningCount ?? 0,
            chatId,
            targetUserId,
            targetObservedRole,
            targetDisplayName);
        return new ModerationContext(chat, actor, target);
    }

    public async Task RecordMessageAsync(MessageIndexEntry entry, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_members
                (chat_id, user_id, username, display_name, status, first_seen_at, last_seen_at)
            VALUES (@chatId, @userId, @username, @displayName, 'active', @seenAt, @seenAt)
            ON CONFLICT (chat_id, user_id) DO UPDATE SET
                username = COALESCE(EXCLUDED.username, chat_admin_members.username),
                display_name = EXCLUDED.display_name,
                status = 'active',
                last_seen_at = GREATEST(chat_admin_members.last_seen_at, EXCLUDED.last_seen_at)
            """,
            new
            {
                chatId = entry.ChatId.Value,
                userId = entry.AuthorUserId.Value,
                username = string.IsNullOrWhiteSpace(entry.AuthorUsername) ? null : entry.AuthorUsername.TrimStart('@'),
                displayName = entry.AuthorDisplayName,
                seenAt = entry.SentAt,
            }, tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_message_index
                (chat_id, message_id, author_user_id, content_type, has_links, sent_at, content_hash)
            VALUES (@chatId, @messageId, @authorUserId, @contentType, @hasLinks, @sentAt, @contentHash)
            ON CONFLICT (chat_id, message_id) DO UPDATE SET
                author_user_id = EXCLUDED.author_user_id, content_type = EXCLUDED.content_type,
                has_links = EXCLUDED.has_links, sent_at = EXCLUDED.sent_at,
                content_hash = EXCLUDED.content_hash
            """,
            new
            {
                chatId = entry.ChatId.Value,
                messageId = entry.MessageId,
                authorUserId = entry.AuthorUserId.Value,
                contentType = ToDb(entry.ContentType),
                entry.HasLinks,
                sentAt = entry.SentAt,
                entry.ContentHash,
            }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<int>> ListMessageIdsAsync(
        ChatId chatId,
        UserId? targetUserId,
        int limit,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<int>(new CommandDefinition(
            """
            SELECT message_id
            FROM chat_admin_message_index
            WHERE chat_id = @chatId AND (@targetUserId IS NULL OR author_user_id = @targetUserId)
            ORDER BY sent_at DESC, message_id DESC
            LIMIT @limit
            """,
            new { chatId = chatId.Value, targetUserId = targetUserId?.Value, limit = Math.Clamp(limit, 1, 1000) },
            cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<PersistCommandResult> PersistPurgeAsync(
        PurgeMessagesCommand command,
        DeleteMessagesEffect effect,
        int messageCount,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var inserted = await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_commands (command_id, idempotency_key, response_text, created_at)
            VALUES (@commandId, @idempotencyKey, @responseText, @createdAt)
            ON CONFLICT (command_id) DO NOTHING
            """,
            new
            {
                command.CommandId,
                command.IdempotencyKey,
                responseText = $"Purge: {messageCount}",
                command.CreatedAt,
            }, tx, cancellationToken: ct));
        if (inserted == 0)
        {
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, null);
        }

        var envelope = new EffectEnvelope
        {
            Id = EffectId.New(),
            EffectType = DeleteMessagesEffectType,
            Payload = effect,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            IdempotencyKey = $"purge-effect:{command.CommandId}",
            CreatedAt = command.CreatedAt,
        };
        await InsertEffectAsync(conn, tx, envelope, EffectImportance.Required, null, ct);
        await InsertAuditAsync(
            conn,
            tx,
            command.ChatId,
            command.ActorUserId,
            command.TargetUserId,
            command.CorrelationId,
            null,
            "messages.purge.requested",
            new { messageCount, effect.MessageIds },
            command.CreatedAt,
            ct);
        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, null);
    }

    public async Task<VerificationSession?> LoadVerificationAsync(VerificationSessionId sessionId, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<VerificationRow>(new CommandDefinition(
            """
            SELECT session_id AS "SessionId", chat_id AS "ChatId", user_id AS "UserId",
                   status AS "Status", challenge_type AS "ChallengeType", correct_answer AS "CorrectAnswer",
                   options::text AS "OptionsJson", attempts AS "Attempts", maximum_attempts AS "MaximumAttempts",
                   created_at AS "CreatedAt", expires_at AS "ExpiresAt", challenge_message_id AS "ChallengeMessageId"
            FROM chat_admin_verifications WHERE session_id = @sessionId
            """,
            new { sessionId = sessionId.Value }, cancellationToken: ct));
        return row is null ? null : ReadVerification(row);
    }

    public async Task<VerificationPersistenceResult> PersistVerificationAsync(
        VerificationSession session,
        VerificationStatus expectedStatus,
        IReadOnlyCollection<IDomainEvent> events,
        EffectPlan effectPlan,
        string correlationId,
        string causationId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_verifications
                (session_id, chat_id, user_id, status, challenge_type, correct_answer, options,
                 attempts, maximum_attempts, created_at, expires_at, challenge_message_id, updated_at)
            VALUES (@sessionId, @chatId, @userId, @status, @challengeType, @correctAnswer, CAST(@options AS jsonb),
                    @attempts, @maximumAttempts, @createdAt, @expiresAt, @challengeMessageId, now())
            ON CONFLICT (session_id) DO UPDATE SET
                status = EXCLUDED.status, attempts = EXCLUDED.attempts,
                challenge_message_id = EXCLUDED.challenge_message_id, updated_at = now()
            WHERE chat_admin_verifications.status = @expectedStatus
            """,
            new
            {
                sessionId = session.Id.Value,
                chatId = session.ChatId.Value,
                userId = session.UserId.Value,
                status = ToDb(session.Status),
                expectedStatus = ToDb(expectedStatus),
                challengeType = session.ChallengeType,
                correctAnswer = session.CorrectAnswer,
                options = JsonSerializer.Serialize(session.Options, JsonOptions),
                session.Attempts,
                session.MaximumAttempts,
                session.CreatedAt,
                session.ExpiresAt,
                session.ChallengeMessageId,
            }, tx, cancellationToken: ct));
        if (affected == 0)
        {
            await tx.CommitAsync(ct);
            return new VerificationPersistenceResult(false, true);
        }

        foreach (var domainEvent in events)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
                new
                {
                    aggregateId = session.Id.Value,
                    eventType = domainEvent.EventType,
                    payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    occurredAt = DateTimeOffset.UtcNow,
                }, tx, cancellationToken: ct));
        }
        foreach (var planned in effectPlan.Effects)
        {
            var envelope = CreateAuxiliaryEnvelope(planned, session, correlationId, causationId);
            await InsertEffectAsync(conn, tx, envelope, planned.Importance, null, ct);
        }
        if (effectPlan.Effects.OfType<PlannedEffect>().Any(planned => planned.Effect is RestrictMemberEffect))
            await conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO chat_admin_members (chat_id, user_id, display_name, desired_restriction, first_seen_at, last_seen_at)
                VALUES (@chatId, @userId, 'member', CAST(@restriction AS jsonb), now(), now())
                ON CONFLICT (chat_id, user_id) DO UPDATE SET desired_restriction = EXCLUDED.desired_restriction, last_seen_at = now()
                """,
                new
                {
                    chatId = session.ChatId.Value,
                    userId = session.UserId.Value,
                    restriction = JsonSerializer.Serialize(new RestrictionState { CanSendMessages = false, Until = session.ExpiresAt }, JsonOptions),
                }, tx, cancellationToken: ct));
        await InsertAuditAsync(conn, tx, session.ChatId, null, session.UserId, correlationId, null,
            $"verification.{ToDb(session.Status)}", new { session.Id, session.Attempts }, DateTimeOffset.UtcNow, ct);
        await tx.CommitAsync(ct);
        return new VerificationPersistenceResult(true, false);
    }

    public async Task<IReadOnlyList<VerificationSession>> ListExpiredVerificationsAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<VerificationRow>(new CommandDefinition(
            """
            SELECT session_id AS "SessionId", chat_id AS "ChatId", user_id AS "UserId", status AS "Status",
                   challenge_type AS "ChallengeType", correct_answer AS "CorrectAnswer", options::text AS "OptionsJson",
                   attempts AS "Attempts", maximum_attempts AS "MaximumAttempts", created_at AS "CreatedAt",
                   expires_at AS "ExpiresAt", challenge_message_id AS "ChallengeMessageId"
            FROM chat_admin_verifications
            WHERE status = 'pending' AND expires_at <= @now
            ORDER BY expires_at, session_id
            LIMIT @limit
            """,
            new { now, limit = Math.Clamp(limit, 1, 100) }, cancellationToken: ct));
        return rows.Select(ReadVerification).ToArray();
    }

    public async Task<PersistCommandResult> PersistAsync(PersistModerationCommand command, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var inserted = await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_commands (command_id, idempotency_key, response_text, created_at)
            VALUES (@commandId, @idempotencyKey, @responseText, @createdAt)
            ON CONFLICT (command_id) DO NOTHING
            """,
            new
            {
                commandId = command.CommandId,
                command.IdempotencyKey,
                command.ResponseText,
                command.CreatedAt,
            }, tx, cancellationToken: ct));

        if (inserted == 0)
        {
            var existing = await conn.QuerySingleAsync<Guid?>(new CommandDefinition(
                "SELECT case_id FROM chat_admin_commands WHERE command_id = @commandId",
                new { commandId = command.CommandId }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, existing is null ? null : new ModerationCaseId(existing.Value));
        }

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_chats (chat_id, chat_type, title, is_enabled, settings, created_at, updated_at)
            VALUES (@chatId, 'supergroup', @title, true, '{}'::jsonb, @createdAt, @createdAt)
            ON CONFLICT (chat_id) DO UPDATE SET updated_at = EXCLUDED.updated_at
            """,
            new { chatId = command.Case.ChatId.Value, title = "Telegram chat", command.CreatedAt }, tx, cancellationToken: ct));

        await UpsertMemberAsync(conn, tx, command.Actor, command.CreatedAt, ct);
        await UpsertMemberAsync(conn, tx, command.Target, command.CreatedAt, ct);
        if (command.Warning is not null)
            await InsertWarningAsync(conn, tx, command.Warning, ct);

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_cases
                (case_id, chat_id, target_user_id, actor_user_id, actor_type, action, reason,
                 source_message_id, source_rule_id, created_at, expires_at, status, correlation_id)
            VALUES (@caseId, @chatId, @targetUserId, @actorUserId, @actorType, @action, @reason,
                    @sourceMessageId, @sourceRuleId,
                    @createdAt, @expiresAt, 'requested', @correlationId)
            """,
            new
            {
                caseId = command.Case.Id.Value,
                chatId = command.Case.ChatId.Value,
                targetUserId = command.Case.TargetUserId.Value,
                actorUserId = command.Case.ActorUserId?.Value,
                actorType = ToDb(command.Case.ActorType),
                action = ToDb(command.Case.Action),
                reason = command.Case.Reason,
                sourceMessageId = command.Case.SourceMessageId,
                sourceRuleId = command.Case.SourceRuleId?.Value,
                createdAt = command.Case.CreatedAt,
                expiresAt = command.Case.ExpiresAt,
                correlationId = command.Case.CorrelationId,
            }, tx, cancellationToken: ct));

        await AppendCaseHistoryAsync(conn, tx, command.Case.Id, ModerationCaseStatus.Requested, "case created", command.CreatedAt, ct);
        foreach (var domainEvent in command.Events)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at)
                VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)
                """,
                new
                {
                    aggregateId = command.Case.Id.Value,
                    eventType = domainEvent.EventType,
                    payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    occurredAt = command.CreatedAt,
                }, tx, cancellationToken: ct));
        }

        foreach (var planned in command.EffectPlan.Effects)
        {
            var envelope = CreateEnvelope(planned, command);
            ModerationCaseId? caseId = IsCaseApplyingEffect(planned) ? command.Case.Id : null;
            await InsertEffectAsync(conn, tx, envelope, planned.Importance, caseId, ct);
        }

        var metric = new EmitMetricEffect(
            "moderation_cases_created_total",
            new Dictionary<string, string>
            {
                ["action"] = ToDb(command.Case.Action),
                ["actor_type"] = ToDb(command.Case.ActorType),
            },
            command.CorrelationId,
            command.CausationId);
        await InsertEffectAsync(conn, tx, CreateMetricEnvelope(metric, command), EffectImportance.BestEffort, null, ct);

        if (!command.EffectPlan.Effects.Any(planned => planned.Importance == EffectImportance.Required))
            await SetCaseStatusAsync(conn, tx, command.Case.Id, ModerationCaseStatus.Applied, "no required external effect", ct);

        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_admin_commands SET case_id = @caseId WHERE command_id = @commandId",
            new { caseId = command.Case.Id.Value, commandId = command.CommandId }, tx, cancellationToken: ct));
        await InsertAuditAsync(
            conn,
            tx,
            command.Case.ChatId,
            command.Case.ActorUserId,
            command.Case.TargetUserId,
            command.CorrelationId,
            command.Case.Id,
            $"moderation.{ToDb(command.Case.Action)}.requested",
            new { command.Case.Action, command.Case.Reason, command.Case.ExpiresAt, WarningId = command.Warning?.Id },
            command.CreatedAt,
            ct);

        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, command.Case.Id);
    }

    public async Task<IReadOnlyList<WarningState>> ListWarningsAsync(
        ChatId chatId,
        UserId targetUserId,
        bool activeOnly,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<WarningRow>(new CommandDefinition(
            """
            SELECT warning_id AS "WarningId", chat_id AS "ChatId", target_user_id AS "TargetUserId",
                   actor_user_id AS "ActorUserId", reason AS "Reason", created_at AS "CreatedAt",
                   expires_at AS "ExpiresAt", is_active AS "IsActive", revocation_reason AS "RevocationReason"
            FROM chat_admin_warnings
            WHERE chat_id = @chatId AND target_user_id = @targetUserId
              AND (@activeOnly = false OR (is_active AND (expires_at IS NULL OR expires_at > now())))
            ORDER BY created_at DESC, warning_id
            """,
            new { chatId = chatId.Value, targetUserId = targetUserId.Value, activeOnly },
            cancellationToken: ct));
        return rows.Select(ReadWarning).ToArray();
    }

    public async Task<PersistCommandResult> PersistWarningMutationAsync(
        WarningMutationCommand command,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var inserted = await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_commands (command_id, idempotency_key, response_text, created_at)
            VALUES (@commandId, @idempotencyKey, @responseText, @createdAt)
            ON CONFLICT (command_id) DO NOTHING
            """,
            new
            {
                command.CommandId,
                command.IdempotencyKey,
                command.ResponseText,
                command.CreatedAt,
            }, tx, cancellationToken: ct));
        if (inserted == 0)
        {
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, null);
        }

        foreach (var warning in command.Warnings)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE chat_admin_warnings
                SET is_active = @isActive, revocation_reason = @revocationReason
                WHERE warning_id = @warningId AND chat_id = @chatId AND target_user_id = @targetUserId
                """,
                new
                {
                    warningId = warning.Id.Value,
                    chatId = warning.ChatId.Value,
                    targetUserId = warning.TargetUserId.Value,
                    warning.IsActive,
                    revocationReason = warning.RevocationReason is null ? null : ToDb(warning.RevocationReason.Value),
                }, tx, cancellationToken: ct));
        }
        foreach (var domainEvent in command.Events)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
                new
                {
                    aggregateId = command.TargetUserId.Value,
                    eventType = domainEvent.EventType,
                    payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    occurredAt = command.CreatedAt,
                }, tx, cancellationToken: ct));
        }
        await InsertAuditAsync(
            conn,
            tx,
            command.ChatId,
            command.ActorUserId,
            command.TargetUserId,
            command.CorrelationId,
            null,
            "warning.revoked",
            new { WarningIds = command.Warnings.Select(warning => warning.Id), command.Warnings.Count },
            command.CreatedAt,
            ct);
        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, null);
    }

    public async Task<int> ExpireWarningsAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var rows = (await conn.QueryAsync<WarningRow>(new CommandDefinition(
            """
            WITH expired AS (
                SELECT warning_id
                FROM chat_admin_warnings
                WHERE is_active AND expires_at IS NOT NULL AND expires_at <= @now
                ORDER BY expires_at, warning_id
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            UPDATE chat_admin_warnings warning
            SET is_active = false, revocation_reason = 'expired'
            FROM expired
            WHERE warning.warning_id = expired.warning_id
            RETURNING warning.warning_id AS "WarningId", warning.chat_id AS "ChatId",
                      warning.target_user_id AS "TargetUserId", warning.actor_user_id AS "ActorUserId",
                      warning.reason AS "Reason", warning.created_at AS "CreatedAt",
                      warning.expires_at AS "ExpiresAt", warning.is_active AS "IsActive",
                      warning.revocation_reason AS "RevocationReason"
            """,
            new { now, limit = Math.Clamp(limit, 1, 1000) }, tx, cancellationToken: ct))).ToArray();
        foreach (var warning in rows)
        {
            var state = ReadWarning(warning);
            var domainEvent = new WarningRevoked(
                state.ChatId,
                state.TargetUserId,
                state.Id,
                WarningRevocationReason.Expired);
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
                new
                {
                    aggregateId = state.Id.Value,
                    eventType = domainEvent.EventType,
                    payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    occurredAt = now,
                }, tx, cancellationToken: ct));
            await InsertAuditAsync(
                conn,
                tx,
                state.ChatId,
                null,
                state.TargetUserId,
                $"warning-expire:{state.Id}",
                null,
                "warning.expired",
                new { state.Id, state.ExpiresAt },
                now,
                ct);
        }
        await tx.CommitAsync(ct);
        return rows.Length;
    }

    public async Task<PersistCommandResult> PersistRoleMutationAsync(
        RoleMutationCommand command,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var inserted = await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_commands (command_id, idempotency_key, response_text, created_at)
            VALUES (@commandId, @idempotencyKey, @responseText, @createdAt)
            ON CONFLICT (command_id) DO NOTHING
            """,
            new
            {
                command.CommandId,
                command.IdempotencyKey,
                command.ResponseText,
                command.CreatedAt,
            }, tx, cancellationToken: ct));
        if (inserted == 0)
        {
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, null);
        }

        await UpsertMemberAsync(conn, tx, command.ResultMember, command.CreatedAt, ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
            new
            {
                aggregateId = StableMemberAggregateId(command.ChatId, command.TargetUserId),
                eventType = command.Event.EventType,
                payload = JsonSerializer.Serialize(command.Event, command.Event.GetType(), JsonOptions),
                occurredAt = command.CreatedAt,
            }, tx, cancellationToken: ct));
        await InsertAuditAsync(
            conn,
            tx,
            command.ChatId,
            command.ActorUserId,
            command.TargetUserId,
            command.CorrelationId,
            null,
            command.Assign ? "member.role.assigned" : "member.role.removed",
            new { command.Role, command.CustomRoleId, command.Assign },
            command.CreatedAt,
            ct);
        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, null);
    }

    public async Task<PersistCommandResult> PersistMemberJoinedAsync(
        MemberJoinedCommand command,
        MemberLifecycleDecision decision,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var inserted = await InsertCommandAsync(conn, tx, command.CommandId, command.IdempotencyKey, null, string.Empty, command.CreatedAt, ct);
        if (!inserted)
        {
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, null);
        }

        await EnsureChatRowAsync(conn, tx, command.ChatId, command.CreatedAt, ct);
        var member = decision.Events.OfType<MemberJoined>().Single().Member;
        await UpsertMemberAsync(conn, tx, member, command.CreatedAt, ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_admin_members SET joined_at = @at, left_at = NULL, status = 'active', trust_level = 'new' WHERE chat_id = @chatId AND user_id = @userId",
            new { chatId = command.ChatId.Value, userId = command.UserId.Value, at = command.CreatedAt }, tx, cancellationToken: ct));
        await InsertLifecycleEventsAsync(conn, tx, command.ChatId, decision.Events, command.CreatedAt, ct);
        foreach (var planned in decision.EffectPlan.Effects)
            await InsertEffectAsync(conn, tx, CreateLifecycleEnvelope(planned, command), planned.Importance, null, ct);
        await InsertAuditAsync(conn, tx, command.ChatId, null, command.UserId, command.CorrelationId, null,
            "member.joined", new { command.UserId, command.DisplayName }, command.CreatedAt, ct);
        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, null);
    }

    public async Task<PersistCommandResult> PersistMemberLeftAsync(
        MemberLeftCommand command,
        MemberLifecycleDecision decision,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var inserted = await InsertCommandAsync(conn, tx, command.CommandId, command.IdempotencyKey, null, string.Empty, command.CreatedAt, ct);
        if (!inserted)
        {
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, null);
        }

        await EnsureChatRowAsync(conn, tx, command.ChatId, command.CreatedAt, ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_admin_members SET left_at = @at, last_seen_at = @at, status = 'left' WHERE chat_id = @chatId AND user_id = @userId",
            new { chatId = command.ChatId.Value, userId = command.UserId.Value, at = command.CreatedAt }, tx, cancellationToken: ct));
        await InsertLifecycleEventsAsync(conn, tx, command.ChatId, decision.Events, command.CreatedAt, ct);
        foreach (var planned in decision.EffectPlan.Effects)
            await InsertEffectAsync(conn, tx, CreateLifecycleEnvelope(planned, command), planned.Importance, null, ct);
        await InsertAuditAsync(conn, tx, command.ChatId, null, command.UserId, command.CorrelationId, null,
            "member.left", new { command.UserId, command.DisplayName }, command.CreatedAt, ct);
        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, null);
    }

    public async Task<IReadOnlyList<ModerationCaseState>> ListCasesAsync(
        ChatId chatId,
        UserId? targetUserId,
        int limit,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<CaseRow>(new CommandDefinition(
            """
            SELECT case_id AS "CaseId", chat_id AS "ChatId", target_user_id AS "TargetUserId",
                   actor_user_id AS "ActorUserId", actor_type AS "ActorType", action AS "Action",
                   reason AS "Reason", source_message_id AS "SourceMessageId", source_rule_id AS "SourceRuleId",
                   created_at AS "CreatedAt", expires_at AS "ExpiresAt", status AS "Status",
                   correlation_id AS "CorrelationId"
            FROM chat_admin_cases
            WHERE chat_id = @chatId AND (@targetUserId IS NULL OR target_user_id = @targetUserId)
            ORDER BY created_at DESC, case_id DESC
            LIMIT @limit
            """,
            new { chatId = chatId.Value, targetUserId = targetUserId?.Value, limit = Math.Clamp(limit, 1, 100), },
            cancellationToken: ct));
        return rows.Select(ReadCase).ToArray();
    }

    public async Task<ModerationAnalytics> LoadAnalyticsAsync(ChatId chatId, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleAsync<AnalyticsRow>(new CommandDefinition(
            """
            SELECT
                (SELECT COUNT(*) FROM chat_admin_cases WHERE chat_id = @chatId) AS "Cases",
                (SELECT COUNT(*) FROM chat_admin_cases WHERE chat_id = @chatId AND status = 'applied') AS "AppliedCases",
                (SELECT COUNT(*) FROM chat_admin_cases WHERE chat_id = @chatId AND status = 'failed') AS "FailedCases",
                (SELECT COUNT(*) FROM chat_admin_cases WHERE chat_id = @chatId AND status IN ('unknown', 'revocation_unknown')) AS "UnknownCases",
                (SELECT COUNT(*) FROM chat_admin_warnings WHERE chat_id = @chatId AND is_active AND (expires_at IS NULL OR expires_at > now())) AS "ActiveWarnings",
                (SELECT COUNT(*) FROM chat_admin_message_index WHERE chat_id = @chatId) AS "IndexedMessages"
            """,
            new { chatId = chatId.Value }, cancellationToken: ct));
        var actions = await conn.QueryAsync<AnalyticsActionRow>(new CommandDefinition(
            "SELECT action AS \"Action\", COUNT(*) AS \"Count\" FROM chat_admin_cases WHERE chat_id = @chatId GROUP BY action ORDER BY action",
            new { chatId = chatId.Value }, cancellationToken: ct));
        return new ModerationAnalytics(chatId, row.Cases, row.AppliedCases, row.FailedCases, row.UnknownCases,
            row.ActiveWarnings, row.IndexedMessages, actions.ToDictionary(item => item.Action, item => item.Count, StringComparer.Ordinal));
    }

    public async Task<ModerationCaseState?> LoadCaseAsync(
        ChatId chatId,
        ModerationCaseId caseId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<CaseRow>(new CommandDefinition(
            """
            SELECT case_id AS "CaseId", chat_id AS "ChatId", target_user_id AS "TargetUserId",
                   actor_user_id AS "ActorUserId", actor_type AS "ActorType", action AS "Action",
                   reason AS "Reason", source_message_id AS "SourceMessageId", source_rule_id AS "SourceRuleId",
                   created_at AS "CreatedAt", expires_at AS "ExpiresAt", status AS "Status",
                   correlation_id AS "CorrelationId"
            FROM chat_admin_cases
            WHERE chat_id = @chatId AND case_id = @caseId
            """,
            new { chatId = chatId.Value, caseId = caseId.Value }, cancellationToken: ct));
        return row is null ? null : ReadCase(row);
    }

    public async Task<PersistCommandResult> PersistCaseRevocationAsync(
        RevokeModerationCaseCommand command,
        CaseRevocationDecision decision,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var inserted = await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_commands (command_id, idempotency_key, case_id, response_text, created_at)
            VALUES (@commandId, @idempotencyKey, @caseId, @responseText, @createdAt)
            ON CONFLICT (command_id) DO NOTHING
            """,
            new
            {
                command.CommandId,
                command.IdempotencyKey,
                caseId = command.CaseId.Value,
                responseText = "moderation case revocation requested",
                command.CreatedAt,
            }, tx, cancellationToken: ct));
        if (inserted == 0)
        {
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, command.CaseId);
        }

        await SetCaseStatusAsync(
            conn,
            tx,
            command.CaseId,
            ModerationCaseStatus.Revoking,
            "case revocation requested",
            ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_admin_members SET desired_restriction = NULL, last_seen_at = now() WHERE chat_id = @chatId AND user_id = (SELECT target_user_id FROM chat_admin_cases WHERE case_id = @caseId)",
            new { chatId = command.ChatId.Value, caseId = command.CaseId.Value }, tx, cancellationToken: ct));
        foreach (var domainEvent in decision.Events)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
                new
                {
                    aggregateId = command.CaseId.Value,
                    eventType = domainEvent.EventType,
                    payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    occurredAt = command.CreatedAt,
                }, tx, cancellationToken: ct));
        }
        foreach (var planned in decision.EffectPlan.Effects)
        {
            var envelope = CreateRevocationEnvelope(planned, command);
            ModerationCaseId? caseId = IsCaseApplyingEffect(planned) ? command.CaseId : null;
            await InsertEffectAsync(conn, tx, envelope, planned.Importance, caseId, ct);
        }
        await InsertAuditAsync(
            conn,
            tx,
            command.ChatId,
            command.ActorUserId,
            null,
            command.CorrelationId,
            command.CaseId,
            "moderation.case.revocation.requested",
            new { command.CaseId },
            command.CreatedAt,
            ct);
        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, command.CaseId);
    }

    public async Task<AppealState?> LoadAppealAsync(
        ChatId chatId,
        AppealId appealId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<AppealRow>(new CommandDefinition(
            """
            SELECT appeal_id AS "AppealId", case_id AS "CaseId", chat_id AS "ChatId",
                   author_user_id AS "AuthorUserId", text AS "Text", status AS "Status",
                   resolved_by AS "ResolvedBy", resolution_comment AS "ResolutionComment",
                   created_at AS "CreatedAt", resolved_at AS "ResolvedAt"
            FROM chat_admin_appeals
            WHERE chat_id = @chatId AND appeal_id = @appealId
            """,
            new { chatId = chatId.Value, appealId = appealId.Value }, cancellationToken: ct));
        return row is null ? null : ReadAppeal(row);
    }

    public async Task<PersistCommandResult> PersistAppealOpenAsync(
        OpenAppealCommand command,
        AppealDecision decision,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var inserted = await InsertCommandAsync(
            conn,
            tx,
            command.CommandId,
            command.IdempotencyKey,
            command.CaseId,
            "appeal opened",
            command.CreatedAt,
            ct);
        if (!inserted)
        {
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, command.CaseId);
        }

        var appeal = decision.Appeal!;
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_appeals
                (appeal_id, case_id, chat_id, author_user_id, text, status, created_at)
            VALUES (@appealId, @caseId, @chatId, @authorUserId, @text, @status, @createdAt)
            """,
            new
            {
                appealId = appeal.Id.Value,
                caseId = appeal.CaseId.Value,
                chatId = command.ChatId.Value,
                authorUserId = appeal.AuthorUserId.Value,
                appeal.Text,
                status = ToDb(appeal.Status),
                createdAt = appeal.CreatedAt,
            }, tx, cancellationToken: ct));
        await InsertDomainEventsAsync(conn, tx, command.CaseId, decision.Events, command.CreatedAt, ct);
        await InsertAuditAsync(
            conn,
            tx,
            command.ChatId,
            command.AuthorUserId,
            command.AuthorUserId,
            command.CorrelationId,
            command.CaseId,
            "appeal.opened",
            new { AppealId = appeal.Id },
            command.CreatedAt,
            ct);
        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, command.CaseId);
    }

    public async Task<PersistCommandResult> PersistAppealResolutionAsync(
        ResolveAppealCommand command,
        AppealDecision decision,
        CaseRevocationDecision? revocation,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var appeal = decision.Appeal!;
        var inserted = await InsertCommandAsync(
            conn,
            tx,
            command.CommandId,
            command.IdempotencyKey,
            appeal.CaseId,
            "appeal resolved",
            command.CreatedAt,
            ct);
        if (!inserted)
        {
            await tx.CommitAsync(ct);
            return new PersistCommandResult(true, appeal.CaseId);
        }

        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE chat_admin_appeals
            SET status = @status, resolved_by = @resolvedBy, resolution_comment = @resolutionComment,
                resolved_at = @resolvedAt
            WHERE appeal_id = @appealId AND chat_id = @chatId
            """,
            new
            {
                appealId = appeal.Id.Value,
                chatId = command.ChatId.Value,
                status = ToDb(appeal.Status),
                resolvedBy = appeal.ResolvedBy?.Value,
                appeal.ResolutionComment,
                appeal.ResolvedAt,
            }, tx, cancellationToken: ct));
        await InsertDomainEventsAsync(conn, tx, appeal.CaseId, decision.Events, command.CreatedAt, ct);

        if (revocation is not null)
        {
            await SetCaseStatusAsync(
                conn,
                tx,
                appeal.CaseId,
                ModerationCaseStatus.Revoking,
                "appeal approved; case revocation requested",
                ct);
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE chat_admin_members SET desired_restriction = NULL, last_seen_at = now() WHERE chat_id = @chatId AND user_id = (SELECT target_user_id FROM chat_admin_cases WHERE case_id = @caseId)",
                new { chatId = command.ChatId.Value, caseId = appeal.CaseId.Value }, tx, cancellationToken: ct));
            await InsertDomainEventsAsync(conn, tx, appeal.CaseId, revocation.Events, command.CreatedAt, ct);
            var revokeCommand = new RevokeModerationCaseCommand(
                command.CommandId,
                command.IdempotencyKey,
                command.CorrelationId,
                command.CausationId,
                command.ChatId,
                command.ActorUserId,
                appeal.CaseId,
                command.SourceMessageId,
                command.CreatedAt);
            foreach (var planned in revocation.EffectPlan.Effects)
            {
                var envelope = CreateRevocationEnvelope(planned, revokeCommand);
                ModerationCaseId? caseId = IsCaseApplyingEffect(planned) ? appeal.CaseId : null;
                await InsertEffectAsync(conn, tx, envelope, planned.Importance, caseId, ct);
            }
        }

        await InsertAuditAsync(
            conn,
            tx,
            command.ChatId,
            command.ActorUserId,
            appeal.AuthorUserId,
            command.CorrelationId,
            appeal.CaseId,
            command.Approve ? "appeal.approved" : "appeal.rejected",
            new { AppealId = appeal.Id, appeal.ResolutionComment },
            command.CreatedAt,
            ct);
        await tx.CommitAsync(ct);
        return new PersistCommandResult(false, appeal.CaseId);
    }

    public async Task UpdateChatSettingsAsync(
        ChatId chatId,
        ChatSettings settings,
        UserId actorUserId,
        string correlationId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_chats (chat_id, chat_type, title, is_enabled, settings, created_at, updated_at)
            VALUES (@chatId, 'supergroup', 'Telegram chat', true, CAST(@settings AS jsonb), @now, @now)
            ON CONFLICT (chat_id) DO UPDATE SET settings = EXCLUDED.settings, updated_at = EXCLUDED.updated_at
            """,
            new
            {
                chatId = chatId.Value,
                settings = JsonSerializer.Serialize(settings, JsonOptions),
                now,
            }, tx, cancellationToken: ct));
        await InsertAuditAsync(
            conn,
            tx,
            chatId,
            actorUserId,
            null,
            correlationId,
            null,
            "settings.updated",
            new { settings.Language, settings.AutoModerationEnabled, settings.CaptchaPolicy.Enabled },
            now,
            ct);
        await AppendStandaloneDomainEventAsync(
            conn,
            tx,
            new ChatSettingsUpdated(chatId, settings, now),
            now,
            ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ChatState>> ListEnabledChatsAsync(CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<ChatRow>(new CommandDefinition(
            """
            SELECT chat_id AS "ChatId", chat_type AS "ChatType", title AS "Title",
                   is_enabled AS "IsEnabled", settings::text AS "SettingsJson",
                   bot_permissions::text AS "BotPermissionsJson",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM chat_admin_chats
            WHERE is_enabled
            ORDER BY chat_id
            """,
            cancellationToken: ct));
        return rows.Select(ReadChat).ToArray();
    }

    public async Task<IReadOnlyList<ChatState>> ListRegisteredChatsAsync(CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<ChatRow>(new CommandDefinition(
            """
            SELECT chat_id AS "ChatId", chat_type AS "ChatType", title AS "Title",
                   is_enabled AS "IsEnabled", settings::text AS "SettingsJson",
                   bot_permissions::text AS "BotPermissionsJson",
                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
            FROM chat_admin_chats
            ORDER BY chat_id
            """,
            cancellationToken: ct));
        return rows.Select(ReadChat).ToArray();
    }

    public async Task<int> CleanupRetentionAsync(
        ChatId chatId,
        RetentionPolicy policy,
        DateTimeOffset now,
        int batchSize,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var limit = Math.Clamp(batchSize, 1, 10000);
        var auditCutoff = now - NonNegative(policy.AuditLogRetention);
        var messageCutoff = now - NonNegative(policy.MessageIndexRetention);
        var callbackCutoff = now - NonNegative(policy.CallbackStateRetention);

        var deletedAudit = await conn.ExecuteAsync(new CommandDefinition(
            """
            WITH expired AS (
                SELECT id
                FROM chat_admin_audit_events
                WHERE chat_id = @chatId AND created_at < @cutoff
                ORDER BY id
                LIMIT @limit
            )
            DELETE FROM chat_admin_audit_events audit
            USING expired
            WHERE audit.id = expired.id
            """,
            new { chatId = chatId.Value, cutoff = auditCutoff, limit }, tx, cancellationToken: ct));
        var deletedMessages = await conn.ExecuteAsync(new CommandDefinition(
            """
            WITH expired AS (
                SELECT ctid
                FROM chat_admin_message_index
                WHERE chat_id = @chatId AND sent_at < @cutoff
                ORDER BY sent_at, message_id
                LIMIT @limit
            )
            DELETE FROM chat_admin_message_index messages
            USING expired
            WHERE messages.ctid = expired.ctid
            """,
            new { chatId = chatId.Value, cutoff = messageCutoff, limit }, tx, cancellationToken: ct));
        var deletedCallbacks = await conn.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM chat_admin_settings_callbacks
            WHERE chat_id = @chatId
              AND ((expires_at < @now) OR (consumed_at IS NOT NULL AND consumed_at < @cutoff))
            """,
            new { chatId = chatId.Value, now, cutoff = callbackCutoff }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return deletedAudit + deletedMessages + deletedCallbacks;
    }

    public async Task<IReadOnlyList<DesiredMemberRestriction>> ListDesiredRestrictionsAsync(
        ChatId chatId,
        int limit,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<DesiredRestrictionRow>(new CommandDefinition(
            """
            SELECT chat_id AS "ChatId", user_id AS "UserId",
                   desired_restriction::text AS "DesiredRestrictionJson",
                   observed_restriction::text AS "ObservedRestrictionJson"
            FROM chat_admin_members
            WHERE chat_id = @chatId AND desired_restriction IS NOT NULL
            ORDER BY last_seen_at DESC, user_id
            LIMIT @limit
            """,
            new { chatId = chatId.Value, limit = Math.Clamp(limit, 1, 500) }, cancellationToken: ct));
        return rows
            .Select(row => DeserializeNullable<RestrictionState>(row.DesiredRestrictionJson) is { } state
                ? new DesiredMemberRestriction(
                    new ChatId(row.ChatId),
                    new UserId(row.UserId),
                    state,
                    DeserializeNullable<RestrictionState>(row.ObservedRestrictionJson))
                : null)
            .OfType<DesiredMemberRestriction>()
            .ToList();
    }

    public async Task UpdateObservedRestrictionAsync(
        ChatId chatId,
        UserId userId,
        RestrictionState? state,
        string correlationId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE chat_admin_members
            SET observed_restriction = CAST(@state AS jsonb), last_seen_at = now()
            WHERE chat_id = @chatId AND user_id = @userId
            """,
            new
            {
                chatId = chatId.Value,
                userId = userId.Value,
                state = state is null ? null : JsonSerializer.Serialize(state, JsonOptions),
            }, tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at)
            VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), now())
            """,
            new
            {
                aggregateId = StableAggregateId(chatId),
                eventType = "restriction.observed_state_changed",
                payload = JsonSerializer.Serialize(new RestrictionObservedStateChanged(chatId, userId, state), JsonOptions),
            }, tx, cancellationToken: ct));
        await InsertAuditAsync(
            conn,
            tx,
            chatId,
            null,
            userId,
            correlationId,
            null,
            "restriction.observed_state_changed",
            new { state },
            DateTimeOffset.UtcNow,
            ct);
        await tx.CommitAsync(ct);
    }

    public async Task UpdateBotPermissionsAsync(
        ChatId chatId,
        TelegramBotPermissions permissions,
        string correlationId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_admin_chats SET bot_permissions = CAST(@permissions AS jsonb), updated_at = now() WHERE chat_id = @chatId",
            new { chatId = chatId.Value, permissions = JsonSerializer.Serialize(permissions, JsonOptions) }, tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
            new
            {
                aggregateId = StableAggregateId(chatId),
                eventType = nameof(BotPermissionsObserved),
                payload = JsonSerializer.Serialize(new BotPermissionsObserved(chatId, permissions, permissions.ObservedAt), JsonOptions),
                occurredAt = permissions.ObservedAt,
            }, tx, cancellationToken: ct));
        await InsertAuditAsync(conn, tx, chatId, null, null, correlationId, null,
            "bot.permissions.observed", permissions, permissions.ObservedAt, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<string> CreateSettingsCallbackAsync(
        ChatId chatId,
        string key,
        string value,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var token = Guid.NewGuid().ToString("N");
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO chat_admin_settings_callbacks (token, chat_id, setting_key, setting_value, expires_at) VALUES (@token, @chatId, @key, @value, @expiresAt)",
            new { token, chatId = chatId.Value, key, value, expiresAt }, cancellationToken: ct));
        return token;
    }

    public async Task<SettingsCallbackState?> ConsumeSettingsCallbackAsync(
        string token,
        ChatId chatId,
        UserId actorUserId,
        CancellationToken ct)
    {
        _ = actorUserId;
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<SettingsCallbackRow>(new CommandDefinition(
            """
            UPDATE chat_admin_settings_callbacks
            SET consumed_at = now()
            WHERE token = @token AND chat_id = @chatId AND consumed_at IS NULL AND expires_at > now()
            RETURNING token AS "Token", chat_id AS "ChatId", setting_key AS "Key",
                      setting_value AS "Value", expires_at AS "ExpiresAt"
            """,
            new { token, chatId = chatId.Value }, cancellationToken: ct));
        return row is null
            ? null
            : new SettingsCallbackState(row.Token, new ChatId(row.ChatId), row.Key, row.Value, UtcOffset(row.ExpiresAt));
    }

    public async Task EnqueueEffectAsync(
        IModerationEffect effect,
        string idempotencyKey,
        EffectImportance importance,
        CancellationToken ct)
        => await EnqueueEffectCoreAsync(effect, idempotencyKey, importance, null, ct);

    public async Task EnqueueScheduledEffectAsync(
        IModerationEffect effect,
        DateTimeOffset executeAt,
        string idempotencyKey,
        EffectImportance importance,
        CancellationToken ct)
        => await EnqueueEffectCoreAsync(effect, idempotencyKey, importance, executeAt, ct);

    public async Task CancelEffectAsync(EffectId effectId, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE chat_admin_effect_outbox
            SET status = 'cancelled', locked_until = NULL, updated_at = now(), completed_at = COALESCE(completed_at, now())
            WHERE effect_id = @effectId AND status IN ('pending', 'ready', 'failed_retryable', 'unknown')
            """,
            new { effectId = effectId.Value }, cancellationToken: ct));
    }

    private async Task EnqueueEffectCoreAsync(
        IModerationEffect effect,
        string idempotencyKey,
        EffectImportance importance,
        DateTimeOffset? notBefore,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var envelope = new EffectEnvelope
        {
            Id = EffectId.New(),
            EffectType = EffectTypeOf(effect),
            Payload = effect,
            CorrelationId = idempotencyKey,
            CausationId = idempotencyKey,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow,
            NotBefore = notBefore,
        };
        await InsertEffectAsync(conn, null, envelope, importance, null, ct);
    }

    public async Task EnqueueResponseAsync(ChatId chatId, string text, int? replyToMessageId, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var effect = new SendMessageEffect(chatId, text, replyToMessageId, MessageParseMode.Html);
        var idempotencyKey = $"validation:{chatId}:{replyToMessageId}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16]}";
        var envelope = new EffectEnvelope
        {
            Id = EffectId.New(),
            EffectType = SendMessageEffectType,
            Payload = effect,
            CorrelationId = idempotencyKey,
            CausationId = idempotencyKey,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await InsertEffectAsync(conn, transaction: null, envelope, EffectImportance.BestEffort, null, ct);
    }

    public async Task<IReadOnlyList<StoredModerationEffect>> ClaimDueEffectsAsync(int limit, TimeSpan lease, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var lostCaseIds = await conn.QueryAsync<Guid?>(new CommandDefinition(
            """
            UPDATE chat_admin_effect_outbox
            SET status = 'unknown', locked_until = NULL, last_error_code = 'worker_lost_lease',
                last_error_message = 'Worker stopped after claiming the effect', updated_at = now()
            WHERE tenant_key = @tenantKey AND status = 'executing' AND locked_until < now()
            RETURNING case_id
            """,
            new { tenantKey },
            cancellationToken: ct));
        foreach (var caseId in lostCaseIds.Where(caseId => caseId is not null).Distinct())
        {
            await SetCaseStatusAsync(
                conn,
                tx,
                new ModerationCaseId(caseId!.Value),
                ModerationCaseStatus.Unknown,
                "worker lost effect lease",
                ct);
        }

        var rows = await conn.QueryAsync<EffectRow>(new CommandDefinition(
            """
            WITH due AS (
                SELECT effect_id
                FROM chat_admin_effect_outbox o
                WHERE o.tenant_key = @tenantKey
                  AND status IN ('pending', 'failed_retryable')
                  AND not_before <= now()
                  AND (locked_until IS NULL OR locked_until <= now())
                  AND NOT EXISTS (
                      SELECT 1
                      FROM jsonb_array_elements_text(o.dependencies) dependency_id
                      WHERE NOT EXISTS (
                          SELECT 1 FROM chat_admin_effect_outbox dependency
                          WHERE dependency.effect_id = dependency_id::uuid
                            AND dependency.status = 'applied'))
                ORDER BY created_at, effect_id
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            UPDATE chat_admin_effect_outbox o
            SET status = 'executing', attempt = o.attempt + 1,
                locked_until = now() + (@leaseMs * interval '1 millisecond'),
                started_at = now(), updated_at = now()
            FROM due WHERE o.effect_id = due.effect_id
            RETURNING o.effect_id AS "EffectId", o.effect_type AS "EffectType",
                      o.payload::text AS "PayloadJson", o.case_id AS "CaseId", o.importance AS "Importance",
                      o.attempt AS "Attempt", o.maximum_attempts AS "MaximumAttempts"
            """,
            new
            {
                tenantKey,
                limit = Math.Clamp(limit, 1, 100),
                leaseMs = Math.Max(lease.TotalMilliseconds, 1),
            },
            tx, cancellationToken: ct));
        foreach (var row in rows.Where(row => row.CaseId is not null && row.EffectType != SendMessageEffectType))
        {
            var caseId = new ModerationCaseId(row.CaseId!.Value);
            if (!await IsCaseRevocationInProgressAsync(conn, tx, caseId, ct))
            {
                await SetCaseStatusAsync(
                    conn,
                    tx,
                    caseId,
                    ModerationCaseStatus.Applying,
                    "effect claimed",
                    ct);
            }
        }
        await tx.CommitAsync(ct);
        return rows.Select(ReadEffect).ToList();
    }

    public async Task MarkEffectAppliedAsync(StoredModerationEffect effect, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE chat_admin_effect_outbox
            SET status = 'applied', completed_at = now(), locked_until = NULL, updated_at = now(),
                last_error_code = NULL, last_error_message = NULL
            WHERE effect_id = @effectId AND status = 'executing'
            """,
            new { effectId = effect.EffectId.Value }, tx, cancellationToken: ct));
        if (effect.Payload is RestrictMemberEffect restriction)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE chat_admin_members
                SET observed_restriction = CAST(@state AS jsonb), last_seen_at = now()
                WHERE chat_id = @chatId AND user_id = @userId
                """,
                new
                {
                    chatId = restriction.ChatId.Value,
                    userId = restriction.UserId.Value,
                    state = JsonSerializer.Serialize(new RestrictionState { CanSendMessages = false, Until = restriction.Until }, JsonOptions),
                }, tx, cancellationToken: ct));
        }
        else if (effect.Payload is UnrestrictMemberEffect unrestriction)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE chat_admin_members
                SET desired_restriction = NULL, observed_restriction = NULL, last_seen_at = now()
                WHERE chat_id = @chatId AND user_id = @userId
                  AND (@expectedState IS NULL OR desired_restriction = CAST(@expectedState AS jsonb))
                """,
                new
                {
                    chatId = unrestriction.ChatId.Value,
                    userId = unrestriction.UserId.Value,
                    expectedState = ExpectedRestrictionJson(unrestriction.ExpectedUntil),
                }, tx, cancellationToken: ct));
        }
        else if (effect.Payload is DeleteMessagesEffect deleteMessages)
        {
            await DeleteIndexedMessagesAsync(conn, tx, deleteMessages, ct);
        }
        else if (effect.Payload is MarkModerationCaseRevokedEffect revoked)
        {
            await SetCaseStatusAsync(conn, tx, revoked.CaseId, ModerationCaseStatus.Revoked, "case revocation applied", ct);
        }
        if (effect.CaseId is not null)
        {
            if (!await IsCaseRevocationInProgressAsync(conn, tx, effect.CaseId.Value, ct))
            {
                var complete = await MarkRequiredEffectAppliedAsync(conn, tx, effect, "external effect applied", ct);
                if (complete)
                    await InsertFollowUpLogAsync(conn, tx, effect, ct);
            }
        }
        await tx.CommitAsync(ct);
    }

    public Task MarkEffectFailedAsync(StoredModerationEffect effect, string code, string message, bool retryable, TimeSpan? retryAfter, CancellationToken ct) =>
        MarkEffectFailedCoreAsync(effect, code, message, retryable, retryAfter, ct);

    public async Task MarkEffectUnknownAsync(StoredModerationEffect effect, string code, string message, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE chat_admin_effect_outbox
            SET status = 'unknown', locked_until = NULL, updated_at = now(),
                last_error_code = @code, last_error_message = @message
            WHERE effect_id = @effectId AND status = 'executing'
            """,
            new { effectId = effect.EffectId.Value, code, message = Truncate(message) }, tx, cancellationToken: ct));
        if (effect.CaseId is not null)
        {
            var revocationInProgress = await IsCaseRevocationInProgressAsync(conn, tx, effect.CaseId.Value, ct);
            await SetCaseStatusAsync(
                conn,
                tx,
                effect.CaseId.Value,
                revocationInProgress ? ModerationCaseStatus.RevocationUnknown : ModerationCaseStatus.Unknown,
                message,
                ct);
            if (effect.Payload is RestrictMemberEffect restriction)
                await conn.ExecuteAsync(new CommandDefinition(
                    "UPDATE chat_admin_members SET observed_restriction = NULL WHERE chat_id = @chatId AND user_id = @userId",
                    new { chatId = restriction.ChatId.Value, userId = restriction.UserId.Value }, tx, cancellationToken: ct));
        }
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<StoredModerationEffect>> ListUnknownEffectsAsync(int limit, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<EffectRow>(new CommandDefinition(
            """
            SELECT effect_id AS "EffectId", effect_type AS "EffectType", payload::text AS "PayloadJson",
                   case_id AS "CaseId", importance AS "Importance", attempt AS "Attempt", maximum_attempts AS "MaximumAttempts"
            FROM chat_admin_effect_outbox
            WHERE tenant_key = @tenantKey AND status = 'unknown'
            ORDER BY updated_at, effect_id
            LIMIT @limit
            """,
            new { tenantKey, limit = Math.Clamp(limit, 1, 100) }, cancellationToken: ct));
        return rows.Select(ReadEffect).ToList();
    }

    public async Task RequeueUnknownAsync(StoredModerationEffect effect, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE chat_admin_effect_outbox
            SET status = 'pending',
                not_before = now() + (@retryMs * interval '1 millisecond'),
                locked_until = NULL,
                updated_at = now()
            WHERE effect_id = @effectId AND status = 'unknown'
            """,
            new
            {
                effectId = effect.EffectId.Value,
                retryMs = Math.Max(Backoff(effect.Attempt).TotalMilliseconds, TimeSpan.FromSeconds(1).TotalMilliseconds),
            }, cancellationToken: ct));
    }

    public async Task ConfirmUnknownAppliedAsync(StoredModerationEffect effect, CancellationToken ct)
    {
        await MarkUnknownAsAppliedAsync(effect, ct);
    }

    private async Task MarkEffectFailedCoreAsync(
        StoredModerationEffect effect,
        string code,
        string message,
        bool retryable,
        TimeSpan? retryAfter,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var terminal = !retryable || effect.Attempt >= effect.MaximumAttempts;
        var status = terminal ? EffectExecutionStatus.FailedPermanent : EffectExecutionStatus.FailedRetryable;
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE chat_admin_effect_outbox
            SET status = @status, locked_until = NULL, updated_at = now(),
                not_before = CASE WHEN @retryable THEN now() + (@retryMs * interval '1 millisecond') ELSE not_before END,
                last_error_code = @code, last_error_message = @message,
                completed_at = CASE WHEN @terminal THEN now() ELSE NULL END
            WHERE effect_id = @effectId AND status = 'executing'
            """,
            new
            {
                effectId = effect.EffectId.Value,
                status = ToDb(status),
                retryable = !terminal,
                retryMs = Math.Max((retryAfter ?? Backoff(effect.Attempt)).TotalMilliseconds, 1),
                code,
                message = Truncate(message),
                terminal,
            }, tx, cancellationToken: ct));
        if (effect.CaseId is not null)
        {
            var revocationInProgress = await IsCaseRevocationInProgressAsync(conn, tx, effect.CaseId.Value, ct);
            var caseStatus = terminal
                ? ModerationCaseStatus.Failed
                : revocationInProgress ? ModerationCaseStatus.Revoking : ModerationCaseStatus.Applying;
            await SetCaseStatusAsync(conn, tx, effect.CaseId.Value, caseStatus, message, ct);
            if (terminal && TryGetEffectTarget(effect.Payload, out var chatId, out var userId, out var correlationId))
            {
                await InsertAuditAsync(
                    conn,
                    tx,
                    chatId,
                    null,
                    userId,
                    correlationId,
                    effect.CaseId.Value,
                    $"moderation.{ActionForEffect(effect.Payload)}.failed",
                    new { code, message },
                    DateTimeOffset.UtcNow,
                    ct);
                var notification = new NotifyAdministratorsEffect(
                    chatId,
                    $"🚫 Moderation case <code>{effect.CaseId.Value}</code> завершился ошибкой. " +
                    $"Действие: <code>{ActionForEffect(effect.Payload)}</code>. Код: <code>{code}</code>.",
                    correlationId,
                    effect.EffectId.ToString());
                await InsertEffectAsync(
                    conn,
                    tx,
                    new EffectEnvelope
                    {
                        Id = EffectId.New(),
                        EffectType = EffectTypeCatalog.NotifyAdministrators,
                        Payload = notification,
                        CorrelationId = correlationId,
                        CausationId = effect.EffectId.ToString(),
                        IdempotencyKey = $"effect-failure-notification:{effect.EffectId}",
                        CreatedAt = DateTimeOffset.UtcNow,
                    },
                    EffectImportance.BestEffort,
                    null,
                    ct);
            }
        }
        await tx.CommitAsync(ct);
    }

    private async Task MarkUnknownAsAppliedAsync(StoredModerationEffect effect, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE chat_admin_effect_outbox
            SET status = 'applied', completed_at = now(), updated_at = now(), locked_until = NULL
            WHERE effect_id = @effectId AND status = 'unknown'
            """,
            new { effectId = effect.EffectId.Value }, tx, cancellationToken: ct));
        if (effect.Payload is RestrictMemberEffect restriction)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE chat_admin_members SET observed_restriction = CAST(@state AS jsonb), last_seen_at = now() WHERE chat_id = @chatId AND user_id = @userId",
                new
                {
                    chatId = restriction.ChatId.Value,
                    userId = restriction.UserId.Value,
                    state = JsonSerializer.Serialize(new RestrictionState { CanSendMessages = false, Until = restriction.Until }, JsonOptions),
                }, tx, cancellationToken: ct));
        }
        else if (effect.Payload is UnrestrictMemberEffect unrestriction)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE chat_admin_members
                SET desired_restriction = NULL, observed_restriction = NULL, last_seen_at = now()
                WHERE chat_id = @chatId AND user_id = @userId
                  AND (@expectedState IS NULL OR desired_restriction = CAST(@expectedState AS jsonb))
                """,
                new
                {
                    chatId = unrestriction.ChatId.Value,
                    userId = unrestriction.UserId.Value,
                    expectedState = ExpectedRestrictionJson(unrestriction.ExpectedUntil),
                }, tx, cancellationToken: ct));
        }
        else if (effect.Payload is DeleteMessagesEffect deleteMessages)
        {
            await DeleteIndexedMessagesAsync(conn, tx, deleteMessages, ct);
        }
        else if (effect.Payload is MarkModerationCaseRevokedEffect revoked)
        {
            await SetCaseStatusAsync(conn, tx, revoked.CaseId, ModerationCaseStatus.Revoked, "case revocation reconciled as applied", ct);
        }
        if (effect.CaseId is not null)
        {
            if (!await IsCaseRevocationInProgressAsync(conn, tx, effect.CaseId.Value, ct))
            {
                var complete = await MarkRequiredEffectAppliedAsync(conn, tx, effect, "reconciled as applied", ct);
                if (complete)
                    await InsertFollowUpLogAsync(conn, tx, effect, ct);
            }
        }
        await tx.CommitAsync(ct);
    }

    private async Task InsertFollowUpLogAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        StoredModerationEffect effect,
        CancellationToken ct)
    {
        var (chatId, userId, action, details) = effect.Payload switch
        {
            RestrictMemberEffect restriction => (restriction.ChatId, restriction.UserId, "mute", $"Until: <code>{restriction.Until:yyyy-MM-dd HH:mm} UTC</code>"),
            UnrestrictMemberEffect unrestriction => (unrestriction.ChatId, unrestriction.UserId, "unmute", string.Empty),
            BanMemberEffect ban => (ban.ChatId, ban.UserId, "ban", ban.Until is null ? "Permanent" : $"Until: <code>{ban.Until:yyyy-MM-dd HH:mm} UTC</code>"),
            UnbanMemberEffect unban => (unban.ChatId, unban.UserId, "unban", string.Empty),
            KickMemberEffect kick => (kick.ChatId, kick.UserId, "kick", string.Empty),
            DeleteMessageEffect delete => (delete.ChatId, delete.TargetUserId ?? new UserId(0), "delete", $"Message: <code>{delete.MessageId}</code>"),
            _ => throw new InvalidOperationException($"Effect '{effect.EffectType}' cannot complete a moderation case."),
        };
        var caseId = effect.CaseId!.Value;
        var text = $"🛡 Moderation case #{caseId}\nAction: {action}\nTarget user: <code>{userId}</code>\nStatus: applied\n{details}";
        var logChatId = await GetModerationLogChatIdAsync(conn, tx, chatId, ct);
        var log = new SendModerationLogEffect(logChatId, text, PayloadCorrelation(effect.Payload), effect.EffectId.ToString());
        var envelope = new EffectEnvelope
        {
            Id = EffectId.New(),
            EffectType = ModerationLogEffectType,
            Payload = log,
            CorrelationId = PayloadCorrelation(effect.Payload),
            CausationId = effect.EffectId.ToString(),
            IdempotencyKey = $"case-log:{caseId}",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        // The log is best effort. It must never change the moderation case status.
        await InsertEffectAsync(conn, tx, envelope, EffectImportance.FollowUp, null, ct);
    }

    private static async Task<ChatId> GetModerationLogChatIdAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        ChatId sourceChatId,
        CancellationToken ct)
    {
        var settingsJson = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT settings::text FROM chat_admin_chats WHERE chat_id = @chatId",
            new { chatId = sourceChatId.Value }, tx, cancellationToken: ct));
        return Deserialize(settingsJson, new ChatSettings()).ModerationLogChatId ?? sourceChatId;
    }

    private async Task UpsertMemberAsync(Npgsql.NpgsqlConnection conn, Npgsql.NpgsqlTransaction tx, MemberState member, DateTimeOffset now, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_members
                (chat_id, user_id, username, display_name, status, roles, explicit_permissions, trust_level,
                 custom_roles, desired_restriction, observed_restriction, first_seen_at, last_seen_at, joined_at, left_at)
            VALUES (@chatId, @userId, @username, @displayName, @status, CAST(@roles AS jsonb),
                    CAST(@explicitPermissions AS jsonb), @trustLevel, CAST(@customRoles AS jsonb),
                    CAST(@desired AS jsonb), CAST(@observed AS jsonb), @firstSeenAt, @lastSeenAt, @joinedAt, @leftAt)
            ON CONFLICT (chat_id, user_id) DO UPDATE SET
                username = COALESCE(EXCLUDED.username, chat_admin_members.username),
                display_name = EXCLUDED.display_name,
                status = EXCLUDED.status,
                roles = EXCLUDED.roles,
                custom_roles = EXCLUDED.custom_roles,
                explicit_permissions = EXCLUDED.explicit_permissions,
                trust_level = EXCLUDED.trust_level,
                desired_restriction = EXCLUDED.desired_restriction,
                observed_restriction = COALESCE(EXCLUDED.observed_restriction, chat_admin_members.observed_restriction),
                last_seen_at = EXCLUDED.last_seen_at,
                joined_at = COALESCE(EXCLUDED.joined_at, chat_admin_members.joined_at),
                left_at = EXCLUDED.left_at
            """,
            new
            {
                chatId = member.ChatId.Value,
                userId = member.UserId.Value,
                member.Username,
                member.DisplayName,
                status = ToDb(member.Status),
                roles = JsonSerializer.Serialize(member.Roles, JsonOptions),
                customRoles = JsonSerializer.Serialize(member.CustomRoleIds, JsonOptions),
                explicitPermissions = JsonSerializer.Serialize(member.ExplicitPermissions, JsonOptions),
                trustLevel = ToDb(member.TrustLevel),
                desired = member.DesiredRestriction is null ? null : JsonSerializer.Serialize(member.DesiredRestriction, JsonOptions),
                observed = member.ObservedRestriction is null ? null : JsonSerializer.Serialize(member.ObservedRestriction, JsonOptions),
                firstSeenAt = member.FirstSeenAt == default ? now : member.FirstSeenAt,
                lastSeenAt = now,
                joinedAt = member.JoinedAt,
                leftAt = member.LeftAt,
            }, tx, cancellationToken: ct));
    }

    private static async Task InsertWarningAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        WarningState warning,
        CancellationToken ct) =>
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_warnings
                (warning_id, chat_id, target_user_id, actor_user_id, reason, created_at, expires_at, is_active, revocation_reason)
            VALUES (@warningId, @chatId, @targetUserId, @actorUserId, @reason, @createdAt, @expiresAt, @isActive, @revocationReason)
            ON CONFLICT (warning_id) DO NOTHING
            """,
            new
            {
                warningId = warning.Id.Value,
                chatId = warning.ChatId.Value,
                targetUserId = warning.TargetUserId.Value,
                actorUserId = warning.ActorUserId?.Value,
                warning.Reason,
                createdAt = warning.CreatedAt,
                expiresAt = warning.ExpiresAt,
                warning.IsActive,
                revocationReason = warning.RevocationReason is null ? null : ToDb(warning.RevocationReason.Value),
            }, tx, cancellationToken: ct));

    private static async Task DeleteIndexedMessagesAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        DeleteMessagesEffect effect,
        CancellationToken ct) =>
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM chat_admin_message_index WHERE chat_id = @chatId AND message_id = ANY(@messageIds)",
            new { chatId = effect.ChatId.Value, messageIds = effect.MessageIds.ToArray() }, tx, cancellationToken: ct));

    private static WarningState ReadWarning(WarningRow row) => new()
    {
        Id = new WarningId(row.WarningId),
        ChatId = new ChatId(row.ChatId),
        TargetUserId = new UserId(row.TargetUserId),
        ActorUserId = row.ActorUserId is null ? null : new UserId(row.ActorUserId.Value),
        Reason = row.Reason,
        CreatedAt = UtcOffset(row.CreatedAt),
        ExpiresAt = row.ExpiresAt is null ? null : UtcOffset(row.ExpiresAt.Value),
        IsActive = row.IsActive,
        RevocationReason = Enum.TryParse<WarningRevocationReason>(row.RevocationReason, true, out var reason)
            ? reason
            : null,
    };

    private static EffectEnvelope CreateEnvelope(PlannedEffect planned, PersistModerationCommand command)
    {
        var id = planned.Id ?? EffectId.New();
        var idempotencyKey = planned.Effect switch
        {
            RestrictMemberEffect restrict => $"mute:{restrict.ChatId}:{restrict.UserId}:{restrict.CaseId}",
            UnrestrictMemberEffect unrestrictEffect => $"unmute-expiration:{unrestrictEffect.ChatId}:{unrestrictEffect.UserId}:{unrestrictEffect.CaseId}",
            BanMemberEffect ban => $"ban:{ban.ChatId}:{ban.UserId}:{ban.CaseId}",
            UnbanMemberEffect unban => $"unban:{unban.ChatId}:{unban.UserId}:{unban.CaseId}",
            KickMemberEffect kick => $"kick:{kick.ChatId}:{kick.UserId}:{kick.CaseId}",
            DeleteMessageEffect delete => $"delete-message:{delete.ChatId}:{delete.MessageId}",
            DeleteMessagesEffect delete => $"delete-messages:{delete.ChatId}:{string.Join(',', delete.MessageIds.Order())}",
            SendMessageEffect => $"response:{command.IdempotencyKey}",
            _ => $"effect:{command.IdempotencyKey}:{id}",
        };
        return new EffectEnvelope
        {
            Id = id,
            EffectType = EffectTypeOf(planned.Effect),
            Payload = planned.Effect,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            IdempotencyKey = idempotencyKey,
            CreatedAt = command.CreatedAt,
            NotBefore = planned.Effect switch
            {
                UnrestrictMemberEffect unrestrict => unrestrict.ExpectedUntil,
                UnbanMemberEffect unban => unban.ExpectedUntil,
                _ => null,
            },
            Dependencies = planned.DependsOn,
        };
    }

    private static EffectEnvelope CreateRevocationEnvelope(
        PlannedEffect planned,
        RevokeModerationCaseCommand command)
    {
        var id = planned.Id ?? EffectId.New();
        return new EffectEnvelope
        {
            Id = id,
            EffectType = EffectTypeOf(planned.Effect),
            Payload = planned.Effect,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            IdempotencyKey = $"case-revoke:{command.CaseId}:{command.CommandId}:{id}",
            CreatedAt = command.CreatedAt,
            Dependencies = planned.DependsOn,
        };
    }

    private async Task InsertEffectAsync(Npgsql.NpgsqlConnection conn, Npgsql.NpgsqlTransaction? transaction, EffectEnvelope envelope, EffectImportance importance, ModerationCaseId? caseId, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_effect_outbox
                (effect_id, tenant_key, effect_type, payload, importance, case_id, correlation_id, causation_id,
                 idempotency_key, status, attempt, maximum_attempts, created_at, not_before, dependencies, updated_at)
            VALUES (@effectId, @tenantKey, @effectType, CAST(@payload AS jsonb), @importance, @caseId, @correlationId, @causationId,
                    @idempotencyKey, 'pending', 0, 8, @createdAt, @notBefore, CAST(@dependencies AS jsonb), @createdAt)
            ON CONFLICT (idempotency_key) DO NOTHING
            """,
            new
            {
                effectId = envelope.Id.Value,
                tenantKey,
                effectType = envelope.EffectType,
                payload = SerializeEffectPayload(envelope.Payload),
                importance = ToDb(importance),
                caseId = caseId?.Value,
                envelope.CorrelationId,
                envelope.CausationId,
                idempotencyKey = $"{tenantKey}:{envelope.IdempotencyKey}",
                envelope.CreatedAt,
                notBefore = envelope.NotBefore ?? envelope.CreatedAt,
                dependencies = JsonSerializer.Serialize(envelope.Dependencies.Select(id => id.Value), JsonOptions),
            }, transaction, cancellationToken: ct));
    }

    private static async Task AppendCaseHistoryAsync(Npgsql.NpgsqlConnection conn, Npgsql.NpgsqlTransaction tx, ModerationCaseId caseId, ModerationCaseStatus status, string reason, DateTimeOffset at, CancellationToken ct) =>
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO chat_admin_case_history (case_id, status, reason, created_at) VALUES (@caseId, @status, @reason, @createdAt)",
            new { caseId = caseId.Value, status = ToDb(status), reason, createdAt = at }, tx, cancellationToken: ct));

    private static async Task<bool> InsertCommandAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        string commandId,
        string idempotencyKey,
        ModerationCaseId? caseId,
        string responseText,
        DateTimeOffset createdAt,
        CancellationToken ct)
    {
        var inserted = await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_commands (command_id, idempotency_key, case_id, response_text, created_at)
            VALUES (@commandId, @idempotencyKey, @caseId, @responseText, @createdAt)
            ON CONFLICT (command_id) DO NOTHING
            """,
            new
            {
                commandId,
                idempotencyKey,
                caseId = caseId?.Value,
                responseText,
                createdAt,
            }, tx, cancellationToken: ct));
        return inserted != 0;
    }

    private static async Task EnsureChatRowAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        ChatId chatId,
        DateTimeOffset at,
        CancellationToken ct) =>
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_chats (chat_id, chat_type, title, is_enabled, settings, created_at, updated_at)
            VALUES (@chatId, 'supergroup', 'Telegram chat', true, '{}'::jsonb, @at, @at)
            ON CONFLICT (chat_id) DO UPDATE SET updated_at = EXCLUDED.updated_at
            """,
            new { chatId = chatId.Value, at }, tx, cancellationToken: ct));

    private static async Task InsertLifecycleEventsAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        ChatId chatId,
        IReadOnlyCollection<IDomainEvent> events,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
                new
                {
                    aggregateId = Guid.NewGuid(),
                    eventType = domainEvent.EventType,
                    payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    occurredAt,
                }, tx, cancellationToken: ct));
        }
    }

    private static EffectEnvelope CreateLifecycleEnvelope(PlannedEffect planned, MemberJoinedCommand command)
    {
        var id = planned.Id ?? EffectId.New();
        return new EffectEnvelope
        {
            Id = id,
            EffectType = EffectTypeOf(planned.Effect),
            Payload = planned.Effect,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            IdempotencyKey = $"lifecycle:{command.IdempotencyKey}:{id}",
            CreatedAt = command.CreatedAt,
            Dependencies = planned.DependsOn,
        };
    }

    private static EffectEnvelope CreateLifecycleEnvelope(PlannedEffect planned, MemberLeftCommand command)
    {
        var id = planned.Id ?? EffectId.New();
        return new EffectEnvelope
        {
            Id = id,
            EffectType = EffectTypeOf(planned.Effect),
            Payload = planned.Effect,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            IdempotencyKey = $"lifecycle:{command.IdempotencyKey}:{id}",
            CreatedAt = command.CreatedAt,
            Dependencies = planned.DependsOn,
        };
    }

    private static async Task InsertDomainEventsAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        ModerationCaseId caseId,
        IReadOnlyList<IDomainEvent> events,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
                new
                {
                    aggregateId = caseId.Value,
                    eventType = domainEvent.EventType,
                    payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    occurredAt,
                }, tx, cancellationToken: ct));
        }
    }

    private static async Task SetCaseStatusAsync(Npgsql.NpgsqlConnection conn, Npgsql.NpgsqlTransaction tx, ModerationCaseId caseId, ModerationCaseStatus status, string reason, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE chat_admin_cases SET status = @status, updated_at = now() WHERE case_id = @caseId",
            new { caseId = caseId.Value, status = ToDb(status) }, tx, cancellationToken: ct));
        await AppendCaseHistoryAsync(conn, tx, caseId, status, reason, DateTimeOffset.UtcNow, ct);
    }

    private static async Task<bool> MarkRequiredEffectAppliedAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        StoredModerationEffect effect,
        string reason,
        CancellationToken ct)
    {
        var pendingRequired = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM chat_admin_effect_outbox
            WHERE case_id = @caseId AND importance = 'required'
              AND status NOT IN ('applied', 'cancelled')
            """,
            new { caseId = effect.CaseId!.Value }, tx, cancellationToken: ct));
        var status = pendingRequired == 0
            ? ModerationCaseStatus.Applied
            : ModerationCaseStatus.PartiallyApplied;
        await SetCaseStatusAsync(conn, tx, effect.CaseId.Value, status, reason, ct);
        return pendingRequired == 0;
    }

    private static async Task<bool> IsCaseRevocationInProgressAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        ModerationCaseId caseId,
        CancellationToken ct)
    {
        var status = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT status FROM chat_admin_cases WHERE case_id = @caseId",
            new { caseId = caseId.Value }, tx, cancellationToken: ct));
        return string.Equals(status, ToDb(ModerationCaseStatus.Revoking), StringComparison.Ordinal)
            || string.Equals(status, ToDb(ModerationCaseStatus.RevocationUnknown), StringComparison.Ordinal);
    }

    private static async Task InsertAuditAsync(Npgsql.NpgsqlConnection conn, Npgsql.NpgsqlTransaction tx, ChatId chatId, UserId? actor, UserId? target, string correlationId, ModerationCaseId? caseId, string action, object metadata, DateTimeOffset at, CancellationToken ct) =>
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO chat_admin_audit_events
                (chat_id, actor_user_id, target_user_id, action, correlation_id, case_id, metadata, created_at)
            VALUES (@chatId, @actorUserId, @targetUserId, @action, @correlationId, @caseId, CAST(@metadata AS jsonb), @createdAt)
            """,
            new
            {
                chatId = chatId.Value,
                actorUserId = actor?.Value,
                targetUserId = target?.Value,
                action,
                correlationId,
                caseId = caseId?.Value,
                metadata = JsonSerializer.Serialize(metadata, JsonOptions),
                createdAt = at,
            }, tx, cancellationToken: ct));

    private static async Task AppendStandaloneDomainEventAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        IDomainEvent domainEvent,
        DateTimeOffset occurredAt,
        CancellationToken ct) =>
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO chat_admin_domain_events (aggregate_id, event_type, payload, occurred_at) VALUES (@aggregateId, @eventType, CAST(@payload AS jsonb), @occurredAt)",
            new
            {
                aggregateId = Guid.NewGuid(),
                eventType = domainEvent.EventType,
                payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                occurredAt,
            }, tx, cancellationToken: ct));

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (schemaReady) return;
        await schemaGate.WaitAsync(ct);
        try
        {
            if (schemaReady) return;
            await using var conn = await connections.OpenAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(ChatAdministrationSchema.Sql, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(ChatAdministrationSchema.TenantIsolationSql, cancellationToken: ct));
            schemaReady = true;
        }
        finally
        {
            schemaGate.Release();
        }
    }

    private static MemberState MergeMember(MemberRow? row, int activeWarningCount, ChatId chatId, UserId userId, ChatMemberRole observedRole, string displayName) =>
        new()
        {
            ChatId = chatId,
            UserId = userId,
            Username = row?.Username,
            DisplayName = string.IsNullOrWhiteSpace(row?.DisplayName) ? displayName : row!.DisplayName,
            Status = Enum.TryParse<MemberStatus>(row?.Status, true, out var status) ? status : MemberStatus.Active,
            Roles = MergeRoles(Deserialize(row?.RolesJson, new HashSet<ChatMemberRole>()), observedRole),
            CustomRoleIds = Deserialize(row?.CustomRolesJson, new HashSet<RoleId>()),
            ExplicitPermissions = Deserialize(row?.ExplicitPermissionsJson, new HashSet<Permission>()),
            TrustLevel = Enum.TryParse<TrustLevel>(row?.TrustLevel, true, out var trustLevel) ? trustLevel : TrustLevel.Unknown,
            ActiveWarningCount = activeWarningCount,
            DesiredRestriction = DeserializeNullable<RestrictionState>(row?.DesiredRestrictionJson),
            ObservedRestriction = DeserializeNullable<RestrictionState>(row?.ObservedRestrictionJson),
            FirstSeenAt = row is null ? DateTimeOffset.UtcNow : UtcOffset(row.FirstSeenAt),
            LastSeenAt = DateTimeOffset.UtcNow,
            JoinedAt = row?.JoinedAt is { } joinedAt ? UtcOffset(joinedAt) : null,
            LeftAt = row?.LeftAt is { } leftAt ? UtcOffset(leftAt) : null,
        };

    private static ChatState ReadChat(ChatRow row) => new()
    {
        Id = new ChatId(row.ChatId),
        Type = ParseChatType(row.ChatType),
        Title = row.Title,
        IsEnabled = row.IsEnabled,
        Settings = Deserialize(row.SettingsJson, new ChatSettings()),
        ObservedBotPermissions = Deserialize(row.BotPermissionsJson, new TelegramBotPermissions()),
        CreatedAt = UtcOffset(row.CreatedAt),
        UpdatedAt = UtcOffset(row.UpdatedAt),
    };

    private static Guid StableAggregateId(ChatId chatId)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(chatId.Value).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static Guid StableMemberAggregateId(ChatId chatId, UserId userId)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(chatId.Value).CopyTo(bytes, 0);
        BitConverter.GetBytes(userId.Value).CopyTo(bytes, sizeof(long));
        return new Guid(bytes);
    }

    private static IReadOnlySet<ChatMemberRole> MergeRoles(IReadOnlySet<ChatMemberRole> roles, ChatMemberRole observedRole)
    {
        var result = roles.ToHashSet();
        if (observedRole is ChatMemberRole.Owner or ChatMemberRole.Admin)
            result.Add(observedRole);
        return result;
    }

    private static ChatState NewChat(ChatId id) => new()
    {
        Id = id,
        Type = ChatType.Supergroup,
        Title = "Telegram chat",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static T Deserialize<T>(string? json, T fallback) =>
        string.IsNullOrWhiteSpace(json) ? fallback : JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;

    private static T? DeserializeNullable<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);

    private static ModerationCaseState ReadCase(CaseRow row) => new()
    {
        Id = new ModerationCaseId(row.CaseId),
        ChatId = new ChatId(row.ChatId),
        TargetUserId = new UserId(row.TargetUserId),
        ActorUserId = row.ActorUserId is null ? null : new UserId(row.ActorUserId.Value),
        ActorType = Enum.TryParse<ModerationActorType>(row.ActorType, true, out var actorType)
            ? actorType
            : ModerationActorType.Human,
        Action = Enum.TryParse<ModerationAction>(row.Action, true, out var action)
            ? action
            : ModerationAction.Delete,
        Reason = row.Reason,
        SourceMessageId = row.SourceMessageId,
        SourceRuleId = row.SourceRuleId is null ? null : new RuleId(row.SourceRuleId),
        CreatedAt = UtcOffset(row.CreatedAt),
        ExpiresAt = row.ExpiresAt is null ? null : UtcOffset(row.ExpiresAt.Value),
        Status = Enum.TryParse<ModerationCaseStatus>(row.Status, true, out var status)
            ? status
            : ModerationCaseStatus.Failed,
        CorrelationId = row.CorrelationId,
    };

    private static AppealState ReadAppeal(AppealRow row) => new()
    {
        Id = new AppealId(row.AppealId),
        CaseId = new ModerationCaseId(row.CaseId),
        AuthorUserId = new UserId(row.AuthorUserId),
        Text = row.Text,
        Status = Enum.TryParse<AppealStatus>(row.Status, true, out var status) ? status : AppealStatus.Open,
        ResolvedBy = row.ResolvedBy is null ? null : new UserId(row.ResolvedBy.Value),
        ResolutionComment = row.ResolutionComment,
        CreatedAt = UtcOffset(row.CreatedAt),
        ResolvedAt = row.ResolvedAt is null ? null : UtcOffset(row.ResolvedAt.Value),
    };

    private static VerificationSession ReadVerification(VerificationRow row) => new()
    {
        Id = new VerificationSessionId(row.SessionId),
        ChatId = new ChatId(row.ChatId),
        UserId = new UserId(row.UserId),
        Status = Enum.TryParse<VerificationStatus>(row.Status, true, out var status) ? status : VerificationStatus.Pending,
        ChallengeType = row.ChallengeType,
        CorrectAnswer = row.CorrectAnswer,
        Options = Deserialize(row.OptionsJson, Array.Empty<string>()),
        Attempts = row.Attempts,
        MaximumAttempts = row.MaximumAttempts,
        CreatedAt = UtcOffset(row.CreatedAt),
        ExpiresAt = UtcOffset(row.ExpiresAt),
        ChallengeMessageId = row.ChallengeMessageId,
    };

    private static DateTimeOffset UtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static ChatType ParseChatType(string value) => Enum.TryParse<ChatType>(value, true, out var result) ? result : ChatType.Supergroup;
    private static string EffectTypeOf(IModerationEffect effect) => effect switch
    {
        RestrictMemberEffect => RestrictEffectType,
        UnrestrictMemberEffect => UnrestrictEffectType,
        BanMemberEffect => BanEffectType,
        UnbanMemberEffect => UnbanEffectType,
        KickMemberEffect => KickEffectType,
        DeleteMessageEffect => DeleteEffectType,
        DeleteMessagesEffect => DeleteMessagesEffectType,
        EditMessageEffect => EffectTypeCatalog.EditMessage,
        PinMessageEffect => EffectTypeCatalog.PinMessage,
        UnpinMessageEffect => EffectTypeCatalog.UnpinMessage,
        SendModerationLogEffect => ModerationLogEffectType,
        GetChatMemberEffect => EffectTypeCatalog.GetChatMember,
        GetChatAdministratorsEffect => EffectTypeCatalog.GetChatAdministrators,
        GetBotPermissionsEffect => EffectTypeCatalog.GetBotPermissions,
        ScheduleEffect => EffectTypeCatalog.Schedule,
        CancelScheduledEffect => EffectTypeCatalog.CancelScheduled,
        PersistAggregateEffect => EffectTypeCatalog.PersistAggregate,
        AppendDomainEventsEffect => EffectTypeCatalog.AppendDomainEvents,
        CreateModerationCaseEffect => EffectTypeCatalog.CreateModerationCase,
        UpdateModerationCaseEffect => EffectTypeCatalog.UpdateModerationCase,
        SaveVerificationSessionEffect => EffectTypeCatalog.SaveVerificationSession,
        WriteAuditEventEffect => EffectTypeCatalog.WriteAuditEvent,
        MarkModerationCaseRevokedEffect => MarkCaseRevokedEffectType,
        EmitMetricEffect => EmitMetricEffectType,
        EmitTraceEventEffect => EffectTypeCatalog.EmitTraceEvent,
        WriteStructuredLogEffect => EffectTypeCatalog.WriteStructuredLog,
        NotifyAdministratorsEffect => EffectTypeCatalog.NotifyAdministrators,
        AnswerCallbackQueryEffect => AnswerCallbackEffectType,
        SendMessageEffect => SendMessageEffectType,
        _ => throw new InvalidOperationException($"Unknown moderation effect '{effect.GetType().Name}'."),
    };

    private static EffectEnvelope CreateMetricEnvelope(EmitMetricEffect metric, PersistModerationCommand command) => new()
    {
        Id = EffectId.New(),
        EffectType = EmitMetricEffectType,
        Payload = metric,
        CorrelationId = metric.CorrelationId,
        CausationId = metric.CausationId,
        IdempotencyKey = $"metric:case-created:{command.CommandId}",
        CreatedAt = command.CreatedAt,
    };
    private static bool IsCaseApplyingEffect(PlannedEffect planned) => planned.Effect switch
    {
        RestrictMemberEffect or BanMemberEffect or KickMemberEffect or DeleteMessageEffect => true,
        UnrestrictMemberEffect or UnbanMemberEffect => planned.DependsOn.Count == 0,
        _ => false,
    };

    private static EffectEnvelope CreateAuxiliaryEnvelope(
        PlannedEffect planned,
        VerificationSession session,
        string correlationId,
        string causationId)
    {
        var id = planned.Id ?? EffectId.New();
        var idempotencyKey = planned.Effect switch
        {
            RestrictMemberEffect => $"captcha-restrict:{session.Id}",
            UnrestrictMemberEffect => $"captcha-unrestrict:{session.Id}",
            BanMemberEffect => $"captcha-ban:{session.Id}",
            KickMemberEffect => $"captcha-kick:{session.Id}",
            DeleteMessageEffect delete => $"captcha-delete:{session.Id}:{delete.MessageId}",
            AnswerCallbackQueryEffect answer => $"captcha-answer:{answer.CallbackQueryId}",
            SendMessageEffect => $"captcha-message:{session.Id}",
            _ => $"captcha-effect:{session.Id}:{id}",
        };
        return new EffectEnvelope
        {
            Id = id,
            EffectType = EffectTypeOf(planned.Effect),
            Payload = planned.Effect,
            CorrelationId = correlationId,
            CausationId = causationId,
            IdempotencyKey = idempotencyKey,
            CreatedAt = session.CreatedAt,
            NotBefore = planned.Effect switch
            {
                UnrestrictMemberEffect unrestrict => unrestrict.ExpectedUntil,
                UnbanMemberEffect unban => unban.ExpectedUntil,
                _ => null,
            },
            Dependencies = planned.DependsOn,
        };
    }
    private static string PayloadCorrelation(IModerationEffect effect) => effect switch
    {
        RestrictMemberEffect value => value.CorrelationId,
        UnrestrictMemberEffect value => value.CorrelationId,
        BanMemberEffect value => value.CorrelationId,
        UnbanMemberEffect value => value.CorrelationId,
        KickMemberEffect value => value.CorrelationId,
        DeleteMessageEffect value => value.CorrelationId,
        DeleteMessagesEffect value => value.CorrelationId,
        EditMessageEffect value => value.CorrelationId,
        PinMessageEffect value => value.CorrelationId,
        UnpinMessageEffect value => value.CorrelationId,
        SendModerationLogEffect value => value.CorrelationId,
        GetChatMemberEffect value => value.CorrelationId,
        GetChatAdministratorsEffect value => value.CorrelationId,
        GetBotPermissionsEffect value => value.CorrelationId,
        ScheduleEffect value => value.CorrelationId,
        CancelScheduledEffect value => value.CorrelationId,
        PersistAggregateEffect value => value.CorrelationId,
        AppendDomainEventsEffect value => value.CorrelationId,
        CreateModerationCaseEffect value => value.CorrelationId,
        UpdateModerationCaseEffect value => value.CorrelationId,
        SaveVerificationSessionEffect value => value.CorrelationId,
        WriteAuditEventEffect value => value.CorrelationId,
        MarkModerationCaseRevokedEffect value => value.CorrelationId,
        EmitMetricEffect value => value.CorrelationId,
        EmitTraceEventEffect value => value.CorrelationId,
        WriteStructuredLogEffect value => value.CorrelationId,
        NotifyAdministratorsEffect value => value.CorrelationId,
        _ => string.Empty,
    };
    private static string ActionForEffect(IModerationEffect effect) => effect switch
    {
        RestrictMemberEffect => "mute",
        UnrestrictMemberEffect => "unmute",
        BanMemberEffect => "ban",
        UnbanMemberEffect => "unban",
        KickMemberEffect => "kick",
        DeleteMessageEffect => "delete",
        DeleteMessagesEffect => "purge",
        MarkModerationCaseRevokedEffect => "case.revoke",
        _ => "moderation",
    };
    private static bool TryGetEffectTarget(IModerationEffect effect, out ChatId chatId, out UserId userId, out string correlationId)
    {
        switch (effect)
        {
            case RestrictMemberEffect value:
                (chatId, userId, correlationId) = (value.ChatId, value.UserId, value.CorrelationId);
                return true;
            case UnrestrictMemberEffect value:
                (chatId, userId, correlationId) = (value.ChatId, value.UserId, value.CorrelationId);
                return true;
            case BanMemberEffect value:
                (chatId, userId, correlationId) = (value.ChatId, value.UserId, value.CorrelationId);
                return true;
            case UnbanMemberEffect value:
                (chatId, userId, correlationId) = (value.ChatId, value.UserId, value.CorrelationId);
                return true;
            case KickMemberEffect value:
                (chatId, userId, correlationId) = (value.ChatId, value.UserId, value.CorrelationId);
                return true;
            case DeleteMessageEffect value:
                (chatId, userId, correlationId) = (value.ChatId, value.TargetUserId ?? new UserId(0), value.CorrelationId);
                return true;
            default:
                chatId = default;
                userId = default;
                correlationId = string.Empty;
                return false;
        }
    }
    private static string NormalizeTenantKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "default" : value.Trim();

    private static string ToDb(Enum value) => value.ToString().ToLowerInvariant();
    private static string? ExpectedRestrictionJson(DateTimeOffset? until) => until is null
        ? null
        : JsonSerializer.Serialize(new RestrictionState { CanSendMessages = false, Until = until }, JsonOptions);
    private static TimeSpan NonNegative(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
    private static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Clamp(attempt, 1, 8))));
    private static string Truncate(string value) => value.Length <= 8000 ? value : value[..8000];

    private StoredModerationEffect ReadEffect(EffectRow row) => new(
        new EffectId(row.EffectId),
        row.EffectType,
        ReadEffectPayload(row.EffectType, row.PayloadJson),
        row.CaseId is null ? null : new ModerationCaseId(row.CaseId.Value),
        Enum.TryParse<EffectImportance>(row.Importance, true, out var importance) ? importance : EffectImportance.Required,
        row.Attempt,
        row.MaximumAttempts);

    private static IModerationEffect ReadEffectPayload(string effectType, string payloadJson) => effectType switch
    {
        RestrictEffectType => JsonSerializer.Deserialize<RestrictMemberEffect>(payloadJson, JsonOptions)!,
        UnrestrictEffectType => JsonSerializer.Deserialize<UnrestrictMemberEffect>(payloadJson, JsonOptions)!,
        BanEffectType => JsonSerializer.Deserialize<BanMemberEffect>(payloadJson, JsonOptions)!,
        UnbanEffectType => JsonSerializer.Deserialize<UnbanMemberEffect>(payloadJson, JsonOptions)!,
        KickEffectType => JsonSerializer.Deserialize<KickMemberEffect>(payloadJson, JsonOptions)!,
        DeleteEffectType => JsonSerializer.Deserialize<DeleteMessageEffect>(payloadJson, JsonOptions)!,
        DeleteMessagesEffectType => JsonSerializer.Deserialize<DeleteMessagesEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.EditMessage => JsonSerializer.Deserialize<EditMessageEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.PinMessage => JsonSerializer.Deserialize<PinMessageEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.UnpinMessage => JsonSerializer.Deserialize<UnpinMessageEffect>(payloadJson, JsonOptions)!,
        ModerationLogEffectType => JsonSerializer.Deserialize<SendModerationLogEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.GetChatMember => JsonSerializer.Deserialize<GetChatMemberEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.GetChatAdministrators => JsonSerializer.Deserialize<GetChatAdministratorsEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.GetBotPermissions => JsonSerializer.Deserialize<GetBotPermissionsEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.Schedule => ReadScheduleEffect(payloadJson),
        EffectTypeCatalog.CancelScheduled => JsonSerializer.Deserialize<CancelScheduledEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.PersistAggregate => JsonSerializer.Deserialize<PersistAggregateEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.AppendDomainEvents => JsonSerializer.Deserialize<AppendDomainEventsEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.CreateModerationCase => JsonSerializer.Deserialize<CreateModerationCaseEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.UpdateModerationCase => JsonSerializer.Deserialize<UpdateModerationCaseEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.SaveVerificationSession => JsonSerializer.Deserialize<SaveVerificationSessionEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.WriteAuditEvent => JsonSerializer.Deserialize<WriteAuditEventEffect>(payloadJson, JsonOptions)!,
        MarkCaseRevokedEffectType => JsonSerializer.Deserialize<MarkModerationCaseRevokedEffect>(payloadJson, JsonOptions)!,
        EmitMetricEffectType => JsonSerializer.Deserialize<EmitMetricEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.EmitTraceEvent => JsonSerializer.Deserialize<EmitTraceEventEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.WriteStructuredLog => JsonSerializer.Deserialize<WriteStructuredLogEffect>(payloadJson, JsonOptions)!,
        EffectTypeCatalog.NotifyAdministrators => JsonSerializer.Deserialize<NotifyAdministratorsEffect>(payloadJson, JsonOptions)!,
        AnswerCallbackEffectType => JsonSerializer.Deserialize<AnswerCallbackQueryEffect>(payloadJson, JsonOptions)!,
        SendMessageEffectType => JsonSerializer.Deserialize<SendMessageEffect>(payloadJson, JsonOptions)!,
        _ => throw new InvalidOperationException($"Unknown persisted effect type '{effectType}'."),
    };

    private static ScheduleEffect ReadScheduleEffect(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        var nested = root.GetProperty("effect");
        var nestedType = nested.GetProperty("effectType").GetString()
            ?? throw new InvalidOperationException("Scheduled effect has no nested effect type.");
        var nestedPayload = ReadEffectPayload(nestedType, nested.GetProperty("payload").GetRawText());
        var envelope = new EffectEnvelope
        {
            Id = new EffectId(nested.GetProperty("id").GetProperty("value").GetGuid()),
            EffectType = nestedType,
            Payload = nestedPayload,
            CorrelationId = nested.GetProperty("correlationId").GetString() ?? string.Empty,
            CausationId = nested.GetProperty("causationId").GetString() ?? string.Empty,
            IdempotencyKey = nested.GetProperty("idempotencyKey").GetString() ?? string.Empty,
            Status = nested.TryGetProperty("status", out var status)
                ? JsonSerializer.Deserialize<EffectExecutionStatus>(status.GetRawText(), JsonOptions)
                : EffectExecutionStatus.Pending,
            Attempt = nested.TryGetProperty("attempt", out var attempt) ? attempt.GetInt32() : 0,
            MaximumAttempts = nested.TryGetProperty("maximumAttempts", out var maximumAttempts) ? maximumAttempts.GetInt32() : 8,
            CreatedAt = nested.TryGetProperty("createdAt", out var createdAt)
                ? createdAt.GetDateTimeOffset()
                : DateTimeOffset.UtcNow,
            NotBefore = nested.TryGetProperty("notBefore", out var notBefore) && notBefore.ValueKind != JsonValueKind.Null
                ? notBefore.GetDateTimeOffset()
                : null,
            Dependencies = nested.TryGetProperty("dependencies", out var dependencies)
                ? JsonSerializer.Deserialize<EffectId[]>(dependencies.GetRawText(), JsonOptions) ?? []
                : [],
        };
        return new ScheduleEffect(
            root.GetProperty("executeAt").GetDateTimeOffset(),
            envelope,
            root.GetProperty("correlationId").GetString() ?? string.Empty,
            root.GetProperty("causationId").GetString() ?? string.Empty);
    }

    private static string SerializeEffectPayload(IModerationEffect effect) => effect switch
    {
        ScheduleEffect schedule => JsonSerializer.Serialize(new
        {
            schedule.ExecuteAt,
            schedule.CorrelationId,
            schedule.CausationId,
            Effect = new
            {
                schedule.Effect.Id,
                schedule.Effect.EffectType,
                Payload = JsonSerializer.Deserialize<JsonElement>(SerializeEffectPayload(schedule.Effect.Payload), JsonOptions),
                schedule.Effect.CorrelationId,
                schedule.Effect.CausationId,
                schedule.Effect.IdempotencyKey,
                schedule.Effect.Status,
                schedule.Effect.Attempt,
                schedule.Effect.MaximumAttempts,
                schedule.Effect.CreatedAt,
                schedule.Effect.NotBefore,
                schedule.Effect.Dependencies,
            },
        }, JsonOptions),
        _ => JsonSerializer.Serialize(effect, effect.GetType(), JsonOptions),
    };

}
