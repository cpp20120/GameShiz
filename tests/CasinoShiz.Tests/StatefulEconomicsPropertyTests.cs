using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Economics;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class StatefulEconomicsPropertyTests
{
    [Property(MaxTest = 150)]
    public Property WalletBatch_CommandSequence_PreservesLedgerAndBalanceInvariants(NonEmptyArray<int> commands)
    {
        var state = new WalletMutationState(1_000, 0);
        var ledger = new List<WalletMutationLine>();
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Math.Abs((long)rawCommand);
            var previousState = state;
            IReadOnlyList<WalletBatchEffect> effects = (magnitude % 4) switch
            {
                0 => [new WalletBatchEffect(WalletBatchEffectKind.Debit, 1 + (int)(magnitude % 250), "game.bet")],
                1 => [new WalletBatchEffect(WalletBatchEffectKind.Credit, 1 + (int)(magnitude % 250), "game.payout")],
                2 =>
                [
                    new WalletBatchEffect(WalletBatchEffectKind.Debit, 1 + (int)(magnitude % 125), "game.bet"),
                    new WalletBatchEffect(WalletBatchEffectKind.Credit, 1 + (int)((magnitude / 4) % 125), "game.payout"),
                ],
                _ =>
                [
                    new WalletBatchEffect(WalletBatchEffectKind.Credit, 1 + (int)(magnitude % 125), "game.payout"),
                    new WalletBatchEffect(WalletBatchEffectKind.Debit, 1 + (int)((magnitude / 4) % 125), "game.bet"),
                ],
            };

            var decision = WalletMutationPolicy.ApplyBatch(state, effects, allowNegative: false);
            if (decision.Applied)
            {
                state = new WalletMutationState(decision.NewBalance, decision.NewVersion);
                ledger.AddRange(decision.Ledger);
            }
            else if (state != previousState || decision.Ledger.Count != 0)
            {
                failure = "wallet rejection changed state or emitted ledger lines";
                break;
            }

            failure = CheckLedgerInvariants(state, ledger);
            if (failure is not null)
                break;
        }

        return ((failure ?? CheckLedgerInvariants(state, ledger)) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, balance={state.Balance}, version={state.Version}");
    }

    [Property(MaxTest = 150)]
    public Property WalletTransfer_CommandSequence_PreservesConservationAndNoOverdraft(NonEmptyArray<int> commands)
    {
        var sender = 1_000;
        var recipient = 250;
        var initialTotal = sender + recipient;
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Math.Abs((long)rawCommand);
            var previousSender = sender;
            var previousRecipient = recipient;
            var debit = 1 + (int)(magnitude % 100);
            var credit = 1 + (int)((magnitude / 7) % debit);
            var decision = WalletTransferPolicy.Apply(sender, recipient, debit, credit);
            if (decision.Applied)
            {
                sender = decision.SenderNewBalance;
                recipient = decision.RecipientNewBalance;
            }
            else if (sender != previousSender || recipient != previousRecipient)
            {
                failure = "rejected transfer changed a wallet";
                break;
            }

            if (sender < 0 || recipient < 0)
            {
                failure = "transfer created a negative wallet";
                break;
            }
            if (sender + recipient > initialTotal)
            {
                failure = "transfer created coins without a fee source";
                break;
            }
        }

        return (failure is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, sender={sender}, recipient={recipient}");
    }

    [Fact]
    public void WalletBatch_RejectsWholeBatchWhenLaterDebitOverdrafts()
    {
        var decision = WalletMutationPolicy.ApplyBatch(
            new WalletMutationState(10, 4),
            [
                new WalletBatchEffect(WalletBatchEffectKind.Credit, 5, "payout"),
                new WalletBatchEffect(WalletBatchEffectKind.Debit, 16, "bet"),
            ],
            allowNegative: false);

        Assert.False(decision.Applied);
        Assert.True(decision.Rejected);
        Assert.Equal(10, decision.NewBalance);
        Assert.Equal(4, decision.NewVersion);
        Assert.Empty(decision.Ledger);
    }

    private static string? CheckLedgerInvariants(WalletMutationState state, IReadOnlyList<WalletMutationLine> ledger)
    {
        var balance = 1_000;
        for (var index = 0; index < ledger.Count; index++)
        {
            var line = ledger[index];
            var expected = checked(balance + line.Delta);
            if (line.BalanceAfter != expected || line.BalanceAfter < 0)
                return "ledger balance-after chain is invalid";
            if (string.IsNullOrWhiteSpace(line.Reason))
                return "ledger line has no reason";
            balance = line.BalanceAfter;
        }

        if (state.Balance != balance)
            return $"wallet balance diverged from ledger: state={state.Balance}, ledger={balance}";
        if (state.Version != ledger.Count)
            return $"wallet version diverged from ledger count: version={state.Version}, lines={ledger.Count}";
        return null;
    }
}
