
using BotFramework.Sdk.MiniGames;
using Games.Basketball.Infrastructure.Persistence;
using Games.Bowling.Infrastructure.Persistence;
using Games.Darts.Infrastructure.Persistence;
using Games.DiceCube.Infrastructure.Persistence;
using Games.Football.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CasinoShiz.Host.Infrastructure;

public sealed class MiniGameSessionGhostHeal(
    IServiceProvider services,
    IMiniGameSessionStore sessions) : IMiniGameSessionGhostHeal
{
    private readonly IDiceCubeBetStore? diceCube = services.GetService<IDiceCubeBetStore>();
    private readonly IDartsRoundStore? darts = services.GetService<IDartsRoundStore>();
    private readonly IBasketballBetStore? basketball = services.GetService<IBasketballBetStore>();
    private readonly IBowlingBetStore? bowling = services.GetService<IBowlingBetStore>();
    private readonly IFootballBetStore? football = services.GetService<IFootballBetStore>();

    public async Task<bool> TryClearGhostIfDbEmptyAsync(long userId, long chatId, string blockingGameId, CancellationToken ct)
    {
        switch (blockingGameId)
        {
            case MiniGameIds.DiceCube:
                if (diceCube is null) return false;
                if (await diceCube.FindAsync(userId, chatId, ct) != null) return false;
                BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.DiceCube);
                await sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.DiceCube, ct);
                return true;
            case MiniGameIds.Darts:
                if (darts is null) return false;
                if (await darts.CountActiveByUserChatAsync(userId, chatId, ct) > 0) return false;
                BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Darts);
                await sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Darts, ct);
                return true;
            case MiniGameIds.Basketball:
                if (basketball is null) return false;
                if (await basketball.FindAsync(userId, chatId, ct) != null) return false;
                BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Basketball);
                await sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Basketball, ct);
                return true;
            case MiniGameIds.Bowling:
                if (bowling is null) return false;
                if (await bowling.FindAsync(userId, chatId, ct) != null) return false;
                BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Bowling);
                await sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Bowling, ct);
                return true;
            case MiniGameIds.Football:
                if (football is null) return false;
                if (await football.FindAsync(userId, chatId, ct) != null) return false;
                BotMiniGameSession.ClearCompletedRound(userId, chatId, MiniGameIds.Football);
                await sessions.ClearCompletedRoundAsync(userId, chatId, MiniGameIds.Football, ct);
                return true;
            default:
                return false;
        }
    }
}
