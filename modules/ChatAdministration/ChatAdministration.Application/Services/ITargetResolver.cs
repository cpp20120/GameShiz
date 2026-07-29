using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public interface ITargetResolver
{
    Task<ResolvedTarget?> ResolveAsync(ChatId chatId, TargetReference? reference, CancellationToken ct);
}
