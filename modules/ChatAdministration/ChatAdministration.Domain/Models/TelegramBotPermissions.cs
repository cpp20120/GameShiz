namespace ChatAdministration.Domain.Models;

public sealed record TelegramBotPermissions
{
    public bool CanDeleteMessages { get; init; }
    public bool CanRestrictMembers { get; init; }
    public bool CanInviteUsers { get; init; }
    public bool CanPinMessages { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
}
