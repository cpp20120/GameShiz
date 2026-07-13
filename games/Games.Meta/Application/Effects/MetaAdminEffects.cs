using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Admin.Execution;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Models;
using Games.Meta.Infrastructure.Catalog;
using Games.Meta.Domain.Seasons;

namespace Games.Meta.Application.Effects;

public sealed record MetaSeasonCreateAdminEffect(
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string ConfigJson) : IAdminEffect;

public sealed record MetaSeasonPrepareAdminEffect(int RequestedCount, int DurationDays) : IAdminEffect;
public sealed record MetaSeasonActivateAdminEffect(long SeasonId) : IAdminEffect;
public sealed record MetaSeasonFinishAdminEffect(long SeasonId) : IAdminEffect;
public sealed record MetaSeasonConfigAdminEffect(long SeasonId, string ConfigJson, bool Structured) : IAdminEffect;
public sealed record MetaSeasonPlayerRewardsAdminEffect(long SeasonId) : IAdminEffect;
public sealed record MetaSeasonClanRewardsAdminEffect(long SeasonId) : IAdminEffect;
public sealed record MetaAlertStatusAdminEffect(long FlagId, string TargetStatus) : IAdminEffect;
public sealed record MetaQuestCatalogSaveAdminEffect(string FormattedJson) : IAdminEffect;
public sealed record MetaQuestCatalogReloadAdminEffect : IAdminEffect;

internal abstract class MetaAdminEffectHandler<TEffect> : AdminEffectHandler<TEffect>
    where TEffect : class, IAdminEffect
{
    protected static Task AppendHistoryAsync(
        IAdminExecutionContext context,
        string eventType,
        string aggregateType,
        string aggregateId,
        long? seasonId,
        object payload,
        CancellationToken ct) =>
        context.ExecuteAsync(
            """
            INSERT INTO meta_event_log
                (event_type, aggregate_type, aggregate_id, season_id, user_id, payload)
            VALUES (@eventType, @aggregateType, @aggregateId, @seasonId, @userId, CAST(@payloadJson AS jsonb))
            """,
            new
            {
                eventType,
                aggregateType,
                aggregateId,
                seasonId,
                userId = context.Actor.Id,
                payloadJson = JsonSerializer.Serialize(payload),
            },
            ct);

    protected static async Task<WalletRow> LockWalletAsync(
        IAdminExecutionContext context,
        long userId,
        long chatId,
        CancellationToken ct)
    {
        var row = await context.QuerySingleOrDefaultAsync<WalletRow>(
            """
            SELECT coins AS Coins, version AS Version
            FROM users
            WHERE telegram_user_id = @userId AND balance_scope_id = @chatId
            FOR UPDATE
            """,
            new { userId, chatId },
            ct).ConfigureAwait(false);
        return row ?? throw new InvalidOperationException($"Wallet {userId}:{chatId} is missing.");
    }

    protected static async Task CreditAsync(
        IAdminExecutionContext context,
        long userId,
        long chatId,
        string displayName,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        await context.ExecuteAsync(
            """
            INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins)
            VALUES (@userId, @chatId, @displayName, 0)
            ON CONFLICT (telegram_user_id, balance_scope_id)
            DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now()
            """,
            new { userId, chatId, displayName, }, ct).ConfigureAwait(false);

        var existing = await context.QuerySingleOrDefaultAsync<int?>(
            "SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId",
            new { operationId }, ct).ConfigureAwait(false);
        if (existing.HasValue) return;

        var wallet = await LockWalletAsync(context, userId, chatId, ct).ConfigureAwait(false);
        // Another reward command may have inserted the same deterministic
        // operation while this transaction waited for the wallet row lock.
        // Re-check after FOR UPDATE to keep reward delivery idempotent.
        existing = await context.QuerySingleOrDefaultAsync<int?>(
            "SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId",
            new { operationId }, ct).ConfigureAwait(false);
        if (existing.HasValue) return;

        var balance = checked(wallet.Coins + amount);
        await context.ExecuteAsync(
            """
            UPDATE users
            SET coins = @balance, version = @version, updated_at = now()
            WHERE telegram_user_id = @userId AND balance_scope_id = @chatId
            """,
            new { userId, chatId, balance, version = checked(wallet.Version + 1) }, ct).ConfigureAwait(false);
        await context.ExecuteAsync(
            """
            INSERT INTO economics_ledger
                (telegram_user_id, balance_scope_id, delta, balance_after, reason, operation_id)
            VALUES (@userId, @chatId, @amount, @balance, @reason, @operationId)
            """,
            new { userId, chatId, amount, balance, reason, operationId }, ct).ConfigureAwait(false);
    }

    protected sealed record WalletRow(int Coins, long Version);
}

