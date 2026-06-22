using System.Text.Json.Serialization;

namespace Games.PixelBattle;

public sealed record TelegramWebAppAuth(TelegramWebAppUser User, DateTimeOffset AuthDate);
