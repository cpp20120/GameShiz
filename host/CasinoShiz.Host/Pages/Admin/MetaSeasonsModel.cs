using BotFramework.Host;
using BotFramework.Sdk;
using Dapper;
using Games.Meta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class MetaSeasonsModel(
    INpgsqlConnectionFactory connections,
    ISeasonRewardService rewards,
    IAdminAuditLog audit,
    IMetaHistoryStore history) : PageModel
{
    public IReadOnlyList<MetaSeasonAdminRow> Seasons { get; private set; } = [];
    public AdminSession? Actor { get; private set; }
    public bool CanEdit { get; private set; }

    public string? Flash { get; set; }
    public bool FlashError { get; set; }

    [BindProperty] public string Name { get; set; } = "Season";
    [BindProperty] public int DurationDays { get; set; } = SeasonPlanFactory.DefaultDurationDays;
    [BindProperty] public string ConfigJson { get; set; } = SeasonPlanFactory.DefaultConfigJson;
    [BindProperty] public int PrepareCount { get; set; } = SeasonPlanFactory.DefaultPreparedSeasonCount;
    [BindProperty] public int PrepareDurationDays { get; set; } = SeasonPlanFactory.DefaultDurationDays;

    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Actor = HttpContext.Session.GetAdminSession();
        if (Actor is null) return RedirectToPage("/Admin/Login");

        CanEdit = Actor.Role == AdminRole.SuperAdmin;
        Flash = TempData["Flash"] as string;
        FlashError = TempData["FlashError"] is not null;
        ConfigJson = SeasonPlanFactory.DefaultConfigJson;
        await LoadAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        Name = string.IsNullOrWhiteSpace(Name) ? "Season" : Name.Trim();
        DurationDays = Math.Clamp(DurationDays, 1, 365);
        if (!IsJsonObject(ConfigJson))
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = "Config must be a valid JSON object.";
            return RedirectToPage();
        }

        const string sql = """
            INSERT INTO meta_seasons (name, starts_at, ends_at, status, config)
            VALUES (@name, @startsAt, @endsAt, 'planned', CAST(@configJson AS jsonb))
            RETURNING id
            """;

        await using var conn = await connections.OpenAsync(ct);
        var startsAt = await NextPlannedStartsAtAsync(conn, null, ct);
        var endsAt = startsAt.AddDays(DurationDays);
        var id = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            sql,
            new { name = Name, startsAt, endsAt, configJson = ConfigJson },
            cancellationToken: ct));
        await audit.LogAsync(actor.UserId, actor.Name, "meta_season.create", new { id, Name, DurationDays }, ct);
        await history.AppendAsync("season.created", "season", id.ToString(), id, null, actor.UserId, new { id, Name, DurationDays, actor = actor.Name }, ct);

        TempData["Flash"] = $"Season #{id} created as planned.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPrepareAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        PrepareCount = Math.Clamp(PrepareCount, 1, 100);
        PrepareDurationDays = Math.Clamp(PrepareDurationDays, 1, 365);

        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string countSql = """
            SELECT count(*)::int
            FROM meta_seasons
            WHERE status = 'planned'
              AND ends_at > now()
            """;
        var existingPlanned = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            countSql,
            transaction: tx,
            cancellationToken: ct));
        var toCreate = Math.Max(0, PrepareCount - existingPlanned);

        if (toCreate > 0)
        {
            var startsAt = await NextPlannedStartsAtAsync(conn, tx, ct);
            var startNumber = await NextSeasonNumberAsync(conn, tx, ct);
            var plans = SeasonPlanFactory.CreatePlans(startsAt, toCreate, PrepareDurationDays, startNumber);

            const string insertSql = """
                INSERT INTO meta_seasons (name, starts_at, ends_at, status, config)
                VALUES (@name, @startsAt, @endsAt, 'planned', CAST(@configJson AS jsonb))
                """;

            foreach (var plan in plans)
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    insertSql,
                    new
                    {
                        name = plan.Name,
                        startsAt = plan.StartsAt,
                        endsAt = plan.EndsAt,
                        configJson = plan.ConfigJson,
                    },
                    transaction: tx,
                    cancellationToken: ct));
            }
        }

        await tx.CommitAsync(ct);

        await audit.LogAsync(actor.UserId, actor.Name, "meta_season.prepare", new
        {
            requested = PrepareCount,
            existingPlanned,
            created = toCreate,
            durationDays = PrepareDurationDays,
        }, ct);
        await history.AppendAsync(
            "season.prepared",
            "season",
            "planned",
            null,
            null,
            actor.UserId,
            new
            {
                requested = PrepareCount,
                existingPlanned,
                created = toCreate,
                durationDays = PrepareDurationDays,
                actor = actor.Name,
            },
            ct);

        TempData["Flash"] = toCreate == 0
            ? $"Already have {existingPlanned} future planned seasons."
            : $"Prepared {toCreate} planned seasons ({PrepareDurationDays} days each).";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivateAsync(long seasonId, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE meta_seasons SET status = 'finished', updated_at = now() WHERE status = 'active'",
            transaction: tx,
            cancellationToken: ct));

        var changed = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE meta_seasons SET status = 'active', starts_at = LEAST(starts_at, now()), updated_at = now() WHERE id = @seasonId AND status IN ('planned', 'finished')",
            new { seasonId },
            transaction: tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);

        if (changed > 0)
        {
            await audit.LogAsync(actor.UserId, actor.Name, "meta_season.activate", new { seasonId }, ct);
            await history.AppendAsync("season.activated", "season", seasonId.ToString(), seasonId, null, actor.UserId, new { seasonId, actor = actor.Name }, ct);
            TempData["Flash"] = $"Season #{seasonId} activated.";
        }
        else
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = $"Season #{seasonId} was not activated.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostFinishAsync(long seasonId, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        const string sql = """
            UPDATE meta_seasons
            SET status = 'finished', ends_at = LEAST(ends_at, now()), updated_at = now()
            WHERE id = @seasonId AND status <> 'finished'
            """;

        await using var conn = await connections.OpenAsync(ct);
        var changed = await conn.ExecuteAsync(new CommandDefinition(sql, new { seasonId }, cancellationToken: ct));
        if (changed > 0)
        {
            await audit.LogAsync(actor.UserId, actor.Name, "meta_season.finish", new { seasonId }, ct);
            await history.AppendAsync("season.finished", "season", seasonId.ToString(), seasonId, null, actor.UserId, new { seasonId, actor = actor.Name }, ct);
            TempData["Flash"] = $"Season #{seasonId} finished.";
        }
        else
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = $"Season #{seasonId} was not changed.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPayRewardsAsync(long seasonId, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        if (!await SeasonExistsAsync(seasonId, ct))
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = $"Season #{seasonId} not found.";
            return RedirectToPage();
        }

        var result = await rewards.ProcessPlayerRewardsAsync(seasonId, ct);
        var winnerPayload = result.Rows.Select(x => new { x.Place, x.ChatId, x.UserId, x.DisplayName, x.Amount }).ToArray();
        await audit.LogAsync(actor.UserId, actor.Name, "meta_season.pay_rewards", new
        {
            seasonId,
            winners = winnerPayload,
        }, ct);
        await history.AppendAsync("season.reward_paid", "season", seasonId.ToString(), seasonId, null, actor.UserId, new { seasonId, paid = result.Paid, winners = winnerPayload, actor = actor.Name }, ct);

        TempData["Flash"] = $"Season #{seasonId} rewards processed for {result.Paid} winners.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPayClanRewardsAsync(long seasonId, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        if (!await SeasonExistsAsync(seasonId, ct))
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = $"Season #{seasonId} not found.";
            return RedirectToPage();
        }

        var result = await rewards.ProcessClanRewardsAsync(seasonId, ct);
        var winnerPayload = result.Rows.Select(x => new { x.Place, x.ChatId, x.UserId, x.DisplayName, x.Amount }).ToArray();
        await audit.LogAsync(actor.UserId, actor.Name, "meta_season.pay_clan_rewards", new
        {
            seasonId,
            winners = winnerPayload,
        }, ct);
        await history.AppendAsync("season.clan_reward_paid", "season", seasonId.ToString(), seasonId, null, actor.UserId, new { seasonId, paid = result.Paid, winners = winnerPayload, actor = actor.Name }, ct);

        TempData["Flash"] = $"Season #{seasonId} clan rewards processed for {result.Paid} clans.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateConfigAsync(long seasonId, string configJson, CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        if (!IsJsonObject(configJson))
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = "Config must be a valid JSON object.";
            return RedirectToPage();
        }

        const string sql = """
            UPDATE meta_seasons
            SET config = CAST(@configJson AS jsonb), updated_at = now()
            WHERE id = @seasonId
            """;

        await using var conn = await connections.OpenAsync(ct);
        var changed = await conn.ExecuteAsync(new CommandDefinition(sql, new { seasonId, configJson }, cancellationToken: ct));
        if (changed > 0)
        {
            await audit.LogAsync(actor.UserId, actor.Name, "meta_season.config_update", new { seasonId }, ct);
            await history.AppendAsync("season.config_updated", "season", seasonId.ToString(), seasonId, null, actor.UserId, new { seasonId, actor = actor.Name }, ct);
            TempData["Flash"] = $"Season #{seasonId} config updated.";
        }
        else
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = $"Season #{seasonId} was not found.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateStructuredConfigAsync(
        long seasonId,
        int xpPlay,
        int xpWin,
        int xpLoss,
        decimal xpStakeMultiplier,
        int xpMinPerGame,
        int xpMaxPerGame,
        bool ratingEnabled,
        int ratingStart,
        int ratingWinDelta,
        int ratingLossDelta,
        int xpPerLevelSquaredBase,
        string playerTopRewards,
        string clanTopRewards,
        string questFocus,
        string questRarityBias,
        CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin) return Forbid();

        const string selectSql = "SELECT config::text FROM meta_seasons WHERE id = @seasonId";
        const string updateSql = """
            UPDATE meta_seasons
            SET config = CAST(@configJson AS jsonb), updated_at = now()
            WHERE id = @seasonId
            """;

        await using var conn = await connections.OpenAsync(ct);
        var existing = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            selectSql,
            new { seasonId },
            cancellationToken: ct));
        if (existing is null)
        {
            TempData["FlashError"] = true;
            TempData["Flash"] = $"Season #{seasonId} was not found.";
            return RedirectToPage();
        }

        var root = ParseOrNewObject(existing);
        root["xp"] = new JsonObject
        {
            ["play"] = Math.Clamp(xpPlay, 0, 10_000),
            ["win"] = Math.Clamp(xpWin, 0, 10_000),
            ["loss"] = Math.Clamp(xpLoss, 0, 10_000),
            ["stakeMultiplier"] = Math.Clamp(xpStakeMultiplier, 0m, 10m),
            ["minXpPerGame"] = Math.Clamp(xpMinPerGame, 0, 1_000_000),
            ["maxXpPerGame"] = Math.Clamp(xpMaxPerGame, 1, 1_000_000),
        };
        root["rating"] = new JsonObject
        {
            ["enabled"] = ratingEnabled,
            ["start"] = Math.Clamp(ratingStart, 0, 1_000_000),
            ["winDelta"] = Math.Clamp(ratingWinDelta, -1_000_000, 1_000_000),
            ["lossDelta"] = Math.Clamp(ratingLossDelta, -1_000_000, 1_000_000),
        };
        root["levels"] = new JsonObject
        {
            ["xpPerLevelSquaredBase"] = Math.Clamp(xpPerLevelSquaredBase, 1, 1_000_000),
        };
        root["rewards"] = new JsonObject
        {
            ["playerTop"] = ToJsonArray(ParseRewardCsv(playerTopRewards, SeasonRewardsConfig.Default.PlayerTop)),
            ["clanTop"] = ToJsonArray(ParseRewardCsv(clanTopRewards, SeasonRewardsConfig.Default.ClanTop)),
        };
        root["quests"] = MergeObject(root["quests"], new JsonObject
        {
            ["focus"] = string.IsNullOrWhiteSpace(questFocus) ? "all-round" : questFocus.Trim(),
            ["rarityBias"] = string.IsNullOrWhiteSpace(questRarityBias) ? "normal" : questRarityBias.Trim(),
        });

        var configJson = root.ToJsonString(PrettyJson);
        await conn.ExecuteAsync(new CommandDefinition(
            updateSql,
            new { seasonId, configJson },
            cancellationToken: ct));

        await audit.LogAsync(actor.UserId, actor.Name, "meta_season.structured_config_update", new { seasonId }, ct);
        await history.AppendAsync("season.config_updated", "season", seasonId.ToString(), seasonId, null, actor.UserId, new { seasonId, actor = actor.Name, structured = true }, ct);

        TempData["Flash"] = $"Season #{seasonId} structured config updated.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id,
                   name AS Name,
                   starts_at AS StartsAt,
                   ends_at AS EndsAt,
                   status AS Status,
                   config::text AS ConfigJson,
                   created_at AS CreatedAt,
                   updated_at AS UpdatedAt
            FROM meta_seasons
            ORDER BY CASE status WHEN 'active' THEN 0 WHEN 'planned' THEN 1 ELSE 2 END, starts_at DESC, id DESC
            LIMIT 100
            """;

        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<MetaSeasonAdminRow>(new CommandDefinition(sql, cancellationToken: ct));
        Seasons = rows.ToList();
    }

    public SeasonProgressionConfig ProgressionFor(string configJson) =>
        SeasonProgressionConfig.FromJson(configJson);

    public SeasonRewardsConfig RewardsFor(string configJson) =>
        SeasonRewardsConfig.FromJson(configJson);

    public SeasonQuestRotationConfig QuestRotationFor(string configJson) =>
        SeasonQuestRotationConfig.FromJson(configJson);

    public static string RewardCsv(IReadOnlyList<int> values) =>
        string.Join(", ", values);

    private static bool IsJsonObject(string value)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(value);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> SeasonExistsAsync(long seasonId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM meta_seasons WHERE id = @seasonId)",
            new { seasonId },
            cancellationToken: ct));
    }

    private static JsonObject ParseOrNewObject(string json)
    {
        try
        {
            return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static JsonObject MergeObject(JsonNode? current, JsonObject patch)
    {
        var target = current is JsonObject currentObject ? currentObject.DeepClone().AsObject() : new JsonObject();
        foreach (var (key, value) in patch)
            target[key] = value?.DeepClone();
        return target;
    }

    private static IReadOnlyList<int> ParseRewardCsv(string value, IReadOnlyList<int> fallback)
    {
        var values = (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var parsed) ? Math.Max(0, parsed) : 0)
            .Where(x => x > 0)
            .Take(10)
            .ToArray();

        return values.Length == 0 ? fallback : values;
    }

    private static JsonArray ToJsonArray(IReadOnlyList<int> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        return array;
    }

    private static async Task<DateTimeOffset> NextPlannedStartsAtAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction? tx,
        CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(max(ends_at), date_trunc('day', now()))
            FROM meta_seasons
            WHERE status IN ('active', 'planned')
            """;

        return await conn.ExecuteScalarAsync<DateTimeOffset>(new CommandDefinition(
            sql,
            transaction: tx,
            cancellationToken: ct));
    }

    private static async Task<int> NextSeasonNumberAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        CancellationToken ct)
    {
        const string sql = "SELECT count(*)::int + 1 FROM meta_seasons";
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            transaction: tx,
            cancellationToken: ct));
    }
}