internal sealed class MetaSeasonCreateAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonCreateAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonCreateAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var id = await context.QuerySingleOrDefaultAsync<long?>(
            """
            INSERT INTO meta_seasons (name, starts_at, ends_at, status, config)
            VALUES (@name, @startsAt, @endsAt, 'planned', CAST(@configJson AS jsonb))
            RETURNING id
            """,
            new { name = effect.Name, startsAt = effect.StartsAt, endsAt = effect.EndsAt, configJson = effect.ConfigJson }, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Season creation did not return an id.");
        context.SetOutput("seasonId", id);
        await AppendHistoryAsync(context, "season.created", "season", id.ToString(CultureInfo.InvariantCulture), id,
            new { id, effect.Name, effect.StartsAt, effect.EndsAt }, ct).ConfigureAwait(false);
    }
}

internal sealed class MetaSeasonPrepareAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonPrepareAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonPrepareAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var existing = await context.QuerySingleOrDefaultAsync<int>(
            "SELECT count(*)::int FROM meta_seasons WHERE status = 'planned' AND ends_at > now()",
            null, ct).ConfigureAwait(false);
        var toCreate = Math.Max(0, effect.RequestedCount - existing);
        var created = 0;
        if (toCreate > 0)
        {
            var startsAt = await context.QuerySingleOrDefaultAsync<DateTimeOffset>(
                "SELECT COALESCE(max(ends_at), date_trunc('day', now())) FROM meta_seasons WHERE status IN ('active', 'planned')",
                null, ct).ConfigureAwait(false);
            var startNumber = await context.QuerySingleOrDefaultAsync<int>(
                "SELECT count(*)::int + 1 FROM meta_seasons", null, ct).ConfigureAwait(false);
            foreach (var plan in SeasonPlanFactory.CreatePlans(startsAt, toCreate, effect.DurationDays, startNumber))
            {
                await context.ExecuteAsync(
                    """
                    INSERT INTO meta_seasons (name, starts_at, ends_at, status, config)
                    VALUES (@name, @startsAt, @endsAt, 'planned', CAST(@configJson AS jsonb))
                    """,
                    new { name = plan.Name, startsAt = plan.StartsAt, endsAt = plan.EndsAt, configJson = plan.ConfigJson }, ct).ConfigureAwait(false);
                created++;
            }
        }

        context.SetOutput("created", created);
        context.SetOutput("existingPlanned", existing);
        await AppendHistoryAsync(context, "season.prepared", "season", "planned", null,
            new { requested = effect.RequestedCount, existingPlanned = existing, created, durationDays = effect.DurationDays }, ct).ConfigureAwait(false);
    }
}

internal sealed class MetaSeasonActivateAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonActivateAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonActivateAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        await context.ExecuteAsync("UPDATE meta_seasons SET status = 'finished', updated_at = now() WHERE status = 'active'", null, ct).ConfigureAwait(false);
        var changed = await context.ExecuteAsync(
            "UPDATE meta_seasons SET status = 'active', starts_at = LEAST(starts_at, now()), updated_at = now() WHERE id = @seasonId AND status IN ('planned', 'finished')",
            new { effect.SeasonId }, ct).ConfigureAwait(false);
        context.SetOutput("changed", changed);
        if (changed > 0)
            await AppendHistoryAsync(context, "season.activated", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
                new { effect.SeasonId }, ct).ConfigureAwait(false);
    }
}

internal sealed class MetaSeasonFinishAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonFinishAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonFinishAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var changed = await context.ExecuteAsync(
            "UPDATE meta_seasons SET status = 'finished', ends_at = LEAST(ends_at, now()), updated_at = now() WHERE id = @seasonId AND status <> 'finished'",
            new { effect.SeasonId }, ct).ConfigureAwait(false);
        context.SetOutput("changed", changed);
        if (changed > 0)
            await AppendHistoryAsync(context, "season.finished", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
                new { effect.SeasonId }, ct).ConfigureAwait(false);
    }
}

internal sealed class MetaSeasonConfigAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonConfigAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonConfigAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var changed = await context.ExecuteAsync(
            "UPDATE meta_seasons SET config = CAST(@configJson AS jsonb), updated_at = now() WHERE id = @seasonId",
            new { seasonId = effect.SeasonId, effect.ConfigJson }, ct).ConfigureAwait(false);
        context.SetOutput("changed", changed);
        if (changed > 0)
            await AppendHistoryAsync(context, "season.config_updated", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
                new { effect.SeasonId, structured = effect.Structured }, ct).ConfigureAwait(false);
    }
}

internal sealed class MetaAlertStatusAdminEffectHandler : MetaAdminEffectHandler<MetaAlertStatusAdminEffect>
{
    protected override async Task ApplyAsync(MetaAlertStatusAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var changed = await context.ExecuteAsync(
            """
            UPDATE meta_risk_flags
            SET status = @targetStatus, resolved_at = now(), updated_at = now()
            WHERE id = @flagId AND status = 'open'
            """,
            new { flagId = effect.FlagId, effect.TargetStatus }, ct).ConfigureAwait(false);
        context.SetOutput("changed", changed);
        if (changed > 0)
            await AppendHistoryAsync(context, "meta_alert.updated", "risk_flag", effect.FlagId.ToString(CultureInfo.InvariantCulture), null,
                new { effect.FlagId, effect.TargetStatus }, ct).ConfigureAwait(false);
    }
}

