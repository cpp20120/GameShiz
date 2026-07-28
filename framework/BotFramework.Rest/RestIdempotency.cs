using System.Security.Cryptography;

namespace BotFramework.Rest;

public static class RestIdempotency
{
    /// <summary>
    /// Legacy game contracts use an integer Telegram message id as part of the
    /// command id. This keeps arbitrary HTTP idempotency keys stable while the
    /// request still goes through the exact same atomic command path.
    /// </summary>
    public static int ToStableSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        var result = BitConverter.ToInt32(hash, 0) & int.MaxValue;
        return result == 0 ? 1 : result;
    }
}