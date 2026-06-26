using System.Globalization;
using System.Text;

namespace Games.Meta.Infrastructure.Catalog;

public sealed partial class JsonQuestCatalog
{
    private static QuestCatalogState BuildState(QuestPoolDocument document)
    {
        var games = document.Games.ToDictionary(
            x => x.Key,
            StringComparer.OrdinalIgnoreCase);

        var slots = document.Slots
            .Select(x => new QuestSlot(
                x.Id,
                NormalizePeriod(x.Period),
                x.PoolTags
                    .Select(NormalizeTag)
                    .ToArray(),
                Math.Max(1, x.Count),
                Math.Max(0, x.RepeatCooldownPeriods)))
            .ToArray();

        var candidates = BuildCandidates(
            document.Definitions,
            games);

        var all = candidates
            .Select(x => x.Template)
            .ToArray();

        var duplicate = all
            .GroupBy(
                x => x.Id,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Skip(1).Any());

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate quest id in {FileName}: {duplicate.Key}");
        }

        return new QuestCatalogState(
            candidates,
            slots,
            all);
    }

    private static List<QuestCandidate> BuildCandidates(
        IReadOnlyList<QuestDefinition> definitions,
        Dictionary<string, QuestGame> games)
    {
        var result = new List<QuestCandidate>();

        foreach (var definition in definitions)
        {
            var period = NormalizePeriod(definition.Period);
            var kind = NormalizeKind(definition.Kind);

            var targets = definition.Targets.Count == 0
                ? [1]
                : definition.Targets;

            var titles = definition.Titles.Count == 0
                ? [definition.Id]
                : definition.Titles;

            var descriptions = definition.Descriptions.Count == 0
                ? ["Выполни цель: {target}."]
                : definition.Descriptions;

            var gameKeys = definition.GameKeys.Count == 0
                ? [null]
                : definition.GameKeys.Cast<string?>().ToArray();

            var conditions = BuildConditions(definition);

            foreach (var gameKey in gameKeys)
            {
                QuestGame? game = null;

                if (gameKey is not null &&
                    !games.TryGetValue(gameKey, out game))
                {
                    throw new InvalidOperationException(
                        $"Quest definition {definition.Id} " +
                        $"references unknown game '{gameKey}'.");
                }

                foreach (var target in targets)
                {
                    foreach (var condition in conditions)
                    {
                        for (var titleIndex = 0;
                             titleIndex < titles.Count;
                             titleIndex++)
                        {
                            for (var descriptionIndex = 0;
                                 descriptionIndex < descriptions.Count;
                                 descriptionIndex++)
                            {
                                var id = BuildId(
                                    definition.Id,
                                    period,
                                    kind,
                                    gameKey,
                                    target,
                                    condition.Id,
                                    titleIndex,
                                    descriptionIndex);

                                var title = FormatText(
                                    titles[titleIndex],
                                    target,
                                    game,
                                    condition);

                                var description = FormatText(
                                    descriptions[descriptionIndex],
                                    target,
                                    game,
                                    condition);

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
                                    string.IsNullOrWhiteSpace(definition.Cluster)
                                        ? "core"
                                        : SanitizeId(definition.Cluster),
                                    Math.Max(0, definition.MinLevel),
                                    Math.Max(0, definition.MinGamesPlayed),
                                    Math.Max(0, definition.MinTotalStaked));

                                var normalizedGameKey = gameKey is null
                                    ? "any"
                                    : SanitizeId(gameKey);

                                var repeatKey =
                                    $"{period}:" +
                                    $"{SanitizeId(definition.Id)}:" +
                                    $"{kind}:" +
                                    $"{normalizedGameKey}";

                                result.Add(
                                    new QuestCandidate(
                                        template,
                                        BuildCandidateTags(
                                            definition,
                                            period,
                                            kind,
                                            gameKey),
                                        repeatKey,
                                        RarityWeight(
                                            definition.Rarity)));
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
        var game = gameKey is null
            ? "any"
            : SanitizeId(gameKey);

        if (string.Equals(
                conditionId,
                "base",
                StringComparison.Ordinal))
        {
            return
                $"{period}_" +
                $"{SanitizeId(definitionId)}_" +
                $"{kind}_" +
                $"{game}_" +
                string.Create(CultureInfo.InvariantCulture, $"t{target}_") +
                $"v{titleIndex}_{descriptionIndex}";
        }

        return
            $"{period}_" +
            $"{SanitizeId(definitionId)}_" +
            $"{kind}_" +
            $"{game}_" +
            $"t{target}_" +
            $"{conditionId}_" +
            $"v{titleIndex}_{descriptionIndex}";
    }

    private static string FormatText(
        string text,
        int target,
        QuestGame? game,
        QuestCondition condition)
    {
        return text
            .Replace(
                "{target}",
                target.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace(
                "{game}",
                game?.Title ?? "любую игру",
                StringComparison.Ordinal)
            .Replace(
                "{gameCommand}",
                game?.Command ?? "/games",
                StringComparison.Ordinal)
            .Replace(
                "{minStake}",
                condition.MinStake?.ToString(
                    CultureInfo.InvariantCulture) ?? "0",
                StringComparison.Ordinal)
            .Replace(
                "{maxStake}",
                condition.MaxStake?.ToString(
                    CultureInfo.InvariantCulture) ?? "0",
                StringComparison.Ordinal)
            .Replace(
                "{minPayout}",
                condition.MinPayout?.ToString(
                    CultureInfo.InvariantCulture) ?? "0",
                StringComparison.Ordinal)
            .Replace(
                "{minProfit}",
                condition.MinProfit?.ToString(
                    CultureInfo.InvariantCulture) ?? "0",
                StringComparison.Ordinal)
            .Replace(
                "{minMultiplier}",
                condition.MinMultiplier?.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture) ?? "0",
                StringComparison.Ordinal);
    }

    private static string[] BuildCandidateTags(
        QuestDefinition definition,
        string period,
        string kind,
        string? gameKey)
    {
        var tags = definition.Tags
            .Select(NormalizeTag)
            .Append(period)
            .Append(kind)
            .Append(NormalizeTag(definition.Cluster))
            .Append(NormalizeRarity(definition.Rarity));

        if (!string.IsNullOrWhiteSpace(gameKey))
        {
            tags = tags.Append(NormalizeTag(gameKey));
        }

        return tags
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool MatchesKind(
        QuestTemplate quest,
        GameCompletedMetaEvent ev)
    {
        if (quest.MinStake is { } minStake &&
            ev.Stake < minStake)
        {
            return false;
        }

        if (quest.MaxStake is { } maxStake &&
            ev.Stake > maxStake)
        {
            return false;
        }

        if (quest.MinPayout is { } minPayout &&
            ev.Payout < minPayout)
        {
            return false;
        }

        if (quest.MinProfit is { } minProfit &&
            ev.Payout - ev.Stake < minProfit)
        {
            return false;
        }

        if (quest.MinMultiplier is { } minMultiplier &&
            ev.Multiplier < minMultiplier)
        {
            return false;
        }

        return quest.Kind switch
        {
            "play" or
            "play_game" or
            "low_stake" or
            "high_stake" => true,

            "win" => ev.IsWin,
            "loss" => !ev.IsWin,
            "volume" => ev.Stake > 0,
            "payout" => ev.Payout > 0,
            "profit" => ev.Payout > ev.Stake,

            "multiplier" =>
                ev is { IsWin: true, Multiplier: > 0 },

            _ => false,
        };
    }

    private static bool IsUnlocked(
        QuestTemplate quest,
        QuestPlayerProgress progress)
    {
        return
            progress.Level >= quest.MinLevel &&
            progress.GamesPlayed >= quest.MinGamesPlayed &&
            progress.TotalStaked >= quest.MinTotalStaked;
    }

    private static List<QuestCondition> BuildConditions(
        QuestDefinition definition)
    {
        var conditions = definition.MinStakes
            .ConvertAll(value => new QuestCondition(
                $"minstake{value}",
                MinStake: value))
;

        conditions.AddRange(
            definition.MaxStakes.Select(
                value => new QuestCondition(
                    $"maxstake{value}",
                    MaxStake: value)));

        conditions.AddRange(
            definition.MinPayouts.Select(
                value => new QuestCondition(
                    $"minpayout{value}",
                    MinPayout: value)));

        conditions.AddRange(
            definition.MinProfits.Select(
                value => new QuestCondition(
                    $"minprofit{value}",
                    MinProfit: value)));

        conditions.AddRange(
            definition.MinMultipliers.Select(
                value => new QuestCondition(
                    $"minmult{SanitizeId(
                        value.ToString(
                            "0.##",
                            CultureInfo.InvariantCulture))}",
                    MinMultiplier: value)));

        if (conditions.Count == 0)
        {
            conditions.Add(
                new QuestCondition("base"));
        }

        return conditions;
    }

    private static QuestCandidate? Pick(
        IReadOnlyList<QuestCandidate> candidates,
        string seed,
        HashSet<string> hardBlocked,
        HashSet<string> hardBlockedRepeatKeys,
        HashSet<string> softBlocked,
        SeasonQuestRotationConfig rotation)
    {
        var eligible = candidates
            .Where(candidate =>
                !hardBlocked.Contains(candidate.Template.Id) && !hardBlockedRepeatKeys.Contains(
                    candidate.RepeatKey))
            .ToArray();

        if (eligible.Length == 0)
        {
            return null;
        }

        var preferred = eligible
            .Where(candidate =>
                !softBlocked.Contains(candidate.RepeatKey))
            .ToArray();

        var pool = preferred.Length == 0
            ? eligible
            : preferred;

        var focusPool = pool
            .Where(candidate =>
                MatchesFocus(candidate, rotation.Focus))
            .ToArray();

        return PickWeighted(
            focusPool.Length == 0
                ? pool
                : focusPool,
            seed,
            rotation);
    }

    private static QuestCandidate PickWeighted(
        IReadOnlyList<QuestCandidate> candidates,
        string seed,
        SeasonQuestRotationConfig rotation)
    {
        var totalWeight = candidates.Sum(
            candidate =>
                EffectiveWeight(candidate, rotation));

        var bucket = (int)(
            StableHash(seed) %
            (ulong)totalWeight);

        var cursor = 0;

        foreach (var candidate in candidates)
        {
            cursor += EffectiveWeight(
                candidate,
                rotation);

            if (bucket < cursor)
            {
                return candidate;
            }
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
        for (var i = 1;
             i <= slot.RepeatCooldownPeriods;
             i++)
        {
            var previous = string.Equals(
                slot.Period,
                "weekly",
                StringComparison.Ordinal)
                    ? now.AddDays(-7 * i)
                    : now.AddDays(-i);

            var seed =
                $"{season.Id}:" +
                $"{chatId}:" +
                $"{userId}:" +
                $"{PeriodKey(slot.Period, previous)}:" +
                $"{slot.Id}:" +
                $"{index}";

            var picked = Pick(
                candidates,
                seed,
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase),
                rotation);

            if (picked is not null)
            {
                yield return picked.RepeatKey;
            }
        }
    }

    private static bool HasTags(
        QuestCandidate candidate,
        IReadOnlyList<string> tags)
    {
        return tags.All(
            tag => candidate.Tags.Contains(
                tag,
                StringComparer.Ordinal));
    }

    private static bool MatchesFocus(
        QuestCandidate candidate,
        string focus)
    {
        if (string.Equals(
                focus,
                "all-round",
                StringComparison.Ordinal) ||
            string.Equals(
                focus,
                "normal",
                StringComparison.Ordinal) ||
            string.Equals(
                focus,
                "balanced",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(
                focus,
                "daily",
                StringComparison.Ordinal) ||
            string.Equals(
                focus,
                "weekly",
                StringComparison.Ordinal))
        {
            return string.Equals(
                candidate.Template.Period,
                focus,
                StringComparison.Ordinal);
        }

        if (string.Equals(
                focus,
                "volume",
                StringComparison.Ordinal))
        {
            return
                string.Equals(
                    candidate.Template.Kind,
                    "volume",
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.Template.Kind,
                    "high_stake",
                    StringComparison.Ordinal);
        }

        if (string.Equals(
                focus,
                "payout",
                StringComparison.Ordinal))
        {
            return
                string.Equals(
                    candidate.Template.Kind,
                    "payout",
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.Template.Kind,
                    "profit",
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.Template.Kind,
                    "multiplier",
                    StringComparison.Ordinal);
        }

        if (string.Equals(
                focus,
                "streaks",
                StringComparison.Ordinal))
        {
            return
                candidate.Tags.Contains(
                    "streak",
                    StringComparer.Ordinal) ||
                candidate.Template.Cluster.Contains(
                    "streak",
                    StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(
                focus,
                "clans",
                StringComparison.Ordinal))
        {
            return candidate.Tags.Contains(
                "clan",
                StringComparer.Ordinal);
        }

        if (string.Equals(
                focus,
                "tournaments",
                StringComparison.Ordinal))
        {
            return candidate.Tags.Contains(
                "tournament",
                StringComparer.Ordinal);
        }

        if (string.Equals(
                focus,
                "controlled",
                StringComparison.Ordinal))
        {
            return
                string.Equals(
                    candidate.Template.Kind,
                    "low_stake",
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.Template.Kind,
                    "play",
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.Template.Kind,
                    "loss",
                    StringComparison.Ordinal);
        }

        return
            candidate.Tags.Contains(
                NormalizeTag(focus),
                StringComparer.Ordinal) ||
            string.Equals(
                candidate.Template.Kind,
                focus,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                candidate.Template.Cluster,
                focus,
                StringComparison.OrdinalIgnoreCase);
    }

    private static int EffectiveWeight(
        QuestCandidate candidate,
        SeasonQuestRotationConfig rotation)
    {
        var weight = candidate.Weight;
        var rarity = NormalizeRarity(
            candidate.Template.Rarity);

        if (string.Equals(
                rotation.RarityBias,
                "uncommon",
                StringComparison.Ordinal))
        {
            if (string.Equals(
                    rarity,
                    "uncommon",
                    StringComparison.Ordinal))
            {
                weight *= 2;
            }
        }
        else if (string.Equals(
                     rotation.RarityBias,
                     "rare",
                     StringComparison.Ordinal))
        {
            if (string.Equals(
                    rarity,
                    "rare",
                    StringComparison.Ordinal) ||
                string.Equals(
                    rarity,
                    "epic",
                    StringComparison.Ordinal) ||
                string.Equals(
                    rarity,
                    "legendary",
                    StringComparison.Ordinal))
            {
                weight *= 3;
            }
        }
        else if (string.Equals(
                     rotation.RarityBias,
                     "epic",
                     StringComparison.Ordinal))
        {
            if (string.Equals(
                    rarity,
                    "epic",
                    StringComparison.Ordinal) ||
                string.Equals(
                    rarity,
                    "legendary",
                    StringComparison.Ordinal))
            {
                weight *= 4;
            }
        }
        else if (string.Equals(
                     rotation.RarityBias,
                     "common",
                     StringComparison.Ordinal))
        {
            weight = string.Equals(
                rarity,
                "common",
                StringComparison.Ordinal)
                    ? weight * 2
                    : Math.Max(1, weight / 2);
        }

        return Math.Max(1, weight);
    }

    private static string NormalizePeriod(string period)
    {
        var normalized = period
            .Trim()
            .ToLowerInvariant();

        return string.Equals(
            normalized,
            "weekly",
            StringComparison.Ordinal)
                ? "weekly"
                : "daily";
    }

    private static string NormalizeKind(string kind)
    {
        return kind
            .Trim()
            .ToLowerInvariant();
    }

    private static string NormalizeRarity(string rarity)
    {
        var normalized = rarity
            .Trim()
            .ToLowerInvariant();

        if (string.Equals(
                normalized,
                "uncommon",
                StringComparison.Ordinal) ||
            string.Equals(
                normalized,
                "rare",
                StringComparison.Ordinal) ||
            string.Equals(
                normalized,
                "epic",
                StringComparison.Ordinal) ||
            string.Equals(
                normalized,
                "legendary",
                StringComparison.Ordinal))
        {
            return normalized;
        }

        return "common";
    }

    private static int RarityWeight(string rarity)
    {
        var normalized = NormalizeRarity(rarity);

        if (string.Equals(
                normalized,
                "uncommon",
                StringComparison.Ordinal))
        {
            return 60;
        }

        if (string.Equals(
                normalized,
                "rare",
                StringComparison.Ordinal))
        {
            return 25;
        }

        if (string.Equals(
                normalized,
                "epic",
                StringComparison.Ordinal))
        {
            return 10;
        }

        if (string.Equals(
                normalized,
                "legendary",
                StringComparison.Ordinal))
        {
            return 4;
        }

        return 100;
    }

    private static string NormalizeTag(string tag)
    {
        return tag
            .Trim()
            .ToLowerInvariant();
    }

    private static string SanitizeId(string value)
    {
        var normalized = value
            .Trim()
            .ToLowerInvariant();

        var builder = new StringBuilder(
            normalized.Length);

        foreach (var ch in normalized)
        {
            builder.Append(
                char.IsLetterOrDigit(ch)
                    ? ch
                    : '_');
        }

        return builder
            .ToString()
            .Trim('_');
    }

    private static ulong StableHash(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offset;

        foreach (var valueByte in Encoding.UTF8.GetBytes(value))
        {
            hash ^= valueByte;
            hash *= prime;
        }

        return hash;
    }
}