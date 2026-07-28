using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.PixelBattle.Application.Execution;

public sealed record PixelBattleTileUpdated(
    int Index,
    string Color,
    string Versionstamp,
    long UserId,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pixelbattle.tile_updated";
}
