using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Workflows;
using CoinFlip.Contracts;
using CoinFlip.Domain;

namespace CoinFlip.Workflow;

/// <summary>
/// A workflow command contains only the bounded, replayable input needed by
/// the handler. It does not contain a database connection, service locator or
/// transport object.
/// </summary>
public sealed record CoinFlipWorkflowCommand(
    string WorkflowId,
    string CommandId,
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId PlayerId,
    int Entropy) : IDurableWorkflowCommand;

/// <summary>
/// Framework consumer example. The handler owns the domain decision, while
/// the framework owns durable delivery, retries, step history and replay.
/// A production game would replace the Empty-state example with its local
/// AtomicEffect/transaction boundary.
/// </summary>
public sealed class CoinFlipWorkflowHandler(IDurableWorkflowStepExecutor workflow)
{
    public Task<CoinFlipReply> Handle(CoinFlipWorkflowCommand command, CancellationToken ct) =>
        workflow.ExecuteAsync(
            command,
            new DurableWorkflowExecutionOptions(
                command.WorkflowId,
                command.CommandId,
                "coinflip.flip",
                AggregateId: command.PlayerId.Value),
            () =>
            {
                var result = CoinFlipRules.Flip(CoinFlipGameState.Empty, command.Entropy);
                return Task.FromResult(new CoinFlipReply(
                    result.Side.ToString(),
                    result.State.Flips,
                    result.State.Heads,
                    result.State.Tails));
            },
            static _ => true,
            static _ => true,
            _ => command.PlayerId.Value,
            static reply => new { reply.Side, reply.Flips, reply.Heads, reply.Tails },
            ct);
}

/// <summary>
/// Application/transport boundary example. It can return a typed result
/// immediately or a Pending reply while the durable command continues.
/// </summary>
public sealed class DurableCoinFlipService(IDurableWorkflowDispatcher dispatcher)
{
    public Task<CoinFlipReply> FlipAsync(
        TenantId tenantId,
        ScopeId scopeId,
        PlayerId playerId,
        string operationId,
        int entropy,
        CancellationToken ct)
    {
        var workflowId = $"coinflip:{tenantId.Value}:{scopeId.Value}:{playerId.Value}";
        var command = new CoinFlipWorkflowCommand(
            workflowId,
            operationId,
            tenantId,
            scopeId,
            playerId,
            entropy);

        return dispatcher.DispatchAsync(
            command,
            new DurableWorkflowDispatchOptions(
                workflowId,
                operationId,
                "coinflip.flip",
                AggregateId: playerId.Value),
            () => new CoinFlipReply("Pending", 0, 0, 0, Pending: true, CommandId: operationId),
            ct);
    }
}
