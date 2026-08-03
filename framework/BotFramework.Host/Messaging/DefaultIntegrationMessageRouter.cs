using System.Reflection;
using BotFramework.Contracts.Messaging;

namespace BotFramework.Host.Messaging;

public sealed class DefaultIntegrationMessageRouter : IIntegrationMessageRouter
{
    private static readonly string[] AggregatePropertyNames =
    [
        "OperationId", "ReservationId", "SettlementId", "BetId", "RoundId",
        "GameId", "AccountId", "ChatId", "UserId", "PlayerId", "AggregateId", "Id"
    ];

    public IntegrationMessageRoute Route(
        IntegrationMessageKind kind,
        string messageType,
        object message,
        string? tenantId,
        string? scopeId,
        string? playerId)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(messageType))
            throw new ArgumentException("Message type is required.", nameof(messageType));

        if (message is IIntegrationMessageRouted routed)
        {
            var routedTopic = string.IsNullOrWhiteSpace(routed.Topic)
                ? DefaultTopic(kind)
                : routed.Topic!;
            var routedKey = string.IsNullOrWhiteSpace(routed.MessageKey)
                ? DefaultKey(message, messageType, tenantId, scopeId, playerId)
                : routed.MessageKey!;
            return new IntegrationMessageRoute(routedTopic, routedKey);
        }

        return new(
            DefaultTopic(kind),
            DefaultKey(message, messageType, tenantId, scopeId, playerId));
    }

    private static string DefaultTopic(IntegrationMessageKind kind) =>
        kind == IntegrationMessageKind.Command
            ? IntegrationMessagingTopics.Commands
            : IntegrationMessagingTopics.Events;

    private static string DefaultKey(
        object message,
        string messageType,
        string? tenantId,
        string? scopeId,
        string? playerId)
    {
        var aggregate = AggregatePropertyNames
            .Select(name => message.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
            .Where(static property => property is not null)
            .Select(property => property!.GetValue(message)?.ToString())
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

        return string.Join(
            ":",
            [
                string.IsNullOrWhiteSpace(tenantId) ? "global" : tenantId,
                string.IsNullOrWhiteSpace(scopeId) ? "global" : scopeId,
                string.IsNullOrWhiteSpace(aggregate) ? messageType : aggregate,
                string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId,
            ]);
    }
}
