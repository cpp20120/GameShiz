using System.Globalization;
using System.Text.Json;

namespace Games.Meta.Infrastructure.Catalog;

public sealed partial class JsonQuestCatalog : IQuestCatalog
{
    private const string FileName = "quest-pool.json";

    private QuestCatalogState _state;

    public JsonQuestCatalog()
        : this(LoadDocument())
    {
    }

    internal JsonQuestCatalog(QuestPoolDocument document)
    {
        _state = BuildState(document);
    }

    public static JsonQuestCatalog Default { get; } = new();

    public IReadOnlyList<QuestTemplate> All => _state.All;

    public void Reload()
    {
        _state = BuildState(LoadDocument());
    }

    public static QuestCatalogValidation ValidateJson(string json)
    {
        var document = DeserializeDocument(json);
        var state = BuildState(document);
        return new QuestCatalogValidation(
            state.All.Count,
            state.Slots.Count,
            document.Definitions.Count,
            state.All.Count(x => string.Equals(x.Period, "daily", StringComparison.Ordinal)),
            state.All.Count(x => string.Equals(x.Period, "weekly", StringComparison.Ordinal)));
    }

    public static string EditablePath()
    {
        var appBasePath = Path.Combine(AppContext.BaseDirectory, FileName);
        return File.Exists(appBasePath) || !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), FileName))
            ? appBasePath
            : Path.Combine(Directory.GetCurrentDirectory(), FileName);
    }

    public static string ReadEffectiveJson()
    {
        foreach (var path in CandidatePaths())
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        var assembly = typeof(JsonQuestCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith($".{FileName}", StringComparison.Ordinal));
        if (resourceName is null)
            throw new FileNotFoundException($"Could not find {FileName}. Checked: {string.Join(", ", CandidatePaths())}");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded quest catalog resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public IReadOnlyList<QuestTemplate> ActiveFor(
        MetaSeason season,
        long chatId,
        long userId,
        DateTimeOffset now,
        QuestPlayerProgress? progress = null)
    {
        progress ??= new QuestPlayerProgress(0, 0, 0);
        var selected = new List<QuestTemplate>();
        var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedRepeatKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var state = _state;
        var rotation = SeasonQuestRotationConfig.FromSeason(season);
        foreach (var slot in state.Slots)
        {
            var candidates = state.Candidates
                .Where(x => string.Equals(x.Template.Period, slot.Period, StringComparison.Ordinal) && HasTags(x, slot.PoolTags) && IsUnlocked(x.Template, progress))
                .OrderBy(x => x.Template.Id, StringComparer.Ordinal)
                .ToArray();

            if (candidates.Length == 0)
                continue;

            for (var index = 0; index < slot.Count; index++)
            {
                var softBlocked = PreviousRawPicks(season, chatId, userId, now, slot, index, candidates, rotation)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var seed = string.Create(CultureInfo.InvariantCulture, $"{season.Id}:{chatId}:{userId}:{PeriodKey(slot.Period, now)}:{slot.Id}:{index}");
                var picked = Pick(candidates, seed, selectedIds, selectedRepeatKeys, softBlocked, rotation);
                if (picked is null)
                    continue;

                selected.Add(picked.Template);
                selectedIds.Add(picked.Template.Id);
                selectedRepeatKeys.Add(picked.RepeatKey);
            }
        }

        return selected;
    }

    public IEnumerable<QuestTemplate> Matching(
        MetaSeason season,
        long chatId,
        long userId,
        GameCompletedMetaEvent ev,
        QuestPlayerProgress? progress = null)
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(ev.OccurredAt);
        foreach (var quest in ActiveFor(season, chatId, userId, now, progress))
        {
            if (quest.GameKey is not null &&
                !string.Equals(quest.GameKey, ev.GameKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (MatchesKind(quest, ev))
                yield return quest;
        }
    }

    public QuestTemplate? FindActive(
        MetaSeason season,
        long chatId,
        long userId,
        string questId,
        DateTimeOffset now,
        QuestPlayerProgress? progress = null)
    {
        return ActiveFor(season, chatId, userId, now, progress)
            .FirstOrDefault(x => string.Equals(x.Id, questId, StringComparison.OrdinalIgnoreCase));
    }

    public static int DeltaFor(QuestTemplate quest, GameCompletedMetaEvent ev) => quest.Kind switch
    {
        "volume" => (int)Math.Min(int.MaxValue, Math.Max(0, ev.Stake)),
        "payout" => (int)Math.Min(int.MaxValue, Math.Max(0, ev.Payout)),
        "profit" => (int)Math.Min(int.MaxValue, Math.Max(0, ev.Payout - ev.Stake)),
        _ => 1,
    };

    public static string PeriodKey(QuestTemplate quest, DateTimeOffset now) => PeriodKey(quest.Period, now);

    public static string PeriodKey(string period, DateTimeOffset now)
    {
        return NormalizePeriod(period) switch
        {
            "weekly" => string.Create(CultureInfo.InvariantCulture, $"{ISOWeek.GetYear(now.DateTime)}-W{ISOWeek.GetWeekOfYear(now.DateTime):00}"),
            _ => now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    private static QuestPoolDocument LoadDocument()
    {
        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path))
                continue;

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<QuestPoolDocument>(stream, JsonOptions())
                ?? throw new InvalidOperationException($"{path} is empty.");
        }

        var assembly = typeof(JsonQuestCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith($".{FileName}", StringComparison.Ordinal));
        if (resourceName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded quest catalog resource '{resourceName}' is missing.");
            return JsonSerializer.Deserialize<QuestPoolDocument>(stream, JsonOptions())
                ?? throw new InvalidOperationException($"Embedded quest catalog resource '{resourceName}' is empty.");
        }

        throw new FileNotFoundException($"Could not find {FileName}. Checked: {string.Join(", ", CandidatePaths())}");
    }

    private static QuestPoolDocument DeserializeDocument(string json)
    {
        return JsonSerializer.Deserialize<QuestPoolDocument>(json, JsonOptions())
            ?? throw new InvalidOperationException("Quest catalog JSON is empty.");
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, FileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), FileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), "games", "Games.Meta", FileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), "games", "Games.Meta", "Infrastructure", "Catalog", FileName);
    }
}
