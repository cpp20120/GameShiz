using System.Globalization;
using System.Text.Json.Nodes;
using BotFramework.Sdk.Configuration;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed partial class HorseModel(
    IHorseService horse,
    IHorseRaceNotifier notifier,
    INpgsqlConnectionFactory connections,
    HorseGifCache gifCache,
    IAdminAuditLog audit,
    IRuntimeTuningAccessor tuning,
    IRuntimeConfigurationService runtimeConfiguration) : PageModel
{
    private HorseOptions Options => tuning.GetSection<HorseOptions>(HorseOptions.SectionName);

    public int BetsToday { get; private set; }
    public IReadOnlyDictionary<int, double> Koefs { get; private set; } = new Dictionary<int, double>();
    public IReadOnlyList<PastRace> Past { get; private set; } = [];
    public string TodayRaceDate { get; private set; } = "";
    public int MinBets => Options.MinBetsToRun;
    public int HorseCount => Options.HorseCount;
    public IReadOnlyList<long> ConfiguredAdmins => Options.Admins;
    public IReadOnlyList<string> DatesWithGif { get; private set; } = [];

    [BindProperty]
    public HorseScheduleSettingsInput Schedule { get; set; } = new();

    public string? Flash { get; set; }
    public bool FlashError { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostRunAsync(CancellationToken ct)
    {
        var options = Options;
        TodayRaceDate = HorseTimeHelper.GetRaceDate(options.TimezoneOffsetHours);
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        var callerId = actor.UserId;

        var outcome = await horse.RunRaceAsync(callerId, HorseRunKind.Global, 0, ct);
        if (outcome.Error != HorseError.None)
        {
            TempData["FlashError"] = $"Race rejected: {outcome.Error}";
            return RedirectToPage();
        }

        gifCache.Put(TodayRaceDate, outcome.GifBytes);

        await notifier.SendResultGifsAsync(outcome, TodayRaceDate, ct);
        notifier.ScheduleWinnerAnnouncements(outcome);
        var flash = string.Create(CultureInfo.InvariantCulture, $"Race done. Winner: horse {outcome.Winner + 1}. ") +
            $"Payouts: {outcome.Transactions.Count}. " +
            $"Chats: {outcome.BetScopeIds.Count}.";
        TempData["Flash"] = flash;

        await audit.LogAsync(actor.UserId, actor.Name, "horse.run_race",
            new { winner = outcome.Winner + 1, payouts = outcome.Transactions.Count, raceDate = TodayRaceDate }, ct);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostScheduleAsync(CancellationToken ct)
    {
        var actor = HttpContext.Session.GetAdminSession();
        if (actor?.Role != AdminRole.SuperAdmin)
            return StatusCode(403);

        if (Schedule.EveryDays is < 1 or > 31
            || Schedule.LocalHour is < 0 or > 23
            || Schedule.LocalMinute is < 0 or > 59
            || Schedule.TimezoneOffsetHours is < -14 or > 14)
        {
            TempData["FlashError"] = "Schedule: period must be 1..31 days, hour 0..23, minute 0..59, timezone -14..14.";
            return RedirectToPage();
        }

        var snapshot = await runtimeConfiguration.GetAsync(ct);
        var patch = JsonNode.Parse(snapshot.PatchJson) as JsonObject ?? new JsonObject();
        patch["Games"] ??= new JsonObject();
        var games = (JsonObject)patch["Games"]!;
        games["horse"] ??= new JsonObject();
        var horseSection = (JsonObject)games["horse"]!;
        horseSection["AutoRunEnabled"] = Schedule.Enabled;
        horseSection["AutoRunEveryDays"] = Schedule.EveryDays;
        horseSection["AutoRunLocalHour"] = Schedule.LocalHour;
        horseSection["AutoRunLocalMinute"] = Schedule.LocalMinute;
        horseSection["TimezoneOffsetHours"] = Schedule.TimezoneOffsetHours;

        var result = await runtimeConfiguration.ApplyAsync(
            patch.ToJsonString(),
            actor.UserId,
            actor.Name,
            "runtime_tuning.horse.schedule.save",
            new
            {
                Schedule.Enabled,
                Schedule.EveryDays,
                Schedule.LocalHour,
                Schedule.LocalMinute,
                Schedule.TimezoneOffsetHours,
            },
            ct);

        if (!result.Applied)
        {
            TempData["FlashError"] = string.Join("; ", result.Issues.Select(issue =>
                $"{issue.Path}: {issue.Message}"));
            return RedirectToPage();
        }

        TempData["Flash"] = "Horse schedule saved. Quartz will pick up the new settings within a minute.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var options = Options;
        TodayRaceDate = HorseTimeHelper.GetRaceDate(options.TimezoneOffsetHours);
        var info = await horse.GetTodayInfoAsync(balanceScopeIdOnly: null, ct);
        BetsToday = info.BetsCount;
        Koefs = info.Koefs;

        Schedule = new HorseScheduleSettingsInput
        {
            Enabled = options.AutoRunEnabled,
            EveryDays = options.AutoRunEveryDays,
            LocalHour = options.AutoRunLocalHour,
            LocalMinute = options.AutoRunLocalMinute,
            TimezoneOffsetHours = options.TimezoneOffsetHours,
        };

        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<PastRace>(new CommandDefinition("""
            SELECT race_date AS RaceDate, balance_scope_id AS BalanceScopeId, winner AS Winner
            FROM horse_results
            ORDER BY race_date DESC, balance_scope_id ASC
            LIMIT 60
            """, cancellationToken: ct));
        Past = rows.ToList();
        DatesWithGif = gifCache.Dates;

        Flash = TempData["Flash"] as string;
        FlashError = TempData["FlashError"] is not null;
        if (FlashError) Flash = TempData["FlashError"] as string;
    }

    public sealed class HorseScheduleSettingsInput
    {
        public bool Enabled { get; set; }
        public int EveryDays { get; set; } = 1;
        public int LocalHour { get; set; } = 21;
        public int LocalMinute { get; set; }
        public int TimezoneOffsetHours { get; set; } = 7;
    }
}
