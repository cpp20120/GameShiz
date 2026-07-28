using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.PixelBattle.Application.Execution;

public sealed record PixelBattleCommand(
    long UserId,
    int Index,
    string Color,
    string CommandId);
