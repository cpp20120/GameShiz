using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Games.Meta;

public interface IQuestCatalog
{
    IReadOnlyList<QuestTemplate> All { get; }
    void Reload();

    IReadOnlyList<QuestTemplate> ActiveFor(
        MetaSeason season,
        long chatId,
        long userId,
        DateTimeOffset now,
        QuestPlayerProgress? progress = null);

    IEnumerable<QuestTemplate> Matching(
        MetaSeason season,
        long chatId,
        long userId,
        GameCompletedMetaEvent ev,
        QuestPlayerProgress? progress = null);

    QuestTemplate? FindActive(
        MetaSeason season,
        long chatId,
        long userId,
        string questId,
        DateTimeOffset now,
        QuestPlayerProgress? progress = null);
}

public sealed class JsonQuestCatalog : IQuestCatalog
{
    private const string FileName = "quest-pool.json";

    private QuestCatalogState _state = new([], [], []);

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
            state.All.Count(x => x.Period == "daily"),
            state.All.Count(x => x.Period == "weekly"));
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

    private static QuestCatalogState BuildState(QuestPoolDocument document)
    {
        var games = document.Games.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var slots = document.Slots.Select(x => new QuestSlot(
            x.Id,
            NormalizePeriod(x.Period),
            x.PoolTags.Select(NormalizeTag).ToArray(),
            Math.Max(1, x.Count),
            Math.Max(0, x.RepeatCooldownPeriods))).ToArray();

        var candidates = BuildCandidates(document.Definitions, games);
        var all = candidates.Select(x => x.Template).ToArray();

        var duplicate = all
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate quest id in {FileName}: {duplicate.Key}");

        return new QuestCatalogState(candidates, slots, all);
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
                .Where(x => x.Template.Period == slot.Period && HasTags(x, slot.PoolTags))
                .Where(x => IsUnlocked(x.Template, progress))
                .OrderBy(x => x.Template.Id, StringComparer.Ordinal)
                .ToArray();

            if (candidates.Length == 0)
                continue;

            for (var index = 0; index < slot.Count; index++)
            {
                var softBlocked = PreviousRawPicks(season, chatId, userId, now, slot, index, candidates, rotation)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var seed = $"{season.Id}:{chatId}:{userId}:{PeriodKey(slot.Period, now)}:{slot.Id}:{index}";
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
            "weekly" => $"{ISOWeek.GetYear(now.DateTime)}-W{ISOWeek.GetWeekOfYear(now.DateTime):00}",
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
            return JsonSerializer.Deserialize<QuestPoolDocument>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new InvalidOperationException($"{path} is empty.");
        }

        var assembly = typeof(JsonQuestCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith($".{FileName}", StringComparison.Ordinal));
        if (resourceName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded quest catalog resource '{resourceName}' is missing.");
            return JsonSerializer.Deserialize<QuestPoolDocument>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new InvalidOperationException($"Embedded quest catalog resource '{resourceName}' is empty.");
        }

        throw new FileNotFoundException($"Could not find {FileName}. Checked: {string.Join(", ", CandidatePaths())}");
    }

    private static QuestPoolDocument DeserializeDocument(string json)
    {
        return JsonSerializer.Deserialize<QuestPoolDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        }) ?? throw new InvalidOperationException("Quest catalog JSON is empty.");
    }

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, FileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), FileName);
        yield return Path.Combine(Directory.GetCurrentDirectory(), "games", "Games.Meta", FileName);
    }

    private static IReadOnlyList<QuestCandidate> BuildCandidates(
        IReadOnlyList<QuestDefinition> definitions,
        IReadOnlyDictionary<string, QuestGame> games)
    {
        var result = new List<QuestCandidate>();
        foreach (var definition in definitions)
        {
            var period = NormalizePeriod(definition.Period);
            var kind = NormalizeKind(definition.Kind);
            var targets = definition.Targets.Count == 0 ? [1] : definition.Targets;
            var titles = definition.Titles.Count == 0 ? [definition.Id] : definition.Titles;
            var descriptions = definition.Descriptions.Count == 0 ? ["Выполни цель: {target}."] : definition.Descriptions;
            var gameKeys = definition.GameKeys.Count == 0 ? [(string?)null] : definition.GameKeys.Select(x => (string?)x).ToArray();
            var conditions = BuildConditions(definition);

            foreach (var gameKey in gameKeys)
            {
                QuestGame? game = null;
                if (gameKey is not null && !games.TryGetValue(gameKey, out game))
                    throw new InvalidOperationException($"Quest definition {definition.Id} references unknown game '{gameKey}'.");

                foreach (var target in targets)
                {
                    foreach (var condition in conditions)
                    {
                        for (var titleIndex = 0; titleIndex < titles.Count; titleIndex++)
                        {
                            for (var descriptionIndex = 0; descriptionIndex < descriptions.Count; descriptionIndex++)
                            {
                                var id = BuildId(definition.Id, period, kind, gameKey, target, condition.Id, titleIndex, descriptionIndex);
                                var title = FormatText(titles[titleIndex], target, game, condition);
                                var description = FormatText(descriptions[descriptionIndex], target, game, condition);
                                var template = new QuestTemplate(
                                    id,
                                    title,
                                    description,
                                    period,
                                    kind,
                                    gameKey,
                                    target,
                                    definition.RewardXp,
                                    definition.RewardCoins,
                                    condition.MinStake,
                                    condition.MaxStake,
                                    condition.MinPayout,
                                    condition.MinProfit,
                                    condition.MinMultiplier,
                                    NormalizeRarity(definition.Rarity),
                                    string.IsNullOrWhiteSpace(definition.Cluster) ? "core" : SanitizeId(definition.Cluster),
                                    Math.Max(0, definition.MinLevel),
                                    Math.Max(0, definition.MinGamesPlayed),
                                    Math.Max(0, definition.MinTotalStaked));

                                var repeatKey = $"{period}:{SanitizeId(definition.Id)}:{kind}:{(gameKey is null ? "any" : SanitizeId(gameKey))}";
                                result.Add(new QuestCandidate(
                                    template,
                                    BuildCandidateTags(definition, period, kind, gameKey),
                                    repeatKey,
                                    RarityWeight(definition.Rarity)));
                            }
                        }
                    }
                }
            }
        }

        return result;
    }

    private static string BuildId(
        string definitionId,
        string period,
        string kind,
        string? gameKey,
        int target,
        string conditionId,
        int titleIndex,
        int descriptionIndex)
    {
        var game = gameKey is null ? "any" : SanitizeId(gameKey);
        if (conditionId == "base")
            return $"{period}_{SanitizeId(definitionId)}_{kind}_{game}_t{target}_v{titleIndex}_{descriptionIndex}";

        return $"{period}_{SanitizeId(definitionId)}_{kind}_{game}_t{target}_{conditionId}_v{titleIndex}_{descriptionIndex}";
    }

    private static string FormatText(string text, int target, QuestGame? game, QuestCondition condition)
    {
        return text
            .Replace("{target}", target.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{game}", game?.Title ?? "любую игру", StringComparison.Ordinal)
            .Replace("{gameCommand}", game?.Command ?? "/games", StringComparison.Ordinal)
            .Replace("{minStake}", condition.MinStake?.ToString(CultureInfo.InvariantCulture) ?? "0", StringComparison.Ordinal)
            .Replace("{maxStake}", condition.MaxStake?.ToString(CultureInfo.InvariantCulture) ?? "0", StringComparison.Ordinal)
            .Replace("{minPayout}", condition.MinPayout?.ToString(CultureInfo.InvariantCulture) ?? "0", StringComparison.Ordinal)
            .Replace("{minProfit}", condition.MinProfit?.ToString(CultureInfo.InvariantCulture) ?? "0", StringComparison.Ordinal)
            .Replace("{minMultiplier}", condition.MinMultiplier?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> BuildCandidateTags(
        QuestDefinition definition,
        string period,
        string kind,
        string? gameKey)
    {
        var tags = definition.Tags.Select(NormalizeTag)
            .Append(period)
            .Append(kind)
            .Append(NormalizeTag(definition.Cluster))
            .Append(NormalizeRarity(definition.Rarity));

        if (!string.IsNullOrWhiteSpace(gameKey))
            tags = tags.Append(NormalizeTag(gameKey));

        return tags.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool MatchesKind(QuestTemplate quest, GameCompletedMetaEvent ev)
    {
        if (quest.MinStake is { } minStake && ev.Stake < minStake) return false;
        if (quest.MaxStake is { } maxStake && ev.Stake > maxStake) return false;
        if (quest.MinPayout is { } minPayout && ev.Payout < minPayout) return false;
        if (quest.MinProfit is { } minProfit && ev.Payout - ev.Stake < minProfit) return false;
        if (quest.MinMultiplier is { } minMultiplier && ev.Multiplier < minMultiplier) return false;

        return quest.Kind switch
        {
            "play" or "play_game" or "low_stake" or "high_stake" => true,
            "win" => ev.IsWin,
            "loss" => !ev.IsWin,
            "volume" => ev.Stake > 0,
            "payout" => ev.Payout > 0,
            "profit" => ev.Payout > ev.Stake,
            "multiplier" => ev.IsWin && ev.Multiplier > 0,
            _ => false,
        };
    }

    private static bool IsUnlocked(QuestTemplate quest, QuestPlayerProgress progress)
    {
        return progress.Level >= quest.MinLevel &&
               progress.GamesPlayed >= quest.MinGamesPlayed &&
               progress.TotalStaked >= quest.MinTotalStaked;
    }

    private static IReadOnlyList<QuestCondition> BuildConditions(QuestDefinition definition)
    {
        var conditions = new List<QuestCondition>();

        foreach (var value in definition.MinStakes)
            conditions.Add(new QuestCondition($"minstake{value}", MinStake: value));
        foreach (var value in definition.MaxStakes)
            conditions.Add(new QuestCondition($"maxstake{value}", MaxStake: value));
        foreach (var value in definition.MinPayouts)
            conditions.Add(new QuestCondition($"minpayout{value}", MinPayout: value));
        foreach (var value in definition.MinProfits)
            conditions.Add(new QuestCondition($"minprofit{value}", MinProfit: value));
        foreach (var value in definition.MinMultipliers)
            conditions.Add(new QuestCondition($"minmult{SanitizeId(value.ToString("0.##", CultureInfo.InvariantCulture))}", MinMultiplier: value));

        return conditions.Count == 0 ? [new QuestCondition("base")] : conditions;
    }

    private static QuestCandidate? Pick(
        IReadOnlyList<QuestCandidate> candidates,
        string seed,
        ISet<string> hardBlocked,
        ISet<string> hardBlockedRepeatKeys,
        ISet<string> softBlocked,
        SeasonQuestRotationConfig rotation)
    {
        var eligible = candidates
            .Where(x => !hardBlocked.Contains(x.Template.Id))
            .Where(x => !hardBlockedRepeatKeys.Contains(x.RepeatKey))
            .ToArray();
        if (eligible.Length == 0)
            return null;

        var preferred = eligible.Where(x => !softBlocked.Contains(x.RepeatKey)).ToArray();
        var pool = preferred.Length == 0 ? eligible : preferred;
        var focusPool = pool.Where(x => MatchesFocus(x, rotation.Focus)).ToArray();
        return PickWeighted(focusPool.Length == 0 ? pool : focusPool, seed, rotation);
    }

    private static QuestCandidate PickWeighted(
        IReadOnlyList<QuestCandidate> candidates,
        string seed,
        SeasonQuestRotationConfig rotation)
    {
        var totalWeight = candidates.Sum(x => EffectiveWeight(x, rotation));
        var bucket = (int)(StableHash(seed) % (ulong)totalWeight);
        var cursor = 0;
        foreach (var candidate in candidates)
        {
            cursor += EffectiveWeight(candidate, rotation);
            if (bucket < cursor)
                return candidate;
        }

        return candidates[^1];
    }

    private static IEnumerable<string> PreviousRawPicks(
        MetaSeason season,
        long chatId,
        long userId,
        DateTimeOffset now,
        QuestSlot slot,
        int index,
        IReadOnlyList<QuestCandidate> candidates,
        SeasonQuestRotationConfig rotation)
    {
        for (var i = 1; i <= slot.RepeatCooldownPeriods; i++)
        {
            var previous = slot.Period == "weekly" ? now.AddDays(-7 * i) : now.AddDays(-i);
            var seed = $"{season.Id}:{chatId}:{userId}:{PeriodKey(slot.Period, previous)}:{slot.Id}:{index}";
            var picked = Pick(
                candidates,
                seed,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                rotation);
            if (picked is not null)
                yield return picked.RepeatKey;
        }
    }

    private static bool HasTags(QuestCandidate candidate, IReadOnlyList<string> tags)
    {
        return tags.All(tag => candidate.Tags.Contains(tag, StringComparer.Ordinal));
    }

    private static bool MatchesFocus(QuestCandidate candidate, string focus)
    {
        return focus switch
        {
            "all-round" or "normal" or "balanced" => true,
            "daily" or "weekly" => string.Equals(candidate.Template.Period, focus, StringComparison.Ordinal),
            "volume" => candidate.Template.Kind is "volume" or "high_stake",
            "payout" => candidate.Template.Kind is "payout" or "profit" or "multiplier",
            "streaks" => candidate.Tags.Contains("streak", StringComparer.Ordinal) ||
                         candidate.Template.Cluster.Contains("streak", StringComparison.OrdinalIgnoreCase),
            "clans" => candidate.Tags.Contains("clan", StringComparer.Ordinal),
            "tournaments" => candidate.Tags.Contains("tournament", StringComparer.Ordinal),
            "controlled" => candidate.Template.Kind is "low_stake" or "play" or "loss",
            _ => candidate.Tags.Contains(NormalizeTag(focus), StringComparer.Ordinal) ||
                 string.Equals(candidate.Template.Kind, focus, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(candidate.Template.Cluster, focus, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static int EffectiveWeight(QuestCandidate candidate, SeasonQuestRotationConfig rotation)
    {
        var weight = candidate.Weight;
        var rarity = NormalizeRarity(candidate.Template.Rarity);
        weight = rotation.RarityBias switch
        {
            "uncommon" => rarity == "uncommon" ? weight * 2 : weight,
            "rare" => rarity is "rare" or "epic" or "legendary" ? weight * 3 : weight,
            "epic" => rarity is "epic" or "legendary" ? weight * 4 : weight,
            "common" => rarity == "common" ? weight * 2 : Math.Max(1, weight / 2),
            _ => weight,
        };

        return Math.Max(1, weight);
    }

    private static string NormalizePeriod(string period)
    {
        var normalized = period.Trim().ToLowerInvariant();
        return normalized is "weekly" ? "weekly" : "daily";
    }

    private static string NormalizeKind(string kind)
    {
        return kind.Trim().ToLowerInvariant();
    }

    private static string NormalizeRarity(string rarity)
    {
        var normalized = rarity.Trim().ToLowerInvariant();
        return normalized switch
        {
            "uncommon" or "rare" or "epic" or "legendary" => normalized,
            _ => "common",
        };
    }

    private static int RarityWeight(string rarity)
    {
        return NormalizeRarity(rarity) switch
        {
            "uncommon" => 60,
            "rare" => 25,
            "epic" => 10,
            "legendary" => 4,
            _ => 100,
        };
    }

    private static string NormalizeTag(string tag)
    {
        return tag.Trim().ToLowerInvariant();
    }

    private static string SanitizeId(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString().Trim('_');
    }

    private static ulong StableHash(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offset;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }

    private sealed record QuestCondition(
        string Id,
        long? MinStake = null,
        long? MaxStake = null,
        long? MinPayout = null,
        long? MinProfit = null,
        decimal? MinMultiplier = null);
    private sealed record QuestCandidate(QuestTemplate Template, IReadOnlyList<string> Tags, string RepeatKey, int Weight);
    private sealed record QuestSlot(string Id, string Period, IReadOnlyList<string> PoolTags, int Count, int RepeatCooldownPeriods);
    private sealed record QuestCatalogState(
        IReadOnlyList<QuestCandidate> Candidates,
        IReadOnlyList<QuestSlot> Slots,
        IReadOnlyList<QuestTemplate> All);
}

public sealed record QuestCatalogValidation(
    int QuestCount,
    int SlotCount,
    int DefinitionCount,
    int DailyQuestCount,
    int WeeklyQuestCount);

public static class QuestRegistry
{
    public static IReadOnlyList<QuestTemplate> All => JsonQuestCatalog.Default.All;

    public static IReadOnlyList<QuestTemplate> ActiveFor(
        MetaSeason season,
        long chatId,
        long userId,
        DateTimeOffset now,
        QuestPlayerProgress? progress = null) => JsonQuestCatalog.Default.ActiveFor(season, chatId, userId, now, progress);

    public static IEnumerable<QuestTemplate> Matching(
        MetaSeason season,
        long chatId,
        long userId,
        GameCompletedMetaEvent ev,
        QuestPlayerProgress? progress = null) => JsonQuestCatalog.Default.Matching(season, chatId, userId, ev, progress);

    public static IEnumerable<QuestTemplate> Matching(GameCompletedMetaEvent ev)
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(ev.OccurredAt);
        var season = new MetaSeason(0, "default", now.AddYears(-1), now.AddYears(1), "active", "{}");
        return Matching(season, ev.ChatId, ev.UserId, ev);
    }

    public static int DeltaFor(QuestTemplate quest, GameCompletedMetaEvent ev) => JsonQuestCatalog.DeltaFor(quest, ev);

    public static string PeriodKey(QuestTemplate quest, DateTimeOffset now) => JsonQuestCatalog.PeriodKey(quest, now);
}

internal sealed class QuestPoolDocument
{
    public List<QuestGame> Games { get; set; } = [];
    public List<QuestSlotDocument> Slots { get; set; } = [];
    public List<QuestDefinition> Definitions { get; set; } = [];
}

internal sealed class QuestGame
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Command { get; set; } = "";
}

internal sealed class QuestSlotDocument
{
    public string Id { get; set; } = "";
    public string Period { get; set; } = "daily";
    public List<string> PoolTags { get; set; } = [];
    public int Count { get; set; } = 1;
    public int RepeatCooldownPeriods { get; set; } = 1;
}

internal sealed class QuestDefinition
{
    public string Id { get; set; } = "";
    public string Period { get; set; } = "daily";
    public string Kind { get; set; } = "play";
    public List<string> Tags { get; set; } = [];
    public List<string> GameKeys { get; set; } = [];
    public List<int> Targets { get; set; } = [];
    public string Rarity { get; set; } = "common";
    public string Cluster { get; set; } = "core";
    public int MinLevel { get; set; }
    public int MinGamesPlayed { get; set; }
    public long MinTotalStaked { get; set; }
    public List<long> MinStakes { get; set; } = [];
    public List<long> MaxStakes { get; set; } = [];
    public List<long> MinPayouts { get; set; } = [];
    public List<long> MinProfits { get; set; } = [];
    public List<decimal> MinMultipliers { get; set; } = [];
    public long RewardXp { get; set; }
    public long RewardCoins { get; set; }
    public List<string> Titles { get; set; } = [];
    public List<string> Descriptions { get; set; } = [];
}
