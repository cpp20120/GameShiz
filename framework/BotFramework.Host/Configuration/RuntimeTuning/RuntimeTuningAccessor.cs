using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace BotFramework.Host.Configuration.RuntimeTuning;

public sealed partial class RuntimeTuningAccessor(
    IConfiguration configuration,
    INpgsqlConnectionFactory connections,
    IServiceProvider services,
    ILogger<RuntimeTuningAccessor> logger)
    : IRuntimeTuningAccessor, IHostedService
{
    private JsonObject? _patchRoot;
    private readonly Lock _reloadGate = new();

    public DailyBonusOptions DailyBonus =>
        RuntimeTuningMerge.MergeSection<DailyBonusOptions>(
            configuration, DailyBonusOptions.SectionName, Navigate(DailyBonusOptions.SectionName));

    public TelegramDiceDailyLimitOptions TelegramDiceDailyLimit =>
        RuntimeTuningMerge.MergeSection<TelegramDiceDailyLimitOptions>(
            configuration, TelegramDiceDailyLimitOptions.SectionName, Navigate(TelegramDiceDailyLimitOptions.SectionName));

    public T GetSection<T>(string sectionPath) where T : class, new() =>
        RuntimeTuningMerge.MergeSection<T>(configuration, sectionPath, Navigate(sectionPath));

    public async Task ReloadFromDatabaseAsync(CancellationToken ct)
    {
        JsonObject? nextPatch = null;
        await using var conn = await connections.OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT payload::text FROM runtime_tuning WHERE id = 1",
            cancellationToken: ct)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(row))
        {
            try
            {
                var validator = services.GetService<RuntimeConfigurationValidator>();
                if (validator is null)
                {
                    nextPatch = JsonNode.Parse(row) is JsonObject parsed
                        ? RuntimeTuningPayloadSanitizer.Sanitize(parsed)
                        : null;
                }
                else
                {
                    var validation = validator.Validate(row);
                    if (validation.IsValid)
                    {
                        nextPatch = JsonNode.Parse(validation.NormalizedPatchJson) as JsonObject;
                    }
                    else
                    {
                        LogRuntimeTuningValidationFailed(
                            logger,
                            string.Join("; ", validation.Issues.Select(static issue => $"{issue.Path}: {issue.Message}")));
                    }
                }
            }
            catch (JsonException ex)
            {
                LogRuntimeTuningPayloadInvalid(logger, ex);
            }
        }

        lock (_reloadGate)
            _patchRoot = nextPatch;
    }

    private JsonNode? Navigate(string sectionPath)
    {
        JsonObject? root;
        lock (_reloadGate)
            root = _patchRoot;

        if (root is null) return null;

        var parts = sectionPath.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        JsonNode? n = root;
        foreach (var p in parts)
        {
            if (n is not JsonObject o) return null;
            if (!o.TryGetPropertyValue(p, out var child)) return null;
            n = child;
        }

        return n;
    }

    public async Task StartAsync(CancellationToken cancellationToken) =>
        await ReloadFromDatabaseAsync(cancellationToken).ConfigureAwait(false);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 1200, Level = LogLevel.Warning,
        Message = "runtime_tuning.payload is invalid JSON; ignoring DB overrides")]
    private static partial void LogRuntimeTuningPayloadInvalid(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Warning,
        Message = "runtime_tuning.payload failed semantic validation; ignoring DB overrides: {Issues}")]
    private static partial void LogRuntimeTuningValidationFailed(ILogger logger, string issues);
}
