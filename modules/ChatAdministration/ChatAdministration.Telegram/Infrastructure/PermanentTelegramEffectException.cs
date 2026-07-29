namespace ChatAdministration.Telegram.Infrastructure;

internal sealed class PermanentTelegramEffectException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
