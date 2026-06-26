using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CasinoShiz.Host.Pages.Admin;

public sealed class MetaQuestsModel(
    INpgsqlConnectionFactory connections,
    IQuestCatalog questCatalog,
    IAdminAuditLog audit) : PageModel
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    public AdminSession? Actor { get; private set; }
    public bool CanEdit { get; private set; }
    public string? Flash { get; private set; }
    public bool FlashError { get; private set; }
    public string EffectivePath { get; private set; } = "";
    public QuestCatalogValidation Validation { get; private set; } = new(0, 0, 0, 0, 0);
    public IReadOnlyList<QuestTemplate> ActivePreview { get; private set; } = [];
    public IReadOnlyList<IGrouping<string, QuestTemplate>> ActivePreviewGroups { get; private set; } = [];
    public IReadOnlyList<QuestPreviewSeasonOption> PreviewSeasons { get; private set; } = [];
    public SeasonQuestRotationConfig PreviewRotation { get; private set; } = SeasonQuestRotationConfig.Default;
    public string PreviewSeasonName { get; private set; } = "Ad hoc preview";

    [BindProperty] public string CatalogJson { get; set; } = "";
    [BindProperty] public List<QuestSlotEditor> Slots { get; set; } = [];
    [BindProperty] public List<QuestDefinitionEditor> Definitions { get; set; } = [];
    [BindProperty(SupportsGet = true)] public long PreviewSeasonId { get; set; }
    [BindProperty(SupportsGet = true)] public long PreviewChatId { get; set; } = 100;
    [BindProperty(SupportsGet = true)] public long PreviewUserId { get; set; } = 42;
    [BindProperty(SupportsGet = true)] public int PreviewLevel { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PreviewGamesPlayed { get; set; }
    [BindProperty(SupportsGet = true)] public long PreviewTotalStaked { get; set; }
    [BindProperty(SupportsGet = true)] public string? PreviewAt { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var init = await InitAsync(rawJsonOverride: null, ct);
        return init ?? Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        Actor = HttpContext.Session.GetAdminSession();
        if (Actor is null) return RedirectToPage("/Admin/Login");
        if (Actor.Role != AdminRole.SuperAdmin) return Forbid();

        QuestCatalogValidation validation;
        string formatted;
        try
        {
            validation = JsonQuestCatalog.ValidateJson(CatalogJson);
            using var document = JsonDocument.Parse(CatalogJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            formatted = JsonSerializer.Serialize(document.RootElement, PrettyJson);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var init = await InitAsync(CatalogJson, ct);
            FlashError = true;
            Flash = $"Quest catalog rejected: {ex.Message}";
            Validation = new(0, 0, 0, 0, 0);
            return init ?? Page();
        }

        await SaveCatalogAsync(formatted, validation, "meta.quests.save", ct);

        TempData["Flash"] = string.Create(CultureInfo.InvariantCulture, $"Quest catalog saved and reloaded: {validation.QuestCount} generated quests, {validation.SlotCount} slots.");
        return RedirectToPreview();
    }

    public async Task<IActionResult> OnPostSaveStructuredAsync(CancellationToken ct)
    {
        Actor = HttpContext.Session.GetAdminSession();
        if (Actor is null) return RedirectToPage("/Admin/Login");
        if (Actor.Role != AdminRole.SuperAdmin) return Forbid();

        QuestCatalogValidation validation;
        string formatted;
        try
        {
            var document = ReadEditorDocument();
            document.Slots = Slots.ConvertAll(x => x.ToDocument());
            document.Definitions = Definitions.ConvertAll(x => x.ToDocument());
            formatted = JsonSerializer.Serialize(document, PrettyJson);
            validation = JsonQuestCatalog.ValidateJson(formatted);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            var init = await InitAsync(rawJsonOverride: null, ct);
            FlashError = true;
            Flash = $"Structured quest settings rejected: {ex.Message}";
            return init ?? Page();
        }

        await SaveCatalogAsync(formatted, validation, "meta.quests.structured.save", ct);
        TempData["Flash"] = $"Quest settings saved and reloaded: {validation.QuestCount} generated quests, {validation.SlotCount} slots.";
        return RedirectToPreview();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        Actor = HttpContext.Session.GetAdminSession();
        if (Actor is null) return RedirectToPage("/Admin/Login");
        if (Actor.Role != AdminRole.SuperAdmin) return Forbid();

        questCatalog.Reload();
        await audit.LogAsync(Actor.UserId, Actor.Name, "meta.quests.reload", new
        {
            path = JsonQuestCatalog.EditablePath(),
            count = questCatalog.All.Count,
        }, ct);

        TempData["Flash"] = $"Quest catalog reloaded: {questCatalog.All.Count} generated quests.";
        return RedirectToPreview();
    }

    private async Task<IActionResult?> InitAsync(string? rawJsonOverride, CancellationToken ct)
    {
        await Task.CompletedTask;

        Actor = HttpContext.Session.GetAdminSession();
        if (Actor is null) return RedirectToPage("/Admin/Login");

        CanEdit = Actor.Role == AdminRole.SuperAdmin;
        Flash = TempData["Flash"] as string;
        FlashError = TempData["FlashError"] is not null;
        EffectivePath = JsonQuestCatalog.EditablePath();

        CatalogJson = rawJsonOverride ?? JsonQuestCatalog.ReadEffectiveJson();
        try
        {
            Validation = JsonQuestCatalog.ValidateJson(CatalogJson);
            LoadStructuredEditors(CatalogJson);
        }
        catch
        {
            Validation = new(questCatalog.All.Count, 0, 0, 0, 0);
            Slots = [];
            Definitions = [];
        }

        var now = ParsePreviewAt();
        var season = await LoadPreviewSeasonAsync(now, ct);
        PreviewRotation = SeasonQuestRotationConfig.FromSeason(season);
        PreviewSeasonName = season.Name;
        var progress = new QuestPlayerProgress(
            Math.Max(0, PreviewLevel),
            Math.Max(0, PreviewGamesPlayed),
            Math.Max(0, PreviewTotalStaked));
        ActivePreview = questCatalog.ActiveFor(season, PreviewChatId, PreviewUserId, now, progress);
        ActivePreviewGroups = ActivePreview
            .GroupBy(x => x.Period, StringComparer.Ordinal)
            .OrderBy(x => string.Equals(x.Key, "daily", StringComparison.Ordinal) ? 0 : 1)
            .ToArray();
        return null;
    }

    private async Task<MetaSeason> LoadPreviewSeasonAsync(DateTimeOffset now, CancellationToken ct)
    {
        const string listSql = """
            SELECT id AS Id,
                   name AS Name,
                   status AS Status,
                   starts_at AS StartsAt,
                   ends_at AS EndsAt,
                   config::text AS ConfigJson
            FROM meta_seasons
            ORDER BY
              CASE status WHEN 'active' THEN 0 WHEN 'planned' THEN 1 ELSE 2 END,
              starts_at DESC,
              id DESC
            LIMIT 80
            """;

        await using var conn = await connections.OpenAsync(ct);
        var rows = (await conn.QueryAsync<QuestPreviewSeasonOption>(new CommandDefinition(
            listSql,
            cancellationToken: ct))).ToArray();
        PreviewSeasons = rows;

        var selected = PreviewSeasonId > 0
            ? rows.FirstOrDefault(x => x.Id == PreviewSeasonId)
            : rows.FirstOrDefault(x => string.Equals(x.Status, "active", StringComparison.Ordinal)) ?? rows.FirstOrDefault();

        if (selected is null)
        {
            PreviewSeasonId = 1;
            return new MetaSeason(
                PreviewSeasonId,
                "Ad hoc preview",
                now.AddYears(-1),
                now.AddYears(1),
                "active",
                SeasonPlanFactory.DefaultConfigJson);
        }

        PreviewSeasonId = selected.Id;
        return new MetaSeason(
            selected.Id,
            selected.Name,
            selected.StartsAt,
            selected.EndsAt,
            selected.Status,
            selected.ConfigJson);
    }

    private async Task SaveCatalogAsync(
        string formatted,
        QuestCatalogValidation validation,
        string auditAction,
        CancellationToken ct)
    {
        var path = JsonQuestCatalog.EditablePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await System.IO.File.WriteAllTextAsync(path, formatted, ct);
        questCatalog.Reload();

        await audit.LogAsync(Actor!.UserId, Actor.Name, auditAction, new
        {
            path,
            validation.QuestCount,
            validation.SlotCount,
            validation.DefinitionCount,
        }, ct);
    }

    private RedirectToPageResult RedirectToPreview()
    {
        return RedirectToPage(new
        {
            PreviewSeasonId,
            PreviewChatId,
            PreviewUserId,
            PreviewLevel,
            PreviewGamesPlayed,
            PreviewTotalStaked,
            PreviewAt,
        });
    }

    private static QuestPoolEditorDocument ReadEditorDocument()
    {
        var raw = JsonQuestCatalog.ReadEffectiveJson();
        return JsonSerializer.Deserialize<QuestPoolEditorDocument>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) ?? throw new InvalidOperationException("Quest catalog JSON is empty.");
    }

    private void LoadStructuredEditors(string json)
    {
        var document = JsonSerializer.Deserialize<QuestPoolEditorDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) ?? new QuestPoolEditorDocument();

        Slots = document.Slots.ConvertAll(QuestSlotEditor.FromDocument);
        Definitions = document.Definitions.ConvertAll(QuestDefinitionEditor.FromDocument);
    }

    private DateTimeOffset ParsePreviewAt()
    {
        if (DateTimeOffset.TryParse(PreviewAt, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        var now = DateTimeOffset.UtcNow;
        PreviewAt = now.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
        return now;
    }

    private static string JoinCsv<T>(IEnumerable<T> values) => string.Join(", ", values);
    private static string JoinLines(IEnumerable<string> values) => string.Join('\n', values);

    private static List<string> SplitCsv(string? value)
    {
        return (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static List<string> SplitLines(string? value)
    {
        return (value ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static List<int> SplitIntCsv(string? value) => SplitCsv(value).ConvertAll(int.Parse);
    private static List<long> SplitLongCsv(string? value) => SplitCsv(value).ConvertAll(long.Parse);
    private static List<decimal> SplitDecimalCsv(string? value) =>
        SplitCsv(value).ConvertAll(x => decimal.Parse(x, System.Globalization.CultureInfo.InvariantCulture));

    public sealed class QuestSlotEditor
    {
        public string Id { get; set; } = "";
        public string Period { get; set; } = "daily";
        public string PoolTags { get; set; } = "";
        public int Count { get; set; } = 1;
        public int RepeatCooldownPeriods { get; set; }

        public static QuestSlotEditor FromDocument(QuestSlotDocumentEditor slot) => new()
        {
            Id = slot.Id,
            Period = slot.Period,
            PoolTags = JoinCsv(slot.PoolTags),
            Count = slot.Count,
            RepeatCooldownPeriods = slot.RepeatCooldownPeriods,
        };

        public QuestSlotDocumentEditor ToDocument() => new()
        {
            Id = Id.Trim(),
            Period = Period.Trim(),
            PoolTags = SplitCsv(PoolTags),
            Count = Math.Max(1, Count),
            RepeatCooldownPeriods = Math.Max(0, RepeatCooldownPeriods),
        };
    }

    public sealed class QuestDefinitionEditor
    {
        public string Id { get; set; } = "";
        public string Period { get; set; } = "daily";
        public string Kind { get; set; } = "play";
        public string Cluster { get; set; } = "core";
        public string Rarity { get; set; } = "common";
        public string Tags { get; set; } = "";
        public string GameKeys { get; set; } = "";
        public string Targets { get; set; } = "";
        public string MinStakes { get; set; } = "";
        public string MaxStakes { get; set; } = "";
        public string MinPayouts { get; set; } = "";
        public string MinProfits { get; set; } = "";
        public string MinMultipliers { get; set; } = "";
        public int MinLevel { get; set; }
        public int MinGamesPlayed { get; set; }
        public long MinTotalStaked { get; set; }
        public long RewardXp { get; set; }
        public long RewardCoins { get; set; }
        public string Titles { get; set; } = "";
        public string Descriptions { get; set; } = "";

        public static QuestDefinitionEditor FromDocument(QuestDefinitionDocumentEditor definition) => new()
        {
            Id = definition.Id,
            Period = definition.Period,
            Kind = definition.Kind,
            Cluster = definition.Cluster,
            Rarity = definition.Rarity,
            Tags = JoinCsv(definition.Tags),
            GameKeys = JoinCsv(definition.GameKeys),
            Targets = JoinCsv(definition.Targets),
            MinStakes = JoinCsv(definition.MinStakes),
            MaxStakes = JoinCsv(definition.MaxStakes),
            MinPayouts = JoinCsv(definition.MinPayouts),
            MinProfits = JoinCsv(definition.MinProfits),
            MinMultipliers = JoinCsv(definition.MinMultipliers),
            MinLevel = definition.MinLevel,
            MinGamesPlayed = definition.MinGamesPlayed,
            MinTotalStaked = definition.MinTotalStaked,
            RewardXp = definition.RewardXp,
            RewardCoins = definition.RewardCoins,
            Titles = JoinLines(definition.Titles),
            Descriptions = JoinLines(definition.Descriptions),
        };

        public QuestDefinitionDocumentEditor ToDocument() => new()
        {
            Id = Id.Trim(),
            Period = Period.Trim(),
            Kind = Kind.Trim(),
            Cluster = Cluster.Trim(),
            Rarity = Rarity.Trim(),
            Tags = SplitCsv(Tags),
            GameKeys = SplitCsv(GameKeys),
            Targets = SplitIntCsv(Targets),
            MinStakes = SplitLongCsv(MinStakes),
            MaxStakes = SplitLongCsv(MaxStakes),
            MinPayouts = SplitLongCsv(MinPayouts),
            MinProfits = SplitLongCsv(MinProfits),
            MinMultipliers = SplitDecimalCsv(MinMultipliers),
            MinLevel = Math.Max(0, MinLevel),
            MinGamesPlayed = Math.Max(0, MinGamesPlayed),
            MinTotalStaked = Math.Max(0, MinTotalStaked),
            RewardXp = Math.Max(0, RewardXp),
            RewardCoins = Math.Max(0, RewardCoins),
            Titles = SplitLines(Titles),
            Descriptions = SplitLines(Descriptions),
        };
    }

    public sealed class QuestPoolEditorDocument
    {
        [JsonPropertyName("games")] public List<QuestGameDocumentEditor> Games { get; set; } = [];
        [JsonPropertyName("slots")] public List<QuestSlotDocumentEditor> Slots { get; set; } = [];
        [JsonPropertyName("definitions")] public List<QuestDefinitionDocumentEditor> Definitions { get; set; } = [];
    }

    public sealed class QuestGameDocumentEditor
    {
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("command")] public string Command { get; set; } = "";
    }

    public sealed class QuestSlotDocumentEditor
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("period")] public string Period { get; set; } = "daily";
        [JsonPropertyName("poolTags")] public List<string> PoolTags { get; set; } = [];
        [JsonPropertyName("count")] public int Count { get; set; } = 1;
        [JsonPropertyName("repeatCooldownPeriods")] public int RepeatCooldownPeriods { get; set; }
    }

    public sealed class QuestDefinitionDocumentEditor
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("period")] public string Period { get; set; } = "daily";
        [JsonPropertyName("kind")] public string Kind { get; set; } = "play";
        [JsonPropertyName("cluster")] public string Cluster { get; set; } = "core";
        [JsonPropertyName("rarity")] public string Rarity { get; set; } = "common";
        [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
        [JsonPropertyName("gameKeys")] public List<string> GameKeys { get; set; } = [];
        [JsonPropertyName("targets")] public List<int> Targets { get; set; } = [];
        [JsonPropertyName("minStakes")] public List<long> MinStakes { get; set; } = [];
        [JsonPropertyName("maxStakes")] public List<long> MaxStakes { get; set; } = [];
        [JsonPropertyName("minPayouts")] public List<long> MinPayouts { get; set; } = [];
        [JsonPropertyName("minProfits")] public List<long> MinProfits { get; set; } = [];
        [JsonPropertyName("minMultipliers")] public List<decimal> MinMultipliers { get; set; } = [];
        [JsonPropertyName("minLevel")] public int MinLevel { get; set; }
        [JsonPropertyName("minGamesPlayed")] public int MinGamesPlayed { get; set; }
        [JsonPropertyName("minTotalStaked")] public long MinTotalStaked { get; set; }
        [JsonPropertyName("rewardXp")] public long RewardXp { get; set; }
        [JsonPropertyName("rewardCoins")] public long RewardCoins { get; set; }
        [JsonPropertyName("titles")] public List<string> Titles { get; set; } = [];
        [JsonPropertyName("descriptions")] public List<string> Descriptions { get; set; } = [];
    }

    public sealed record QuestPreviewSeasonOption(
        long Id,
        string Name,
        string Status,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        string ConfigJson);
}