internal sealed class MetaQuestCatalogSaveAdminEffectHandler : MetaAdminEffectHandler<MetaQuestCatalogSaveAdminEffect>
{
    protected override async Task ApplyAsync(MetaQuestCatalogSaveAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var path = JsonQuestCatalog.EditablePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, effect.FormattedJson, ct).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
            context.SetOutput("path", path);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}

internal sealed class MetaQuestCatalogReloadAdminEffectHandler(
    IQuestCatalog catalog) : MetaAdminEffectHandler<MetaQuestCatalogReloadAdminEffect>
{
    protected override Task ApplyAsync(MetaQuestCatalogReloadAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        if (catalog is JsonQuestCatalog jsonCatalog)
            jsonCatalog.Reload();
        return Task.CompletedTask;
    }
}

internal sealed class MetaSeasonPlayerRewardsAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonPlayerRewardsAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonPlayerRewardsAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var configJson = await context.QuerySingleOrDefaultAsync<string?>("SELECT config::text FROM meta_seasons WHERE id = @seasonId", new { effect.SeasonId }, ct).ConfigureAwait(false);
        if (configJson is null)
        {
            context.SetOutput("rows", Array.Empty<SeasonRewardPaidRow>());
            return;
        }

        var rewards = SeasonRewardsConfig.FromJson(configJson);
        var winners = await context.QueryAsync<PlayerSeasonRewardWinner>(
            """
            SELECT row_number() OVER (ORDER BY xp DESC, rating DESC, user_id ASC)::int AS Place,
                   chat_id AS ChatId, user_id AS UserId, display_name AS DisplayName
            FROM meta_season_players
            WHERE season_id = @seasonId
            ORDER BY xp DESC, rating DESC, user_id ASC
            LIMIT 10
            """,
            new { effect.SeasonId }, ct).ConfigureAwait(false);
        var rows = new List<SeasonRewardPaidRow>();
        foreach (var winner in winners)
        {
            var amount = rewards.PlayerRewardForPlace(winner.Place);
            if (amount <= 0) continue;
            await CreditAsync(context, winner.UserId, winner.ChatId, winner.DisplayName, amount, "season.reward",
                $"season:reward:{effect.SeasonId}:{winner.Place}:{winner.ChatId}:{winner.UserId}", ct).ConfigureAwait(false);
            rows.Add(new SeasonRewardPaidRow(winner.Place, winner.ChatId, winner.UserId, winner.DisplayName, amount));
        }

        context.SetOutput("rows", rows);
        await AppendHistoryAsync(context, "season.reward_paid", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
            new { effect.SeasonId, paid = rows.Count, winners = rows }, ct).ConfigureAwait(false);
    }
}

internal sealed class MetaSeasonClanRewardsAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonClanRewardsAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonClanRewardsAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var configJson = await context.QuerySingleOrDefaultAsync<string?>("SELECT config::text FROM meta_seasons WHERE id = @seasonId", new { effect.SeasonId }, ct).ConfigureAwait(false);
        if (configJson is null)
        {
            context.SetOutput("rows", Array.Empty<SeasonRewardPaidRow>());
            return;
        }

        var rewards = SeasonRewardsConfig.FromJson(configJson);
        var winners = await context.QueryAsync<ClanSeasonRewardWinner>(
            """
            SELECT row_number() OVER (ORDER BY sc.xp DESC, sc.rating DESC, sc.clan_id ASC)::int AS Place,
                   sc.chat_id AS ChatId, sc.clan_id AS ClanId, c.name AS ClanName, c.tag AS ClanTag,
                   c.owner_user_id AS OwnerUserId, COALESCE(m.display_name, c.owner_user_id::text) AS OwnerDisplayName
            FROM meta_season_clans sc
            JOIN meta_clans c ON c.id = sc.clan_id
            LEFT JOIN meta_clan_members m ON m.clan_id = c.id AND m.user_id = c.owner_user_id
            WHERE sc.season_id = @seasonId
            ORDER BY sc.xp DESC, sc.rating DESC, sc.clan_id ASC
            LIMIT 10
            """,
            new { effect.SeasonId }, ct).ConfigureAwait(false);
        var rows = new List<SeasonRewardPaidRow>();
        foreach (var winner in winners)
        {
            var amount = rewards.ClanRewardForPlace(winner.Place);
            if (amount <= 0) continue;
            await CreditAsync(context, winner.OwnerUserId, winner.ChatId, winner.OwnerDisplayName, amount, "season.clan_reward",
                $"season:clan-reward:{effect.SeasonId}:{winner.Place}:{winner.ChatId}:{winner.ClanId}:{winner.OwnerUserId}", ct).ConfigureAwait(false);
            rows.Add(new SeasonRewardPaidRow(winner.Place, winner.ChatId, winner.OwnerUserId, $"{winner.ClanTag} {winner.ClanName}", amount));
        }

        context.SetOutput("rows", rows);
        await AppendHistoryAsync(context, "season.clan_reward_paid", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
            new { effect.SeasonId, paid = rows.Count, winners = rows }, ct).ConfigureAwait(false);
    }
}
