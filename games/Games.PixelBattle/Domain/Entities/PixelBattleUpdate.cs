using System.Text.Json.Serialization;

namespace Games.PixelBattle;

public sealed record PixelBattleUpdate(int Index, string Color, string Versionstamp);
