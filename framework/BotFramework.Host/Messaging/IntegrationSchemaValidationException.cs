namespace BotFramework.Host.Messaging;

public sealed class IntegrationSchemaValidationException(
    string code,
    string message,
    Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
}
