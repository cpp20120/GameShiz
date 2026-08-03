using System.Text.Json;
using BotFramework.Contracts.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace BotFramework.Host.Workflows;

public sealed class DurableWorkflowTimeoutDispatcher(
    IDurableWorkflowTimeoutStore timeouts,
    IServiceScopeFactory scopes,
    ILogger<DurableWorkflowTimeoutDispatcher> logger) : BackgroundService
{
    private readonly string leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await timeouts.ClaimDueAsync(50, TimeSpan.FromMinutes(1), leaseOwner, stoppingToken);
                if (claimed.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                    continue;
                }

                foreach (var timeout in claimed)
                    await DispatchAsync(timeout, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Durable workflow timeout poll failed.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task DispatchAsync(DurableWorkflowTimeout timeout, CancellationToken ct)
    {
        try
        {
            var command = DurableWorkflowCommandDeserializer.Deserialize(timeout.CommandType, timeout.CommandJson);
            await using var scope = scopes.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.SendAsync(command, new DeliveryOptions
            {
                CorrelationId = timeout.WorkflowId,
                CausationId = timeout.CausationId ?? timeout.CommandId,
                GroupId = timeout.GroupId ?? timeout.WorkflowId,
            });
            await timeouts.MarkDispatchedAsync(timeout.TimeoutId, leaseOwner, ct);
            BotFrameworkMetrics.WorkflowTimeoutsDispatched.Add(1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            BotFrameworkMetrics.WorkflowTimeoutRetries.Add(1);
            logger.LogError(
                exception,
                "Durable workflow timeout {TimeoutId} dispatch failed on attempt {Attempt}.",
                timeout.TimeoutId,
                timeout.Attempts);
            await timeouts.MarkFailedAsync(timeout.TimeoutId, leaseOwner, exception.Message, CancellationToken.None);
        }
    }
}
