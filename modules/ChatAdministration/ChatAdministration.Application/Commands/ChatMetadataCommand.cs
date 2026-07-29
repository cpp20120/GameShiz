using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record ChatMetadataCommand(
    string CommandId,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    ChatType Type,
    string Title,
    DateTimeOffset CreatedAt);
