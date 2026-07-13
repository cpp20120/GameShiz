using System.Text.Json;
using System.Text.Json.Nodes;
using BotFramework.Sdk.Configuration;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class SettingsModel(
    INpgsqlConnectionFactory connections,
    IRuntimeTuningAccessor tuning,
    IRuntimeConfigurationService runtimeConfiguration,
    ILogger<SettingsModel> logger) : PageModel
{
    [BindProperty]
    public string PatchJson { get; set; } = "{}";
    public string EffectivePreviewJson { get; set; } = "";
    public string? Error { get; set; }
    public string? Flash { get; set; }
    public bool CanEdit { get; private set; }
    [BindProperty]
    public StickerGameSettingsInput StickerGames { get; set; } = new();
    [BindProperty]
    public PokerGameSettingsInput PokerGame { get; set; } = new();
    [BindProperty]
    public PickGameSettingsInput PickGame { get; set; } = new();
    [BindProperty]
    public ChallengeSettingsInput ChallengeGame { get; set; } = new();
    [BindProperty]
    public BlackjackSettingsInput BlackjackGame { get; set; } = new();
    [BindProperty]
    public SecretHitlerSettingsInput SecretHitlerGame { get; set; } = new();
    [BindProperty]
    public MetaSettingsInput MetaGame { get; set; } = new();
    public IReadOnlyList<RedeemDropStats> DropStats { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");

        CanEdit = actor.Role == AdminRole.SuperAdmin;
        var snapshot = await runtimeConfiguration.GetAsync(ct);
        PatchJson = FormatJson(snapshot.PatchJson);
        EffectivePreviewJson = FormatJson(snapshot.EffectiveJson);
        Error = FormatIssues(snapshot.Issues);
        LoadAllStructuredSettings();
        await LoadRedeemDropStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");
        if (actor.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        var result = await runtimeConfiguration.ApplyAsync(
            PatchJson,
            actor.UserId,
            actor.Name,
            "runtime_tuning.save",
            null,
            ct);
        if (!result.Applied)
        {
            Error = FormatIssues(result.Issues);
            CanEdit = true;
            EffectivePreviewJson = FormatJson(result.EffectiveJson);
            LoadAllStructuredSettings();
            await LoadRedeemDropStatsAsync(ct);
            return Page();
        }

        Flash = "Saved. Live settings reloaded.";
        PatchJson = FormatJson(result.PatchJson);
        EffectivePreviewJson = FormatJson(result.EffectiveJson);
        LoadAllStructuredSettings();
        await LoadRedeemDropStatsAsync(ct);
        CanEdit = true;
        logger.LogInformation("runtime_tuning updated by admin {UserId}", actor.UserId);
        return Page();
    }

    public async Task<IActionResult> OnPostStickerGamesAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");
        if (actor.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (StickerGames.All.Any(g => g.DailyLimit < 0))
        {
            Error = "Daily limits must be 0 or greater.";
            return await ReloadPageForErrorAsync(ct);
        }

        if (StickerGames.All.Any(g => g.DropChance < 0 || g.DropChance > 1))
        {
            Error = "Drop chances must be between 0 and 1. Example: 0.02 = 2%.";
            return await ReloadPageForErrorAsync(ct);
        }

        var patch = await LoadPatchObjectAsync(ct);
        patch["Bot"] ??= new JsonObject();
        patch["Games"] ??= new JsonObject();
        var bot = (JsonObject)patch["Bot"]!;
        var games = (JsonObject)patch["Games"]!;

        bot["TelegramDiceDailyLimit"] ??= new JsonObject();
        var daily = (JsonObject)bot["TelegramDiceDailyLimit"]!;
        daily["MaxRollsPerUserPerDayByGame"] = new JsonObject
        {
            ["dice"] = StickerGames.DiceDailyLimit,
            ["dicecube"] = StickerGames.DiceCubeDailyLimit,
            ["darts"] = StickerGames.DartsDailyLimit,
            ["football"] = StickerGames.FootballDailyLimit,
            ["basketball"] = StickerGames.BasketballDailyLimit,
            ["bowling"] = StickerGames.BowlingDailyLimit,
        };

        SetDropChance(games, "dice", StickerGames.DiceDropChance);
        SetDropChance(games, "dicecube", StickerGames.DiceCubeDropChance);
        SetDropChance(games, "darts", StickerGames.DartsDropChance);
        SetDropChance(games, "football", StickerGames.FootballDropChance);
        SetDropChance(games, "basketball", StickerGames.BasketballDropChance);
        SetDropChance(games, "bowling", StickerGames.BowlingDropChance);

        return await SaveAndReloadAsync(actor, patch, "runtime_tuning.sticker_games.save",
            new { games = StickerGames.All.Select(g => g.GameId).ToArray() },
            "Sticker game drop chances and daily limits saved.", ct);
    }

    public async Task<IActionResult> OnPostPokerAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");
        if (actor.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (PokerGame.BuyIn <= 0)
        {
            Error = "Poker buy-in must be greater than 0.";
            return await ReloadPageForErrorAsync(ct);
        }

        var patch = await LoadPatchObjectAsync(ct);
        patch["Games"] ??= new JsonObject();
        var games = (JsonObject)patch["Games"]!;
        games["poker"] ??= new JsonObject();
        var poker = (JsonObject)games["poker"]!;
        poker["BuyIn"] = PokerGame.BuyIn;

        return await SaveAndReloadAsync(actor, patch, "runtime_tuning.poker.save",
            new { PokerGame.BuyIn }, "Poker settings saved.", ct);
    }

    public async Task<IActionResult> OnPostPickAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");
        if (actor.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (PickGame.DefaultBet <= 0 || PickGame.MaxBet < PickGame.DefaultBet)
        {
            Error = "Pick: DefaultBet must be > 0 and ≤ MaxBet.";
            return await ReloadPageForErrorAsync(ct);
        }
        if (PickGame.HouseEdge is < 0 or >= 1)
        {
            Error = "Pick: HouseEdge must be in [0, 1).";
            return await ReloadPageForErrorAsync(ct);
        }
        if (PickGame.StreakBonusPerWin < 0 || PickGame.StreakCap < 0)
        {
            Error = "Pick: streak bonus and cap must be >= 0.";
            return await ReloadPageForErrorAsync(ct);
        }
        if (PickGame.ChainMaxDepth < 0 || PickGame.ChainTtlSeconds <= 0)
        {
            Error = "Pick: ChainMaxDepth >= 0 and ChainTtlSeconds > 0 required.";
            return await ReloadPageForErrorAsync(ct);
        }
        if (PickGame.LotteryDurationSeconds <= 0 || PickGame.LotteryMinEntrants < 2)
        {
            Error = "Pick lottery: DurationSeconds > 0 and MinEntrantsToSettle >= 2 required.";
            return await ReloadPageForErrorAsync(ct);
        }
        if (PickGame.LotteryHouseFeePercent is < 0 or >= 1
            || PickGame.DailyHouseFeePercent is < 0 or >= 1)
        {
            Error = "Pick: HouseFeePercent must be in [0, 1).";
            return await ReloadPageForErrorAsync(ct);
        }
        if (PickGame.LotteryMinStake <= 0
            || (PickGame.LotteryMaxStake != 0 && PickGame.LotteryMaxStake < PickGame.LotteryMinStake))
        {
            Error = "Pick lottery: MinStake > 0 and (MaxStake == 0 or MaxStake >= MinStake).";
            return await ReloadPageForErrorAsync(ct);
        }
        if (PickGame.DailyTicketPrice <= 0
            || PickGame.DailyMaxTicketsPerBuyCommand <= 0
            || PickGame.DailyHistoryLimit <= 0)
        {
            Error = "Daily lottery: TicketPrice, MaxTicketsPerBuyCommand and HistoryLimit must be > 0.";
            return await ReloadPageForErrorAsync(ct);
        }
        if (PickGame.DailyDrawHourLocal is < 0 or > 23)
        {
            Error = "Daily lottery: DrawHourLocal must be in 0..23.";
            return await ReloadPageForErrorAsync(ct);
        }

        var patch = await LoadPatchObjectAsync(ct);
        patch["Games"] ??= new JsonObject();
        var games = (JsonObject)patch["Games"]!;
        games["pick"] ??= new JsonObject();
        var pick = (JsonObject)games["pick"]!;

        pick["DefaultBet"] = PickGame.DefaultBet;
        pick["MaxBet"] = PickGame.MaxBet;
        pick["HouseEdge"] = PickGame.HouseEdge;
        pick["StreakBonusPerWin"] = PickGame.StreakBonusPerWin;
        pick["StreakCap"] = PickGame.StreakCap;
        pick["ChainMaxDepth"] = PickGame.ChainMaxDepth;
        pick["ChainTtlSeconds"] = PickGame.ChainTtlSeconds;
        pick["RevealAnimation"] = PickGame.RevealAnimation;

        pick["Lottery"] ??= new JsonObject();
        var lot = (JsonObject)pick["Lottery"]!;
        lot["DurationSeconds"] = PickGame.LotteryDurationSeconds;
        lot["MinEntrantsToSettle"] = PickGame.LotteryMinEntrants;
        lot["HouseFeePercent"] = PickGame.LotteryHouseFeePercent;
        lot["MinStake"] = PickGame.LotteryMinStake;
        lot["MaxStake"] = PickGame.LotteryMaxStake;

        pick["Daily"] ??= new JsonObject();
        var daily = (JsonObject)pick["Daily"]!;
        daily["TicketPrice"] = PickGame.DailyTicketPrice;
        daily["MaxTicketsPerUserPerDay"] = PickGame.DailyMaxTicketsPerUserPerDay;
        daily["MaxTicketsPerBuyCommand"] = PickGame.DailyMaxTicketsPerBuyCommand;
        daily["HouseFeePercent"] = PickGame.DailyHouseFeePercent;
        daily["HistoryLimit"] = PickGame.DailyHistoryLimit;
        daily["DrawHourLocal"] = PickGame.DailyDrawHourLocal;
        daily["TimezoneOffsetHoursOverride"] = PickGame.DailyTimezoneOffsetHoursOverride;

        return await SaveAndReloadAsync(actor, patch, "runtime_tuning.pick.save",
            new
            {
                PickGame.DefaultBet, PickGame.MaxBet, PickGame.HouseEdge,
                PickGame.LotteryDurationSeconds, PickGame.DailyTicketPrice, PickGame.DailyDrawHourLocal,
            }, "Pick / lottery / daily-lottery settings saved.", ct);
    }

    public async Task<IActionResult> OnPostChallengeAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");
        if (actor.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (ChallengeGame.MinBet <= 0 || ChallengeGame.MaxBet < ChallengeGame.MinBet)
        {
            Error = "Challenge: MinBet > 0 and MaxBet >= MinBet required.";
            return await ReloadPageForErrorAsync(ct);
        }
        if (ChallengeGame.HouseFeeBasisPoints is < 0 or > 9999)
        {
            Error = "Challenge: HouseFeeBasisPoints must be in 0..9999 (10000 bps = 100%).";
            return await ReloadPageForErrorAsync(ct);
        }
        if (ChallengeGame.PendingTtlMinutes is < 1 or > 60)
        {
            Error = "Challenge: PendingTtlMinutes must be in 1..60.";
            return await ReloadPageForErrorAsync(ct);
        }

        var patch = await LoadPatchObjectAsync(ct);
        patch["Games"] ??= new JsonObject();
        var games = (JsonObject)patch["Games"]!;
        games["challenges"] ??= new JsonObject();
        var ch = (JsonObject)games["challenges"]!;
        ch["MinBet"] = ChallengeGame.MinBet;
        ch["MaxBet"] = ChallengeGame.MaxBet;
        ch["HouseFeeBasisPoints"] = ChallengeGame.HouseFeeBasisPoints;
        ch["PendingTtlMinutes"] = ChallengeGame.PendingTtlMinutes;

        return await SaveAndReloadAsync(actor, patch, "runtime_tuning.challenges.save",
            new { ChallengeGame.MinBet, ChallengeGame.MaxBet, ChallengeGame.HouseFeeBasisPoints },
            "Challenge settings saved.", ct);
    }

    public async Task<IActionResult> OnPostBlackjackAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");
        if (actor.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (BlackjackGame.MinBet <= 0 || BlackjackGame.MaxBet < BlackjackGame.MinBet)
        {
            Error = "Blackjack: MinBet > 0 and MaxBet >= MinBet required.";
            return await ReloadPageForErrorAsync(ct);
        }
        if (BlackjackGame.HandTimeoutMs < 5_000)
        {
            Error = "Blackjack: HandTimeoutMs must be at least 5000 (5s).";
            return await ReloadPageForErrorAsync(ct);
        }

        var patch = await LoadPatchObjectAsync(ct);
        patch["Games"] ??= new JsonObject();
        var games = (JsonObject)patch["Games"]!;
        games["blackjack"] ??= new JsonObject();
        var bj = (JsonObject)games["blackjack"]!;
        bj["MinBet"] = BlackjackGame.MinBet;
        bj["MaxBet"] = BlackjackGame.MaxBet;
        bj["HandTimeoutMs"] = BlackjackGame.HandTimeoutMs;

        return await SaveAndReloadAsync(actor, patch, "runtime_tuning.blackjack.save",
            new { BlackjackGame.MinBet, BlackjackGame.MaxBet, BlackjackGame.HandTimeoutMs },
            "Blackjack settings saved.", ct);
    }

    public async Task<IActionResult> OnPostSecretHitlerAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");
        if (actor.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (SecretHitlerGame.BuyIn <= 0)
        {
            Error = "Secret Hitler buy-in must be greater than 0.";
            return await ReloadPageForErrorAsync(ct);
        }

        var patch = await LoadPatchObjectAsync(ct);
        patch["Games"] ??= new JsonObject();
        var games = (JsonObject)patch["Games"]!;
        games["sh"] ??= new JsonObject();
        var sh = (JsonObject)games["sh"]!;
        sh["BuyIn"] = SecretHitlerGame.BuyIn;

        return await SaveAndReloadAsync(actor, patch, "runtime_tuning.sh.save",
            new { SecretHitlerGame.BuyIn },
            "Secret Hitler settings saved.", ct);
    }

    public async Task<IActionResult> OnPostMetaAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor is null)
            return RedirectToPage("/Admin/Login");
        if (actor.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (MetaGame.HighRollerTotalStaked <= 0 || MetaGame.BigPayoutMinimum <= 0)
        {
            Error = "Meta: achievement thresholds must be greater than 0.";
            return await ReloadPageForErrorAsync(ct);
        }

        var patch = await LoadPatchObjectAsync(ct);
        patch["Games"] ??= new JsonObject();
        var games = (JsonObject)patch["Games"]!;
        games["meta"] ??= new JsonObject();
        var meta = (JsonObject)games["meta"]!;
        meta["HighRollerTotalStaked"] = MetaGame.HighRollerTotalStaked;
        meta["BigPayoutMinimum"] = MetaGame.BigPayoutMinimum;

        return await SaveAndReloadAsync(actor, patch, "runtime_tuning.meta.save",
            new { MetaGame.HighRollerTotalStaked, MetaGame.BigPayoutMinimum },
            "Meta achievement settings saved.", ct);
    }

    private async Task<IActionResult> SaveAndReloadAsync(
        AdminSession actor, JsonObject patch, string auditAction, object auditPayload,
        string flashOnSuccess, CancellationToken ct)
    {
        var result = await runtimeConfiguration.ApplyAsync(
            patch.ToJsonString(), actor.UserId, actor.Name, auditAction, auditPayload, ct);
        if (!result.Applied)
        {
            Error = FormatIssues(result.Issues);
            return await ReloadPageForErrorAsync(ct);
        }

        Flash = flashOnSuccess;
        PatchJson = FormatJson(result.PatchJson);
        EffectivePreviewJson = FormatJson(result.EffectiveJson);
        LoadAllStructuredSettings();
        await LoadRedeemDropStatsAsync(ct);
        CanEdit = true;
        return Page();
    }

    private async Task<IActionResult> ReloadPageForErrorAsync(CancellationToken ct)
    {
        CanEdit = true;
        var snapshot = await runtimeConfiguration.GetAsync(ct);
        PatchJson = FormatJson(snapshot.PatchJson);
        EffectivePreviewJson = FormatJson(snapshot.EffectiveJson);
        LoadAllStructuredSettings();
        await LoadRedeemDropStatsAsync(ct);
        return Page();
    }

    private async Task<JsonObject> LoadPatchObjectAsync(CancellationToken ct)
    {
        var snapshot = await runtimeConfiguration.GetAsync(ct);
        return JsonNode.Parse(snapshot.PatchJson) as JsonObject ?? new JsonObject();
    }

    private async Task LoadRedeemDropStatsAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<RedeemDropStatsRow>(new CommandDefinition("""
            SELECT
                free_spin_game_id AS GameId,
                count(*)::int AS Issued,
                count(*) FILTER (WHERE issued_by = 0)::int AS BotDrops,
                count(*) FILTER (WHERE active)::int AS Active,
                count(*) FILTER (WHERE NOT active)::int AS Redeemed,
                max(issued_at)::bigint AS LastIssuedAt
            FROM redeem_codes
            GROUP BY free_spin_game_id
            ORDER BY count(*) DESC, free_spin_game_id
            """, cancellationToken: ct));

        DropStats = rows
            .Select(r => new RedeemDropStats(
                r.GameId,
                GameLabel(r.GameId),
                r.Issued,
                r.BotDrops,
                r.Active,
                r.Redeemed,
                r.LastIssuedAt))
            .ToList();
    }

    private static void SetDropChance(JsonObject games, string gameId, double dropChance)
    {
        games[gameId] ??= new JsonObject();
        var game = (JsonObject)games[gameId]!;
        game["RedeemDropChance"] = dropChance;
    }

    private void LoadStickerGameSettings()
    {
        var daily = tuning.TelegramDiceDailyLimit;
        StickerGames = new StickerGameSettingsInput
        {
            DiceDropChance = tuning.GetSection<DiceOptions>(DiceOptions.SectionName).RedeemDropChance,
            DiceDailyLimit = daily.GetMaxRollsPerUserPerDay("dice"),
            DiceCubeDropChance = tuning.GetSection<DiceCubeOptions>(DiceCubeOptions.SectionName).RedeemDropChance,
            DiceCubeDailyLimit = daily.GetMaxRollsPerUserPerDay("dicecube"),
            DartsDropChance = tuning.GetSection<DartsOptions>(DartsOptions.SectionName).RedeemDropChance,
            DartsDailyLimit = daily.GetMaxRollsPerUserPerDay("darts"),
            FootballDropChance = tuning.GetSection<FootballOptions>(FootballOptions.SectionName).RedeemDropChance,
            FootballDailyLimit = daily.GetMaxRollsPerUserPerDay("football"),
            BasketballDropChance = tuning.GetSection<BasketballOptions>(BasketballOptions.SectionName).RedeemDropChance,
            BasketballDailyLimit = daily.GetMaxRollsPerUserPerDay("basketball"),
            BowlingDropChance = tuning.GetSection<BowlingOptions>(BowlingOptions.SectionName).RedeemDropChance,
            BowlingDailyLimit = daily.GetMaxRollsPerUserPerDay("bowling"),
        };
    }

    private void LoadPokerGameSettings()
    {
        var poker = tuning.GetSection<PokerOptions>(PokerOptions.SectionName);
        PokerGame = new PokerGameSettingsInput
        {
            BuyIn = poker.BuyIn,
        };
    }

    private void LoadPickGameSettings()
    {
        var p = tuning.GetSection<PickOptions>(PickOptions.SectionName);
        PickGame = new PickGameSettingsInput
        {
            DefaultBet = p.DefaultBet,
            MaxBet = p.MaxBet,
            HouseEdge = p.HouseEdge,
            StreakBonusPerWin = p.StreakBonusPerWin,
            StreakCap = p.StreakCap,
            ChainMaxDepth = p.ChainMaxDepth,
            ChainTtlSeconds = p.ChainTtlSeconds,
            RevealAnimation = p.RevealAnimation,
            LotteryDurationSeconds = p.Lottery.DurationSeconds,
            LotteryMinEntrants = p.Lottery.MinEntrantsToSettle,
            LotteryHouseFeePercent = p.Lottery.HouseFeePercent,
            LotteryMinStake = p.Lottery.MinStake,
            LotteryMaxStake = p.Lottery.MaxStake,
            DailyTicketPrice = p.Daily.TicketPrice,
            DailyMaxTicketsPerUserPerDay = p.Daily.MaxTicketsPerUserPerDay,
            DailyMaxTicketsPerBuyCommand = p.Daily.MaxTicketsPerBuyCommand,
            DailyHouseFeePercent = p.Daily.HouseFeePercent,
            DailyHistoryLimit = p.Daily.HistoryLimit,
            DailyDrawHourLocal = p.Daily.DrawHourLocal,
            DailyTimezoneOffsetHoursOverride = p.Daily.TimezoneOffsetHoursOverride,
        };
    }

    private void LoadChallengeSettings()
    {
        var c = tuning.GetSection<ChallengeOptions>(ChallengeOptions.SectionName);
        ChallengeGame = new ChallengeSettingsInput
        {
            MinBet = c.MinBet,
            MaxBet = c.MaxBet,
            HouseFeeBasisPoints = c.HouseFeeBasisPoints,
            PendingTtlMinutes = c.PendingTtlMinutes,
        };
    }

    private void LoadBlackjackSettings()
    {
        var b = tuning.GetSection<BlackjackOptions>(BlackjackOptions.SectionName);
        BlackjackGame = new BlackjackSettingsInput
        {
            MinBet = b.MinBet,
            MaxBet = b.MaxBet,
            HandTimeoutMs = b.HandTimeoutMs,
        };
    }

    private void LoadSecretHitlerSettings()
    {
        var s = tuning.GetSection<SecretHitlerOptions>(SecretHitlerOptions.SectionName);
        SecretHitlerGame = new SecretHitlerSettingsInput
        {
            BuyIn = s.BuyIn,
        };
    }

    private void LoadMetaSettings()
    {
        var meta = tuning.GetSection<MetaOptions>(MetaOptions.SectionName);
        MetaGame = new MetaSettingsInput
        {
            HighRollerTotalStaked = meta.HighRollerTotalStaked,
            BigPayoutMinimum = meta.BigPayoutMinimum,
        };
    }

    private void LoadAllStructuredSettings()
    {
        LoadStickerGameSettings();
        LoadPokerGameSettings();
        LoadPickGameSettings();
        LoadChallengeSettings();
        LoadBlackjackSettings();
        LoadSecretHitlerSettings();
        LoadMetaSettings();
    }

    private static string GameLabel(string gameId) => gameId switch
    {
        "dice" => "slots",
        "dicecube" => "dicecube",
        "darts" => "darts",
        "football" => "football",
        "basketball" => "basketball",
        "bowling" => "bowling",
        _ => gameId,
    };

    private static string FormatJson(string json)
    {
        try
        {
            var n = JsonNode.Parse(json);
            return n?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? json;
        }
        catch
        {
            return json;
        }
    }

    private static string? FormatIssues(IReadOnlyList<ConfigurationValidationIssue> issues) =>
        issues.Count == 0
            ? null
            : string.Join(Environment.NewLine, issues.Select(static issue =>
                $"{issue.Path}: {issue.Message} ({issue.Code})"));

    public sealed class StickerGameSettingsInput
    {
        public double DiceDropChance { get; set; }
        public int DiceDailyLimit { get; set; }
        public double DiceCubeDropChance { get; set; }
        public int DiceCubeDailyLimit { get; set; }
        public double DartsDropChance { get; set; }
        public int DartsDailyLimit { get; set; }
        public double FootballDropChance { get; set; }
        public int FootballDailyLimit { get; set; }
        public double BasketballDropChance { get; set; }
        public int BasketballDailyLimit { get; set; }
        public double BowlingDropChance { get; set; }
        public int BowlingDailyLimit { get; set; }

        public IEnumerable<(string GameId, double DropChance, int DailyLimit)> All
        {
            get
            {
                yield return ("dice", DiceDropChance, DiceDailyLimit);
                yield return ("dicecube", DiceCubeDropChance, DiceCubeDailyLimit);
                yield return ("darts", DartsDropChance, DartsDailyLimit);
                yield return ("football", FootballDropChance, FootballDailyLimit);
                yield return ("basketball", BasketballDropChance, BasketballDailyLimit);
                yield return ("bowling", BowlingDropChance, BowlingDailyLimit);
            }
        }
    }

    public sealed class PokerGameSettingsInput
    {
        public int BuyIn { get; set; }
    }

    public sealed class PickGameSettingsInput
    {
        public int DefaultBet { get; set; }
        public int MaxBet { get; set; }
        public double HouseEdge { get; set; }
        public double StreakBonusPerWin { get; set; }
        public int StreakCap { get; set; }
        public int ChainMaxDepth { get; set; }
        public int ChainTtlSeconds { get; set; }
        public bool RevealAnimation { get; set; }

        public int LotteryDurationSeconds { get; set; }
        public int LotteryMinEntrants { get; set; }
        public double LotteryHouseFeePercent { get; set; }
        public int LotteryMinStake { get; set; }
        public int LotteryMaxStake { get; set; }

        public int DailyTicketPrice { get; set; }
        public int DailyMaxTicketsPerUserPerDay { get; set; }
        public int DailyMaxTicketsPerBuyCommand { get; set; }
        public double DailyHouseFeePercent { get; set; }
        public int DailyHistoryLimit { get; set; }
        public int DailyDrawHourLocal { get; set; }
        public int DailyTimezoneOffsetHoursOverride { get; set; }
    }

    public sealed class ChallengeSettingsInput
    {
        public int MinBet { get; set; }
        public int MaxBet { get; set; }
        public int HouseFeeBasisPoints { get; set; }
        public int PendingTtlMinutes { get; set; }
    }

    public sealed class BlackjackSettingsInput
    {
        public int MinBet { get; set; }
        public int MaxBet { get; set; }
        public int HandTimeoutMs { get; set; }
    }

    public sealed class SecretHitlerSettingsInput
    {
        public int BuyIn { get; set; }
    }

    public sealed class MetaSettingsInput
    {
        public long HighRollerTotalStaked { get; set; } = 1_000;
        public long BigPayoutMinimum { get; set; } = 1_000;
    }

    private sealed record RedeemDropStatsRow(
        string GameId,
        int Issued,
        int BotDrops,
        int Active,
        int Redeemed,
        long LastIssuedAt);

    public sealed record RedeemDropStats(
        string GameId,
        string Label,
        int Issued,
        int BotDrops,
        int Active,
        int Redeemed,
        long LastIssuedAt);
}
