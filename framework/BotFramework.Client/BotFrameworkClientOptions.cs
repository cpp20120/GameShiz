using System.Text.Json;

namespace BotFramework.Client;

public sealed record BotFrameworkClientOptions(
    Uri BaseAddress,
    Func<CancellationToken, ValueTask<string?>>? AccessTokenProvider = null,
    JsonSerializerOptions? JsonSerializerOptions = null);
