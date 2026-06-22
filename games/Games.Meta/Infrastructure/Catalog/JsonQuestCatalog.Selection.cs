using System.Globalization;
using System.Text;

namespace Games.Meta.Infrastructure.Catalog;

public sealed partial class JsonQuestCatalog
{
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

}
