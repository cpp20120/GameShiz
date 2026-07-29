namespace ChatAdministration.Domain.Models;

public sealed record MessageEntity(
    MessageEntityType Type,
    int Offset,
    int Length,
    string? Url = null);
