using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Workflows;
using CoinFlip.Workflow;
using Xunit;

namespace CoinFlip.Workflow.Tests;

public sealed class CoinFlipWorkflowTests
{
    [Fact]
    public async Task Handler_keeps_workflow_metadata_outside_the_domain_result()
    {
        var executor = new RecordingStepExecutor();
        var handler = new CoinFlipWorkflowHandler(executor);
        var command = new CoinFlipWorkflowCommand(
            "coinflip:tenant-a:main:player-1",
            "coinflip-op-1",
            TenantId.Create("tenant-a"),
            ScopeId.Create("main"),
            PlayerId.Create("player-1"),
            Entropy: 0);

        var result = await handler.Handle(command, default);

        Assert.Equal("Heads", result.Side);
        Assert.Equal(command.WorkflowId, executor.Options!.WorkflowId);
        Assert.Equal(command.CommandId, executor.Options.CommandId);
        Assert.Equal("coinflip.flip", executor.Options.Operation);
        Assert.Equal("player-1", executor.Options.AggregateId);
    }

    private sealed class RecordingStepExecutor : IDurableWorkflowStepExecutor
    {
        public DurableWorkflowExecutionOptions? Options { get; private set; }

        public async Task<TResult> ExecuteAsync<TResult>(
            object command,
            DurableWorkflowExecutionOptions options,
            Func<Task<TResult>> execute,
            Func<TResult, bool> succeeded,
            Func<TResult, bool> terminal,
            Func<TResult, string?> aggregateId,
            Func<TResult, object> payload,
            CancellationToken ct)
        {
            Options = options;
            return await execute().ConfigureAwait(false);
        }
    }
}
