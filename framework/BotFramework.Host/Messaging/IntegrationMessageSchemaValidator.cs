using System.Text.Json;
using BotFramework.Contracts.Messaging;

namespace BotFramework.Host.Messaging;

public sealed class IntegrationMessageSchemaValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public (Type Type, object Message) DeserializeEvent(IntegrationEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateSchemaVersion(envelope.SchemaVersion);
        var type = Resolve(envelope.ContractType, typeof(IIntegrationEvent));
        var message = Deserialize(type, envelope.Payload, envelope.EventType);
        if (message is not IIntegrationEvent integrationEvent
            || !string.Equals(integrationEvent.EventType, envelope.EventType, StringComparison.Ordinal))
            throw new IntegrationSchemaValidationException(
                "message_type_mismatch",
                $"Integration event type '{envelope.EventType}' does not match its payload.");
        return (type, message);
    }

    public (Type Type, object Message) DeserializeCommand(IntegrationCommandEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateSchemaVersion(envelope.SchemaVersion);
        var type = Resolve(envelope.ContractType, typeof(IIntegrationCommand));
        var message = Deserialize(type, envelope.Payload, envelope.CommandType);
        if (message is not IIntegrationCommand integrationCommand
            || !string.Equals(integrationCommand.CommandType, envelope.CommandType, StringComparison.Ordinal))
            throw new IntegrationSchemaValidationException(
                "message_type_mismatch",
                $"Integration command type '{envelope.CommandType}' does not match its payload.");
        return (type, message);
    }

    private static object Deserialize(Type type, string payload, string messageType)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new IntegrationSchemaValidationException(
                "empty_payload",
                $"Integration message '{messageType}' has an empty payload.");

        try
        {
            return JsonSerializer.Deserialize(payload, type, JsonOptions)
                ?? throw new IntegrationSchemaValidationException(
                    "empty_payload",
                    $"Integration message '{messageType}' has an empty payload.");
        }
        catch (IntegrationSchemaValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new IntegrationSchemaValidationException(
                "invalid_json",
                $"Integration message '{messageType}' has invalid JSON.",
                exception);
        }
    }

    private static Type Resolve(string stableName, Type contractType)
    {
        if (string.IsNullOrWhiteSpace(stableName))
            throw new IntegrationSchemaValidationException("missing_contract_type", "Integration contract type is required.");

        var separator = stableName.IndexOf(':');
        if (separator <= 0 || separator == stableName.Length - 1)
            throw new IntegrationSchemaValidationException(
                "invalid_contract_type",
                $"Integration contract type '{stableName}' has an invalid stable name.");

        var assemblyName = stableName[..separator];
        var typeName = stableName[(separator + 1)..];
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.Ordinal));
        var type = assembly?.GetType(typeName, throwOnError: false, ignoreCase: false);
        if (type is null || !contractType.IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
            throw new IntegrationSchemaValidationException(
                "unknown_contract_type",
                $"Integration contract type '{stableName}' is not available or is not a valid {contractType.Name}.");
        return type;
    }

    private static void ValidateSchemaVersion(int schemaVersion)
    {
        if (schemaVersion != 1)
            throw new IntegrationSchemaValidationException(
                "unsupported_schema_version",
                $"Integration schema version '{schemaVersion}' is not supported.");
    }
}
