using ChatAdministration.Application.Services;
using BotFramework.Sdk.Modules;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class RetentionCleanupJob(IChatAdministrationStore store) : IBackgroundJob
{
    public string Name => "chat_administration.retention_cleanup";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
                foreach (var chat in await store.ListRegisteredChatsAsync(stoppingToken))
            {
                try
                {
                    await store.CleanupRetentionAsync(
                        chat.Id,
                        chat.Settings.RetentionPolicy,
                        DateTimeOffset.UtcNow,
                        5000,
                        stoppingToken);
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // One tenant's cleanup must not block retention for the others.
                }
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
