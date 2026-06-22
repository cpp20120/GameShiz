using System.Text.Json.Serialization;

namespace Games.PixelBattle.Domain.Models;

public sealed record TelegramWebAppAuth(TelegramWebAppUser User, DateTimeOffset AuthDate);
