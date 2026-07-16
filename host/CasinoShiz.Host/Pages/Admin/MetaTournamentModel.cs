using Dapper;
using BotFramework.Host.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class MetaTournamentModel(
    INpgsqlConnectionFactory connections,
    IDurableWorkflowReplayService replay,
    IAdminAuditLog audit) : PageModel
{
    public MetaTournamentDetail? Tournament { get; private set; }
    public IReadOnlyList<MetaTournamentPlayerRow> Players { get; private set; } = [];
    public IReadOnlyList<MetaTournamentMatchRow> Matches { get; private set; } = [];
    public IReadOnlyList<MetaTournamentWorkflowStepRow> WorkflowSteps { get; private set; } = [];
    public string? Flash { get; private set; }
    public bool FlashError { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null) return RedirectToPage("/Admin/Login");

        Flash = TempData["TournamentFlash"] as string;
        FlashError = TempData["TournamentFlashError"] is true;

        const string tournamentSql = """
            SELECT t.id AS Id,
                   t.season_id AS SeasonId,
                   s.name AS SeasonName,
                   t.chat_id AS ChatId,
                   t.game_key AS GameKey,
                   t.type AS Type,
                   t.status AS Status,
                   t.entry_fee AS EntryFee,
                   t.max_players AS MaxPlayers,
                   t.created_by AS CreatedBy,
                   t.created_at AS CreatedAt,
                   count(p.user_id)::int AS PlayerCount,
                   (count(p.user_id) * t.entry_fee)::bigint AS PrizePool
            FROM meta_tournaments t
            JOIN meta_seasons s ON s.id = t.season_id
            LEFT JOIN meta_tournament_players p ON p.tournament_id = t.id AND p.status IN ('joined', 'winner', 'eliminated')
            WHERE t.id = @id
            GROUP BY t.id, t.season_id, s.name, t.chat_id, t.game_key, t.type, t.status, t.entry_fee, t.max_players, t.created_by, t.created_at
            LIMIT 1
            """;

        const string playersSql = """
            SELECT tournament_id AS TournamentId,
                   user_id AS UserId,
                   display_name AS DisplayName,
                   status AS Status,
                   joined_at AS JoinedAt
            FROM meta_tournament_players
            WHERE tournament_id = @id
            ORDER BY joined_at ASC
            """;

        const string matchesSql = """
            SELECT id AS Id,
                   tournament_id AS TournamentId,
                   round AS Round,
                   match_index AS MatchIndex,
                   status AS Status,
                   player1_user_id AS Player1UserId,
                   player1_display_name AS Player1DisplayName,
                   player2_user_id AS Player2UserId,
                   player2_display_name AS Player2DisplayName,
                   victor_user_id AS VictorUserId,
                   created_at AS CreatedAt,
                   updated_at AS UpdatedAt
            FROM meta_tournament_matches
            WHERE tournament_id = @id
            ORDER BY round ASC, match_index ASC
            """;

        const string workflowSql = """
            SELECT id AS Id,
                   workflow_id AS WorkflowId,
                   command_id AS CommandId,
                   command_type AS CommandType,
                   aggregate_id AS AggregateId,
                   operation AS Operation,
                   status AS Status,
                   terminal AS Terminal,
                   causation_id AS CausationId,
                   command_json::text AS CommandJson,
                   payload::text AS PayloadJson,
                   error AS Error,
                   occurred_at AS OccurredAt
            FROM durable_workflow_steps
            WHERE aggregate_id = CAST(@id AS text)
               OR workflow_id = ('tournament:' || CAST(@id AS text))
            ORDER BY occurred_at DESC, id DESC
            LIMIT 100
            """;

        await using var conn = await connections.OpenAsync(ct);
        Tournament = await conn.QuerySingleOrDefaultAsync<MetaTournamentDetail>(new CommandDefinition(tournamentSql, new { id }, cancellationToken: ct));
        if (Tournament is null) return NotFound();

        Players = (await conn.QueryAsync<MetaTournamentPlayerRow>(new CommandDefinition(playersSql, new { id }, cancellationToken: ct))).ToList();
        Matches = (await conn.QueryAsync<MetaTournamentMatchRow>(new CommandDefinition(matchesSql, new { id }, cancellationToken: ct))).ToList();
        WorkflowSteps = (await conn.QueryAsync<MetaTournamentWorkflowStepRow>(new CommandDefinition(workflowSql, new { id }, cancellationToken: ct))).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostReplayAsync(long id, long tournamentId, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        try
        {
            var result = await replay.ReplayAsync(id, ct);
            await audit.LogAsync(
                actor.UserId,
                actor.Name,
                "meta_tournament.workflow_replay",
                new { stepId = id, tournamentId, enqueued = result.Enqueued, result.Message },
                ct);
            TempData["TournamentFlash"] = result.Message;
            TempData["TournamentFlashError"] = !result.Enqueued;
        }
        catch (Exception exception)
        {
            await audit.LogAsync(
                actor.UserId,
                actor.Name,
                "meta_tournament.workflow_replay",
                new { stepId = id, tournamentId, enqueued = false, error = exception.Message },
                ct);
            TempData["TournamentFlash"] = $"Replay не отправлен: {exception.Message}";
            TempData["TournamentFlashError"] = true;
        }

        return RedirectToPage(new { id = tournamentId });
    }
}

public sealed record MetaTournamentWorkflowStepRow(
    long Id,
    string WorkflowId,
    string CommandId,
    string CommandType,
    string? AggregateId,
    string Operation,
    string Status,
    bool Terminal,
    string? CausationId,
    string CommandJson,
    string PayloadJson,
    string? Error,
    DateTimeOffset OccurredAt);
