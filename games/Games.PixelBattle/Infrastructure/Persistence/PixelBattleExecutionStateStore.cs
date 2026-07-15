using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using Games.PixelBattle.Application.Execution;

namespace Games.PixelBattle.Infrastructure.Persistence;

public sealed class PixelBattleExecutionStateStore(IWalletReadService wallet)
    : IGameStateStore<PixelBattleCommand, PixelBattleExecutionState>
{
    public async Task<PixelBattleExecutionState> LoadAsync(
        PixelBattleCommand command, IGameExecutionContext context, CancellationToken ct)
    {
        var knownUser = await wallet.ExistsAsync(command.UserId, ct);
        var tile = PixelBattleConstants.IsValidIndex(command.Index)
            ? await context.QuerySingleOrDefaultAsync<PixelBattleTileState>("""
                SELECT index AS Index,color AS Color,version AS Version,updated_by AS UpdatedBy
                FROM pixelbattle_tiles WHERE index=@Index FOR UPDATE
                """, new { command.Index }, ct)
            : null;
        var nextVersion = knownUser
            && PixelBattleConstants.IsValidIndex(command.Index)
            && PixelBattleConstants.IsValidColor(command.Color)
                ? await context.QuerySingleOrDefaultAsync<long>(
                    "SELECT nextval('pixelbattle_version_seq')::bigint", null, ct)
                : 0;
        return new(tile, knownUser, nextVersion);
    }

    public Task SaveAsync(
        PixelBattleCommand command,
        PixelBattleExecutionState state,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        if (state.Tile is not { } tile) return Task.CompletedTask;
        return context.ExecuteAsync("""
            INSERT INTO pixelbattle_tiles (index,color,version,updated_by,updated_at)
            VALUES (@Index,@Color,@Version,@UpdatedBy,now())
            ON CONFLICT (index) DO UPDATE SET
                color=EXCLUDED.color,
                version=EXCLUDED.version,
                updated_by=EXCLUDED.updated_by,
                updated_at=now()
            WHERE pixelbattle_tiles.version < EXCLUDED.version
            """, tile, ct);
    }
}
