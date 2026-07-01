using BotFramework.Host.TelegramOutbox;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class TelegramOutboxReschedulePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("pending")]
    [InlineData("sending")]
    public void Classify_AllowsPendingAndExpiredSendingRows(string status)
    {
        var outcome = TelegramOutboxReschedulePolicy.Classify(status, Now.AddSeconds(-1), Now);

        Assert.Equal(TelegramOutboxRescheduleOutcome.Rescheduled, outcome);
    }

    [Fact]
    public void Classify_RejectsAnActiveDispatcherLease()
    {
        var outcome = TelegramOutboxReschedulePolicy.Classify("sending", Now.AddMinutes(1), Now);

        Assert.Equal(TelegramOutboxRescheduleOutcome.ActivelySending, outcome);
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("cancelled")]
    public void Classify_RejectsTerminalRows(string status)
    {
        var outcome = TelegramOutboxReschedulePolicy.Classify(status, null, Now);

        Assert.Equal(TelegramOutboxRescheduleOutcome.AlreadySent, outcome);
    }
}
