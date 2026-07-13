using System.Text.Json.Serialization;

namespace Games.PixelBattle.Domain.Entities;

public sealed record TelegramWebAppUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username);
