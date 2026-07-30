using System.Text.Json;
using ChatAdministration.Domain.Models;
using ChatAdministration.Telegram.Infrastructure;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;
using DomainUserId = ChatAdministration.Domain.Models.UserId;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class ChatAdministrationModel(
    INpgsqlConnectionFactory connections,
    ChatAdministrationStore moderationStore,
    IAdminAuditLog adminAudit) : PageModel
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<ChatAdministrationChatRow> Chats { get; private set; } = [];
    public ChatAdministrationSelectedChat? SelectedChat { get; private set; }
    public IReadOnlyList<ChatAdministrationCaseRow> Cases { get; private set; } = [];
    public IReadOnlyList<ChatAdministrationWarningRow> Warnings { get; private set; } = [];
    public IReadOnlyList<ChatAdministrationVerificationRow> Verifications { get; private set; } = [];
    public IReadOnlyList<ChatAdministrationAppealRow> Appeals { get; private set; } = [];
    public IReadOnlyList<ChatAdministrationMemberRow> Members { get; private set; } = [];
    public IReadOnlyList<ChatAdministrationEffectRow> Effects { get; private set; } = [];
    public IReadOnlyList<ChatAdministrationAuditRow> AuditEvents { get; private set; } = [];
    public IReadOnlyDictionary<string, int> CaseCounts { get; private set; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public string? Error { get; private set; }
    public string? Flash { get; private set; }
    public bool FlashError { get; private set; }
    public AdminSession? Actor { get; private set; }
    public bool CanManage => Actor?.Role == AdminRole.SuperAdmin;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? ChatId { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Actor = HttpContext.Session.GetAdminSession();
        if (Actor is null)
            return RedirectToPage("/Admin/Login", new { ReturnUrl = Request.Path + Request.QueryString });

        Flash = TempData["ChatAdministrationFlash"] as string;
        FlashError = TempData["ChatAdministrationFlashError"] is true;

        try
        {
            await LoadAsync(ct);
        }
        catch (Exception ex)
        {
            Error = $"ChatAdministration read model unavailable: {ex.GetType().Name}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostApplyModerationPresetAsync(
        long chatId,
        string preset,
        CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (preset is not (ModerationPresetCatalog.Default
            or ModerationPresetCatalog.Relaxed
            or ModerationPresetCatalog.Strict
            or ModerationPresetCatalog.Disabled))
        {
            SetFlash("Unknown moderation preset.", isError: true);
            return RedirectToPage(new { chatId, q = Q });
        }

        await using (var connection = await connections.OpenAsync(ct))
        {
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM chat_admin_chats WHERE chat_id = @chatId)",
                new { chatId }, cancellationToken: ct));
            if (!exists)
            {
                SetFlash($"Chat {chatId} is not registered.", isError: true);
                return RedirectToPage(new { chatId, q = Q });
            }
        }

        var domainChatId = new DomainChatId(chatId);
        var actorUserId = new DomainUserId(actor.UserId);
        var context = await moderationStore.LoadContextAsync(
            domainChatId,
            actorUserId,
            actorUserId,
            ChatMemberRole.Owner,
            ChatMemberRole.Owner,
            actor.Name,
            actor.Name,
            ct);
        if (!ModerationPresetCatalog.TryApply(preset, context.Chat.Settings, out var updated))
        {
            SetFlash("Unknown moderation preset.", isError: true);
            return RedirectToPage(new { chatId, q = Q });
        }

        var correlationId = $"admin-moderation-preset:{chatId}:{Guid.NewGuid():N}";
        await moderationStore.UpdateChatSettingsAsync(domainChatId, updated, actorUserId, correlationId, ct);
        await adminAudit.LogAsync(
            actor.UserId,
            actor.Name,
            "chat_administration.moderation_preset_applied",
            new { chatId, preset, correlationId },
            ct);

        SetFlash($"Moderation preset '{preset}' applied to chat {chatId}.", isError: false);
        return RedirectToPage(new { chatId, q = Q });
    }

    // The page intentionally loads one consistent chat snapshot through one
    // connection; keep the projections together so each section uses the same
    // selected tenant and cancellation token.
