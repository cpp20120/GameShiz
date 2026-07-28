using System.Security.Cryptography;
using System.Text;
using BotFramework.Host.Execution;
using Games.PixelBattle.Application.Execution;
using Games.PixelBattle.Contracts;

namespace Games.PixelBattle.Application;

public interface IPixelBattleCommandService
{
    Task<PixelUpdateResult> UpdateAsync(
        long userId, int index, string color, string commandId, CancellationToken ct);
}
