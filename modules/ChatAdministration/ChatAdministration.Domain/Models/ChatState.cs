namespace ChatAdministration.Domain.Models;

public sealed record ChatState
{
    public required ChatId Id { get; init; }
    public required ChatType Type { get; init; }
    public required string Title { get; init; }
    public bool IsEnabled { get; init; } = true;
    public ChatSettings Settings { get; init; } = new();
    public TelegramBotPermissions ObservedBotPermissions { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
