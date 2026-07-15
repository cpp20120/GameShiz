using System.Text.Json;
using System.Text.Json.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using Games.Challenges.Application.Execution;

namespace Games.Challenges.Infrastructure.Persistence;

public sealed class ChallengeExecutionStateStore<TCommand>(IEconomicsService economics)
    : IGameStateStore<TCommand, ChallengeExecutionState>
    where TCommand : IChallengeExecutionCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ChallengeExecutionState> LoadAsync(
        TCommand command, IGameExecutionContext context, CancellationToken ct)
    {
        if (command is ChallengeCreateCommand create)
        {
            var hasPending = await context.QuerySingleOrDefaultAsync<bool>("""
                SELECT EXISTS(
                    SELECT 1 FROM challenge_duels
                    WHERE chat_id=@ChatId AND status='Pending' AND expires_at>now()
                      AND ((challenger_id=@ActorUserId AND target_id=@targetId)
                        OR (challenger_id=@targetId AND target_id=@ActorUserId)))
                """, new { create.ChatId, create.ActorUserId, targetId = create.Target.UserId }, ct);
            return new(null, hasPending, 0, 0);
        }

        var json = await context.QuerySingleOrDefaultAsync<string>(ChallengeSelect,
            new { command.ChallengeId }, ct);
        var challenge = json is null ? null : JsonSerializer.Deserialize<Challenge>(json, JsonOptions);
        if (challenge is null) return new(null, false, 0, 0);

        var challengerBalance = 0;
        var targetBalance = 0;
        if (command.EnsureExpectedWallets)
        {
            await economics.EnsureUserAsync(challenge.ChallengerId, challenge.ChatId, challenge.ChallengerName, ct);
            await economics.EnsureUserAsync(challenge.TargetId, challenge.ChatId, challenge.TargetName, ct);
            challengerBalance = await economics.GetBalanceAsync(challenge.ChallengerId, challenge.ChatId, ct);
            targetBalance = await economics.GetBalanceAsync(challenge.TargetId, challenge.ChatId, ct);
        }
        return new(challenge, false, challengerBalance, targetBalance);
    }

    public async Task SaveAsync(
        TCommand command, ChallengeExecutionState state, IGameExecutionContext context, CancellationToken ct)
    {
        if (state.Challenge is not { } challenge) return;
        await context.ExecuteAsync("""
            INSERT INTO challenge_duels
                (id,chat_id,challenger_id,challenger_name,target_id,target_name,amount,game,status,
                 created_at,expires_at,responded_at,completed_at)
            VALUES
                (@Id,@ChatId,@ChallengerId,@ChallengerName,@TargetId,@TargetName,@Amount,@Game,@Status,
                 @CreatedAt,@ExpiresAt,
                 CASE WHEN @Status='Pending' THEN NULL ELSE now() END,
                 CASE WHEN @terminal THEN now() ELSE NULL END)
            ON CONFLICT (id) DO UPDATE SET
                status=EXCLUDED.status,
                responded_at=COALESCE(challenge_duels.responded_at,EXCLUDED.responded_at),
                completed_at=CASE WHEN @terminal THEN COALESCE(challenge_duels.completed_at,now())
                                  ELSE challenge_duels.completed_at END
            """, new
        {
            challenge.Id, challenge.ChatId, challenge.ChallengerId, challenge.ChallengerName,
            challenge.TargetId, challenge.TargetName, challenge.Amount,
            Game = challenge.Game.ToString(), Status = challenge.Status.ToString(),
            challenge.CreatedAt, challenge.ExpiresAt,
            terminal = challenge.Status is ChallengeStatus.Completed or ChallengeStatus.Failed or ChallengeStatus.Declined,
        }, ct);
    }

    private const string ChallengeSelect = """
        SELECT json_build_object(
            'Id',id,'ChatId',chat_id,'ChallengerId',challenger_id,'ChallengerName',challenger_name,
            'TargetId',target_id,'TargetName',target_name,'Amount',amount,'Game',game,'Status',status,
            'CreatedAt',created_at,'ExpiresAt',expires_at)::text
        FROM challenge_duels WHERE id=@ChallengeId FOR UPDATE
        """;
}
