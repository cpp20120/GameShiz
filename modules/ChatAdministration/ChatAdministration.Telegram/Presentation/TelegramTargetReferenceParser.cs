using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramEntityType = Telegram.Bot.Types.Enums.MessageEntityType;

namespace ChatAdministration.Telegram.Presentation;

public static class TelegramTargetReferenceParser
{
    public static TargetReference? FromMessage(Message message, bool allowActor = false)
    {
        if (message.ReplyToMessage?.From is { IsBot: false } reply)
            return TargetReference.ForUser(
                new UserId(reply.Id),
                reply.Username,
                DisplayName(reply));

        var textMention = (message.Entities ?? message.CaptionEntities ?? [])
            .FirstOrDefault(entity => entity.Type == TelegramEntityType.TextMention && entity.User is not null);
        if (textMention?.User is { IsBot: false } mentioned)
            return TargetReference.ForUser(
                new UserId(mentioned.Id),
                mentioned.Username,
                DisplayName(mentioned));

        var tokens = (message.Text ?? message.Caption ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .ToArray();
        var token = tokens.FirstOrDefault(value => value.StartsWith('@') || long.TryParse(value, out _));
        if (token?.StartsWith('@') == true && token.Length > 1)
            return new TargetReference(null, token[1..], null);
        if (token is not null && long.TryParse(token, out var userId))
            return TargetReference.ForUser(new UserId(userId));

        return allowActor && message.From is not null
            ? TargetReference.ForUser(new UserId(message.From.Id), message.From.Username, DisplayName(message.From))
            : null;
    }

    private static string DisplayName(User user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : user.Username ?? $"User {user.Id}";
}
