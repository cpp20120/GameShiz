namespace BotFramework.Host.Messaging;

public interface IIntegrationMessageQuarantineStore
{
    Task QuarantineAsync(IntegrationQuarantineMessage message, CancellationToken ct);
}

public sealed record IntegrationQuarantineMessage(
    string ConsumerName,
    string? TenantId,
    string? ScopeId,
    string MessageId,
    string? Topic,
    string? MessageType,
    string? ContractType,
    int? SchemaVersion,
    string? Payload,
    string ErrorCode,
    string ErrorMessage);
