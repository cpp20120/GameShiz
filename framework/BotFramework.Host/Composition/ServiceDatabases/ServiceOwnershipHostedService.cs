using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Composition.ServiceDatabases;

public sealed class ServiceOwnershipHostedService(
    IServiceOwnershipValidator validator,
    IOptions<ServiceOwnershipOptions> options,
    ILogger<ServiceOwnershipHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enforce)
            return;

        var report = await validator.ValidateAsync(cancellationToken);
        if (!report.IsValid)
        {
            var violations = string.Join("; ", report.Violations);
            logger.LogCritical(
                "Service database ownership validation failed for {Database}/{Schema} as {User}: {Violations}",
                report.Database,
                report.Schema,
                report.User,
                violations);
            throw new InvalidOperationException($"Service database ownership validation failed: {violations}");
        }

        logger.LogInformation(
            "Service database ownership validated for {Database}/{Schema} as {User}",
            report.Database,
            report.Schema,
            report.User);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
