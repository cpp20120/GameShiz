using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.PixelBattle.Application.Execution;

public sealed record PixelBattleTileState(int Index, string Color, long Version, long UpdatedBy);
