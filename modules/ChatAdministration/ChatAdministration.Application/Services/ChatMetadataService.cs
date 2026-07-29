using ChatAdministration.Application.Commands;

namespace ChatAdministration.Application.Services;

public sealed class ChatMetadataService(IChatAdministrationStore store)
{
    public Task ObserveAsync(ChatMetadataCommand command, CancellationToken ct) =>
        store.UpsertChatMetadataAsync(command, ct);
}
