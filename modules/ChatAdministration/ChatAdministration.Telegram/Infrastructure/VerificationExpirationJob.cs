using ChatAdministration.Application.Services;
using BotFramework.Sdk.Modules;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class VerificationExpirationJob(
    IChatAdministrationStore store,
    VerificationService verification) : IBackgroundJob
{
    public string Name => "chat_administration.verification_expiration";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var sessions = await store.ListExpiredVerificationsAsync(DateTimeOffset.UtcNow, 50, stoppingToken);
            foreach (var session in sessions)
                await verification.ExpireAsync(session, session.ChallengeMessageId ?? 0, DateTimeOffset.UtcNow, stoppingToken);
            await Task.Delay(sessions.Count == 0 ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }
}
