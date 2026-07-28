using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.PixelBattle.Application.Execution;

public sealed record PixelBattleExecutionState(
    PixelBattleTileState? Tile,
    bool KnownUser,
    long NextVersion);
