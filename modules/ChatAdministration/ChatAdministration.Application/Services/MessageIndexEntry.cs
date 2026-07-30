using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record MessageIndexEntry(
    ChatId ChatId,
    int MessageId,
    UserId AuthorUserId,
    MessageContentType ContentType,
    bool HasLinks,
    DateTimeOffset SentAt,
    string? ContentHash,
    string? AuthorUsername,
    string AuthorDisplayName);