#pragma warning disable MA0051
    private async Task LoadAsync(CancellationToken ct)
    {
        var query = (Q ?? string.Empty).Trim();
        await using var connection = await connections.OpenAsync(ct);

        var chats = await connection.QueryAsync<ChatAdministrationChatRow>(new CommandDefinition(
            """
            SELECT c.chat_id AS ChatId,
                   c.chat_type AS ChatType,
                   c.title AS Title,
                   c.is_enabled AS IsEnabled,
                   COALESCE(m.member_count, 0)::int AS MemberCount,
                   COALESCE(w.active_warning_count, 0)::int AS ActiveWarningCount,
                   COALESCE(k.open_case_count, 0)::int AS OpenCaseCount,
                   COALESCE(e.pending_effect_count, 0)::int AS PendingEffectCount,
                   c.updated_at AS UpdatedAt
            FROM chat_admin_chats c
            LEFT JOIN (
                SELECT chat_id, count(*) AS member_count
                FROM chat_admin_members
                GROUP BY chat_id
            ) m ON m.chat_id = c.chat_id
            LEFT JOIN (
                SELECT chat_id, count(*) FILTER (WHERE is_active)::int AS active_warning_count
                FROM chat_admin_warnings
                GROUP BY chat_id
            ) w ON w.chat_id = c.chat_id
            LEFT JOIN (
                SELECT chat_id, count(*) FILTER (
                    WHERE status IN ('requested', 'applying', 'unknown', 'revoking', 'revocationunknown')
                )::int AS open_case_count
                FROM chat_admin_cases
                GROUP BY chat_id
            ) k ON k.chat_id = c.chat_id
            LEFT JOIN (
                SELECT c2.chat_id, count(*) FILTER (
                    WHERE e2.status IN ('pending', 'ready', 'executing', 'failedretryable', 'unknown')
                )::int AS pending_effect_count
                FROM chat_admin_effect_outbox e2
                INNER JOIN chat_admin_cases c2 ON c2.case_id = e2.case_id
                GROUP BY c2.chat_id
            ) e ON e.chat_id = c.chat_id
            WHERE @query = ''
               OR c.chat_id::text = @query
               OR c.title ILIKE '%' || @query || '%'
               OR c.chat_type ILIKE '%' || @query || '%'
            ORDER BY c.updated_at DESC, c.chat_id
            LIMIT 500
            """,
            new { query }, cancellationToken: ct));
        Chats = chats.ToList();

        if (ChatId is not { } selectedChatId)
            return;

        SelectedChat = await connection.QuerySingleOrDefaultAsync<ChatAdministrationSelectedChat>(new CommandDefinition(
            """
            SELECT chat_id AS ChatId,
                   chat_type AS ChatType,
                   title AS Title,
                   is_enabled AS IsEnabled,
                   settings::text AS SettingsJson,
                   COALESCE(bot_permissions, '{}'::jsonb)::text AS BotPermissionsJson,
                   created_at AS CreatedAt,
                   updated_at AS UpdatedAt
            FROM chat_admin_chats
            WHERE chat_id = @chatId
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));

        if (SelectedChat is null)
            return;

        var caseRows = await connection.QueryAsync<ChatAdministrationCaseRow>(new CommandDefinition(
            """
            SELECT case_id AS CaseId,
                   target_user_id AS TargetUserId,
                   actor_user_id AS ActorUserId,
                   actor_type AS ActorType,
                   action AS Action,
                   reason AS Reason,
                   status AS Status,
                   created_at AS CreatedAt,
                   expires_at AS ExpiresAt,
                   source_rule_id AS SourceRuleId
            FROM chat_admin_cases
            WHERE chat_id = @chatId
            ORDER BY created_at DESC
            LIMIT 100
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));
        Cases = caseRows.ToList();

        var warningRows = await connection.QueryAsync<ChatAdministrationWarningRow>(new CommandDefinition(
            """
            SELECT warning_id AS WarningId,
                   target_user_id AS TargetUserId,
                   actor_user_id AS ActorUserId,
                   reason AS Reason,
                   is_active AS IsActive,
                   created_at AS CreatedAt,
                   expires_at AS ExpiresAt,
                   revocation_reason AS RevocationReason
            FROM chat_admin_warnings
            WHERE chat_id = @chatId
            ORDER BY created_at DESC
            LIMIT 100
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));
        Warnings = warningRows.ToList();

        var verificationRows = await connection.QueryAsync<ChatAdministrationVerificationRow>(new CommandDefinition(
            """
            SELECT session_id AS SessionId,
                   user_id AS UserId,
                   status AS Status,
                   challenge_type AS ChallengeType,
                   attempts AS Attempts,
                   maximum_attempts AS MaximumAttempts,
                   created_at AS CreatedAt,
                   expires_at AS ExpiresAt,
                   challenge_message_id AS ChallengeMessageId
            FROM chat_admin_verifications
            WHERE chat_id = @chatId
            ORDER BY created_at DESC
            LIMIT 100
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));
        Verifications = verificationRows.ToList();

        var appealRows = await connection.QueryAsync<ChatAdministrationAppealRow>(new CommandDefinition(
            """
            SELECT appeal_id AS AppealId,
                   case_id AS CaseId,
                   author_user_id AS AuthorUserId,
                   text AS Text,
                   status AS Status,
                   resolved_by AS ResolvedBy,
                   resolution_comment AS ResolutionComment,
                   created_at AS CreatedAt,
                   resolved_at AS ResolvedAt
            FROM chat_admin_appeals
            WHERE chat_id = @chatId
            ORDER BY created_at DESC
            LIMIT 100
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));
        Appeals = appealRows.ToList();

        var memberRows = await connection.QueryAsync<ChatAdministrationMemberRow>(new CommandDefinition(
            """
            SELECT user_id AS UserId,
                   username AS Username,
                   display_name AS DisplayName,
                   status AS Status,
                   roles::text AS RolesJson,
                   trust_level AS TrustLevel,
                   desired_restriction::text AS DesiredRestrictionJson,
                   observed_restriction::text AS ObservedRestrictionJson,
                   last_seen_at AS LastSeenAt
            FROM chat_admin_members
            WHERE chat_id = @chatId
            ORDER BY last_seen_at DESC
            LIMIT 200
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));
        Members = memberRows.ToList();

        var effectRows = await connection.QueryAsync<ChatAdministrationEffectRow>(new CommandDefinition(
            """
            SELECT e.effect_id AS EffectId,
                   e.effect_type AS EffectType,
                   e.importance AS Importance,
                   e.status AS Status,
                   e.attempt AS Attempt,
                   e.created_at AS CreatedAt,
                   e.not_before AS NotBefore,
                   e.last_error_code AS LastErrorCode,
                   e.last_error_message AS LastErrorMessage
            FROM chat_admin_effect_outbox e
            INNER JOIN chat_admin_cases c ON c.case_id = e.case_id
            WHERE c.chat_id = @chatId
            ORDER BY e.created_at DESC
            LIMIT 100
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));
        Effects = effectRows.ToList();

        var auditRows = await connection.QueryAsync<ChatAdministrationAuditRow>(new CommandDefinition(
            """
            SELECT id AS Id,
                   actor_user_id AS ActorUserId,
                   target_user_id AS TargetUserId,
                   action AS Action,
                   correlation_id AS CorrelationId,
                   case_id AS CaseId,
                   metadata::text AS MetadataJson,
                   created_at AS CreatedAt
            FROM chat_admin_audit_events
            WHERE chat_id = @chatId
            ORDER BY created_at DESC
            LIMIT 100
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));
        AuditEvents = auditRows.ToList();

        var caseCounts = await connection.QueryAsync<(string Action, int Count)>(new CommandDefinition(
            """
            SELECT action AS Action, count(*)::int AS Count
            FROM chat_admin_cases
            WHERE chat_id = @chatId
            GROUP BY action
            ORDER BY count(*) DESC, action
            """,
            new { chatId = selectedChatId }, cancellationToken: ct));
        CaseCounts = caseCounts.ToDictionary(x => x.Action, x => x.Count, StringComparer.OrdinalIgnoreCase);
    }
#pragma warning restore MA0051

    public static string PrettyJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "{}";

        try
        {
            return JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json), PrettyJsonOptions);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    public static string Truncate(string? value, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    private void SetFlash(string message, bool isError)
    {
        TempData["ChatAdministrationFlash"] = message;
        TempData["ChatAdministrationFlashError"] = isError;
    }
}
