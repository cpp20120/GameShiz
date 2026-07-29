using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed class TelegramTargetResolver(IChatAdministrationStore store) : ITargetResolver
{
    public async Task<ResolvedTarget?> ResolveAsync(ChatId chatId, TargetReference? reference, CancellationToken ct)
    {
        if (reference is null)
            return null;

        if (reference.UserId is { } userId)
            return new ResolvedTarget(
                userId,
                reference.Username,
                string.IsNullOrWhiteSpace(reference.DisplayName) ? $"User {userId}" : reference.DisplayName);

        if (string.IsNullOrWhiteSpace(reference.Username))
            return null;

        return await store.FindMemberByUsernameAsync(chatId, reference.Username.TrimStart('@'), ct);
    }
}
