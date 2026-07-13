using BotFramework.Host.Execution;
using Games.Dice.Application.Execution;

namespace Games.Dice.Infrastructure.Persistence;

public sealed class DiceRollRecordWriter : GameRecordWriter<DiceRollRecord>
{
    protected override async Task WriteAsync(
        DiceRollRecord record,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        await context.ExecuteAsync(
            """
            INSERT INTO dice_rolls (id, user_id, dice_value, prize, loss, rolled_at)
            VALUES (gen_random_uuid(), @UserId, @DiceValue, @Prize, @Loss, @RolledAt)
            """,
            record,
            ct).ConfigureAwait(false);
    }
}
