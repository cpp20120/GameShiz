using ChatAdministration.Application.Services;
using BotFramework.Sdk.Modules;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class WarningExpirationJob(IChatAdministrationStore store) : IBackgroundJob
{
    public string Name => "chat_administration.warning_expiration";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await store.ExpireWarningsAsync(DateTimeOffset.UtcNow, 100, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
