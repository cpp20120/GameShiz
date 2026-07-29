namespace ChatAdministration.Domain.Models;

public sealed record NormalizedMessage
{
    public required ChatId ChatId { get; init; }
    public required int MessageId { get; init; }
    public required UserId AuthorId { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<MessageEntity> Entities { get; init; } = [];
    public MessageContentType ContentType { get; init; } = MessageContentType.Text;
    public bool IsForwarded { get; init; }
    public bool IsServiceMessage { get; init; }
    public DateTimeOffset SentAt { get; init; }
}
