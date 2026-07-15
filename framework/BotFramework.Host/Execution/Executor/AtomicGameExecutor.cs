using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed class AtomicGameExecutor<TCommand, TState, TResult>(
    IGameExecutionSessionFactory sessions,
    ICommandInbox inbox,
    IAtomicGameAvailability availability,
    IAtomicEconomics economics,
    IAtomicQuotaStore quotaStore,
    IAtomicPlayerProtection playerProtection,
    ITransactionalEventCollector eventCollector,
    GameExecutionDescriptor<TCommand, TState, TResult> descriptor,
    IGameAction<TCommand, TState, TResult> action,
    IGameStateStore<TCommand, TState> stateStore,
    IEnumerable<IGameRecordWriter> recordWriters,
    TimeProvider timeProvider,
    GameExecutionTelemetry telemetry,
    ITransactionalScheduleCollector? scheduleCollector = null,
    IEnumerable<IGameEffectHandler>? effectHandlers = null)
    : IAtomicGameExecutor<TCommand, TState, TResult>
{
    private readonly TransactionalGameEffectPipeline<TCommand, TState, TResult> effectPipeline = new(
        economics,
        quotaStore,
        playerProtection,
        eventCollector,
        stateStore,
        recordWriters,
        scheduleCollector,
        effectHandlers);

    public Type StateType => typeof(TState);

    public async Task<TResult> ExecuteAsync(GameExecutionEnvelope<TCommand> envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var command = envelope.Command;
        var commandId = descriptor.CommandId(command);
        var aggregateId = descriptor.AggregateId(command);
        var chatId = descriptor.ChatId(command);
        var wallet = descriptor.Wallet(command);
        var utcNow = timeProvider.GetUtcNow();
        var quotas = descriptor.Quotas(command, utcNow);
        using var observation = telemetry.Start(descriptor.GameId, commandId, aggregateId);

        IGameExecutionSession? session = null;
        var transactionStartedAt = 0L;
        var committed = false;
        try
        {
            session = await sessions.BeginAsync(ct).ConfigureAwait(false);
            transactionStartedAt = Stopwatch.GetTimestamp();
            observation.LockWaitStarted();
            var lockStartedAt = Stopwatch.GetTimestamp();
            await session.AcquireLocksAsync(BuildLockKeys(command, commandId, aggregateId, wallet, quotas), ct)
                .ConfigureAwait(false);
            observation.Locked(Stopwatch.GetElapsedTime(lockStartedAt));

            var existing = await inbox.GetOrBeginAsync<TResult>(
                commandId,
                descriptor.GameId,
                aggregateId,
                session,
                ct).ConfigureAwait(false);
            if (existing.Status == CommandInboxStatus.Completed)
            {
                observation.Duplicate();
                observation.Committing();
                await session.CommitAsync(ct).ConfigureAwait(false);
                committed = true;
                observation.Committed();
                return existing.Result!;
            }

            var availabilityState = await availability.GetAsync(
                chatId,
                descriptor.GameId,
                session,
                ct).ConfigureAwait(false);
            if (!availabilityState.Enabled)
                throw new GameUnavailableException(descriptor.GameId, chatId, availabilityState.Reason);

            var walletSnapshot = new WalletSnapshot(0);
            if (descriptor.UsesPrimaryWallet)
            {
                await economics.EnsureAsync(wallet, descriptor.DisplayName(command), session, ct).ConfigureAwait(false);
                walletSnapshot = await economics.LoadAsync(wallet, session, ct).ConfigureAwait(false);
            }
            var executionContext = new GameExecutionContext(session, economics, commandId);
            var state = await stateStore.LoadAsync(command, executionContext, ct).ConfigureAwait(false);

            var quotaSnapshots = await LoadQuotaSnapshotsAsync(quotas, session, ct).ConfigureAwait(false);

            var entropy = CreateEntropy(descriptor.EntropyNames);
            await inbox.StoreEntropyAsync(commandId, entropy, session, ct).ConfigureAwait(false);
            var input = new GameActionInput<TState, TCommand>(
                command,
                state,
                walletSnapshot,
                quotaSnapshots,
                entropy,
                utcNow);
            var decision = action.Decide(input);
            observation.Decided(decision.Status, decision.RejectionReason);
            var effectPlan = effectPipeline.Plan(decision, quotas);
            await effectPipeline.ApplyAsync(
                commandId,
                descriptor.GameId,
                aggregateId,
                command,
                state,
                wallet,
                quotas,
                decision,
                effectPlan,
                executionContext,
                session,
                ct).ConfigureAwait(false);
            await inbox.CompleteAsync(commandId, decision.Result, session, ct).ConfigureAwait(false);
            observation.Committing();
            await session.CommitAsync(ct).ConfigureAwait(false);
            committed = true;
            observation.Committed();
            return decision.Result;
        }
        catch (Exception exception)
        {
            if (session is not null && !committed)
            {
                try
                {
                    await session.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    observation.RolledBack();
                }
                catch (Exception rollbackException)
                {
                    exception.Data["RollbackException"] = rollbackException;
                }
            }
            observation.Failed(exception);
            throw;
        }
        finally
        {
            if (transactionStartedAt != 0)
                observation.TransactionFinished(Stopwatch.GetElapsedTime(transactionStartedAt));
            if (session is not null)
                await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<string, QuotaSnapshot>> LoadQuotaSnapshotsAsync(
        IReadOnlyList<QuotaIdentity> quotas,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        var snapshots = new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal);
        foreach (var quota in quotas)
        {
            var snapshot = await quotaStore.LoadAsync(quota, session, ct).ConfigureAwait(false);
            if (!snapshots.TryAdd(quota.QuotaId, snapshot))
                throw new InvalidOperationException($"Duplicate quota id '{quota.QuotaId}'.");
        }
        return snapshots;
    }

    private IEnumerable<string> BuildLockKeys(
        TCommand command,
        string commandId,
        string aggregateId,
        WalletIdentity wallet,
        IReadOnlyList<QuotaIdentity> quotas)
    {
        yield return $"command:{commandId}";
        yield return $"game:{descriptor.GameId}:{aggregateId}";
        if (descriptor.UsesPrimaryWallet)
            yield return wallet.LockKey;
        foreach (var lockKey in descriptor.AdditionalLockKeys(command))
            yield return lockKey;
        foreach (var quota in quotas)
            yield return quota.LockKey;
    }

    private static EntropyValue CreateEntropy(IReadOnlyList<string> names)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        foreach (var name in names)
        {
            RandomNumberGenerator.Fill(bytes);
            var raw = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
            var value = (raw >> 11) * (1.0 / (1UL << 53));
            if (!values.TryAdd(name, value))
                throw new InvalidOperationException($"Duplicate entropy name '{name}'.");
        }
        return new EntropyValue(values);
    }
}
