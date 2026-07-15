using System.Text.Json;
using BotFramework.Sdk.Execution;
using Dapper;

namespace BotFramework.Host.Execution;

internal sealed class TransactionalScheduleCollector : ITransactionalScheduleCollector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(
        string commandId,
        string gameId,
        string aggregateId,
        IReadOnlyList<ScheduleEffect> effects,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        if (effects.Count == 0) return;

        foreach (var effect in effects) Validate(effect);

        const string sql = """
            INSERT INTO game_schedule_outbox (
                command_id,
                effect_index,
                game_id,
                schedule_id,
                effect_kind,
                job_key,
                due_at,
                data)
            SELECT @commandId,
                   batch.effect_index,
                   batch.game_id,
                   batch.schedule_id,
                   batch.effect_kind,
                   batch.job_key,
                   batch.due_at,
                   CAST(batch.data AS jsonb)
            FROM unnest(
                CAST(@effectIndexes AS integer[]),
                CAST(@gameIds AS text[]),
                CAST(@scheduleIds AS text[]),
                CAST(@effectKinds AS text[]),
                CAST(@jobKeys AS text[]),
                CAST(@dueAts AS timestamp with time zone[]),
                CAST(@data AS text[]))
                AS batch(effect_index, game_id, schedule_id, effect_kind, job_key, due_at, data)
            ON CONFLICT (command_id, effect_index) DO NOTHING
            """;

        await session.Connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                commandId,
                effectIndexes = Enumerable.Range(0, effects.Count).ToArray(),
                gameIds = effects.Select(_ => gameId).ToArray(),
                scheduleIds = effects.Select(effect => ScopeScheduleId(gameId, aggregateId, effect.ScheduleId)).ToArray(),
                effectKinds = effects.Select(effect => effect.Kind == ScheduleEffectKind.Schedule ? "schedule" : "cancel").ToArray(),
                jobKeys = effects.Select(effect => effect.JobKey).ToArray(),
                dueAts = effects.Select(effect => effect.DueAt).ToArray(),
                data = effects.Select(effect => JsonSerializer.Serialize(
                    effect.Data ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    JsonOptions)).ToArray(),
            },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    private static string ScopeScheduleId(string gameId, string aggregateId, string scheduleId) =>
        $"{gameId}:{aggregateId}:{scheduleId}";

    private static void Validate(ScheduleEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (string.IsNullOrWhiteSpace(effect.ScheduleId))
            throw new InvalidOperationException("A schedule effect requires a schedule id.");

        if (effect.Kind == ScheduleEffectKind.Schedule)
        {
            if (string.IsNullOrWhiteSpace(effect.JobKey))
                throw new InvalidOperationException("A schedule effect requires a job key.");
            if (effect.DueAt is null)
                throw new InvalidOperationException("A schedule effect requires a due time.");
            return;
        }

        if (effect.Kind != ScheduleEffectKind.Cancel)
            throw new InvalidOperationException($"Unknown schedule effect kind '{effect.Kind}'.");
    }
}
