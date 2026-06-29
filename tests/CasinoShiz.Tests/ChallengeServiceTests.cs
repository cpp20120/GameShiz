using Games.Challenges.Application.Services;
using Games.Challenges.Domain.Entities;
using Games.Challenges.Domain.Results;
using Games.Challenges.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class ChallengeServiceTests
{
    private static ChallengeService MakeService(
        InMemoryChallengeStore? store = null,
        FakeEconomicsService? economics = null,
        ChallengeOptions? options = null,
        IAnalyticsService? analytics = null) =>
        new(
            store ?? new InMemoryChallengeStore(),
            economics ?? new FakeEconomicsService(),
            analytics ?? new NullAnalyticsService(),
            Options.Create(options ?? new ChallengeOptions { MinBet = 10, MaxBet = 1_000 }),
            TimeProvider.System);

    [Fact]
    public async Task CreateAsync_SelfChallenge_ReturnsSelfChallengeAndDoesNotInsert()
    {
        var store = new InMemoryChallengeStore();
        var service = MakeService(store);

        var result = await service.CreateAsync(
            challengerId: 1,
            challengerName: "alice",
            target: new ChallengeUser(1, "alice"),
            chatId: 100,
            amount: 10,
            game: ChallengeGame.Dice,
            ct: default);

        Assert.Equal(ChallengeCreateError.SelfChallenge, result.Error);
        Assert.Empty(store.Challenges);
    }

    [Fact]
    public async Task CreateAsync_NotEnoughCoins_ReturnsBalanceAndDoesNotInsert()
    {
        var store = new InMemoryChallengeStore();
        var service = MakeService(store, new FakeEconomicsService { StartingBalance = 9 });

        var result = await service.CreateAsync(
            challengerId: 1,
            challengerName: "alice",
            target: new ChallengeUser(2, "bob"),
            chatId: 100,
            amount: 10,
            game: ChallengeGame.Dice,
            ct: default);

        Assert.Equal(ChallengeCreateError.NotEnoughCoins, result.Error);
        Assert.Equal(9, result.Balance);
        Assert.Empty(store.Challenges);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(1_001)]
    public async Task CreateAsync_OutsideBetLimits_ReturnsInvalidAmount(int amount)
    {
        var store = new InMemoryChallengeStore();

        var result = await MakeService(store).CreateAsync(
            1, "alice", new ChallengeUser(2, "bob"), 100, amount, ChallengeGame.Dice, default);

        Assert.Equal(ChallengeCreateError.InvalidAmount, result.Error);
        Assert.Empty(store.Challenges);
    }

    [Fact]
    public async Task CreateAsync_DuplicatePendingChallenge_IsRejected()
    {
        var store = new InMemoryChallengeStore();
        var existing = NewChallenge();
        store.Challenges[existing.Id] = existing;

        var result = await MakeService(store).CreateAsync(
            1, "alice", new ChallengeUser(2, "bob"), 100, 10, ChallengeGame.Dice, default);

        Assert.Equal(ChallengeCreateError.AlreadyPending, result.Error);
        Assert.Single(store.Challenges);
    }

    [Fact]
    public async Task CreateAsync_ValidChallenge_PersistsPendingChallenge()
    {
        var store = new InMemoryChallengeStore();

        var result = await MakeService(store).CreateAsync(
            1, "alice", new ChallengeUser(2, "bob"), 100, 25, ChallengeGame.Darts, default);

        Assert.Equal(ChallengeCreateError.None, result.Error);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Pending, result.Challenge.Status);
        Assert.Contains(result.Challenge.Id, store.Challenges.Keys);
    }

    [Theory]
    [InlineData(999, ChallengeAcceptError.NotTarget)]
    [InlineData(2, ChallengeAcceptError.AlreadyResolved)]
    public async Task BeginAcceptAsync_RejectsWrongActorOrResolvedChallenge(
        long actorId, ChallengeAcceptError expected)
    {
        var store = new InMemoryChallengeStore();
        var challenge = NewChallenge(status: actorId == 2 ? ChallengeStatus.Declined : ChallengeStatus.Pending);
        store.Challenges[challenge.Id] = challenge;

        var result = await MakeService(store).BeginAcceptAsync(challenge.Id, actorId, default);

        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task DeclineAsync_TargetDeclinesPendingChallenge()
    {
        var store = new InMemoryChallengeStore();
        var challenge = NewChallenge();
        store.Challenges[challenge.Id] = challenge;

        var result = await MakeService(store).DeclineAsync(challenge.Id, challenge.TargetId, default);

        Assert.Equal(ChallengeAcceptError.None, result);
        Assert.Equal(ChallengeStatus.Declined, store.Challenges[challenge.Id].Status);
    }

    [Fact]
    public async Task DeclineAsync_MissingChallenge_ReturnsNotFound()
    {
        var result = await MakeService().DeclineAsync(Guid.NewGuid(), 2, default);

        Assert.Equal(ChallengeAcceptError.NotFound, result);
    }

    [Fact]
    public async Task DeclineAsync_NonTarget_ReturnsNotTargetWithoutMutation()
    {
        var store = new InMemoryChallengeStore();
        var challenge = NewChallenge();
        store.Challenges[challenge.Id] = challenge;

        var result = await MakeService(store).DeclineAsync(challenge.Id, 999, default);

        Assert.Equal(ChallengeAcceptError.NotTarget, result);
        Assert.Equal(ChallengeStatus.Pending, store.Challenges[challenge.Id].Status);
    }

    [Fact]
    public async Task DeclineAsync_ResolvedChallenge_ReturnsAlreadyResolved()
    {
        var store = new InMemoryChallengeStore();
        var challenge = NewChallenge(status: ChallengeStatus.Completed);
        store.Challenges[challenge.Id] = challenge;

        var result = await MakeService(store).DeclineAsync(challenge.Id, challenge.TargetId, default);

        Assert.Equal(ChallengeAcceptError.AlreadyResolved, result);
    }

    [Fact]
    public async Task FailAcceptedAsync_RefundsBothPlayersAndMarksFailed()
    {
        var store = new InMemoryChallengeStore();
        var economics = new FakeEconomicsService { StartingBalance = 0 };
        var challenge = NewChallenge(amount: 25, status: ChallengeStatus.Accepted);
        store.Challenges[challenge.Id] = challenge;

        await MakeService(store, economics).FailAcceptedAsync(challenge, default);

        Assert.Equal(ChallengeStatus.Failed, store.Challenges[challenge.Id].Status);
        Assert.Equal(2, economics.Credits.Count);
        Assert.All(economics.Credits, credit =>
        {
            Assert.Equal(25, credit.Amount);
            Assert.Equal("challenge.refund", credit.Reason);
        });
    }

    [Fact]
    public async Task BeginAcceptAsync_TargetWithoutCoins_RefundsChallengerAndMarksFailed()
    {
        var store = new InMemoryChallengeStore();
        var economics = new FakeEconomicsService { StartingBalance = 100 };
        var challenge = NewChallenge(amount: 25);
        store.Challenges[challenge.Id] = challenge;
        economics.SetBalance(challenge.TargetId, challenge.ChatId, 0);

        var result = await MakeService(store, economics).BeginAcceptAsync(challenge.Id, challenge.TargetId, default);

        Assert.Equal(ChallengeAcceptError.TargetNotEnoughCoins, result.Error);
        Assert.Equal(ChallengeStatus.Failed, store.Challenges[challenge.Id].Status);
        Assert.Contains(economics.Debits, d => d.UserId == challenge.ChallengerId && d.Amount == 25 && string.Equals(d.Reason, "challenge.stake", StringComparison.Ordinal));
        Assert.Contains(economics.Credits, c => c.UserId == challenge.ChallengerId && c.Amount == 25 && string.Equals(c.Reason, "challenge.refund", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompleteAcceptedAsync_WinnerReceivesPotMinusFee()
    {
        var store = new InMemoryChallengeStore();
        var economics = new FakeEconomicsService { StartingBalance = 0 };
        var challenge = NewChallenge(amount: 100, status: ChallengeStatus.Accepted);
        store.Challenges[challenge.Id] = challenge;
        var service = MakeService(store, economics, new ChallengeOptions { MinBet = 1, MaxBet = 1_000, HouseFeeBasisPoints = 250 });

        var result = await service.CompleteAcceptedAsync(challenge, challengerRoll: 6, targetRoll: 3, default);

        Assert.Equal(ChallengeAcceptError.None, result.Error);
        Assert.Equal(challenge.ChallengerId, result.WinnerId);
        Assert.Equal(195, result.Payout);
        Assert.Equal(5, result.Fee);
        Assert.Equal(ChallengeStatus.Completed, store.Challenges[challenge.Id].Status);
        Assert.Contains(economics.Credits, c => c.UserId == challenge.ChallengerId && c.Amount == 195 && string.Equals(c.Reason, "challenge.payout", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompleteAcceptedAsync_TieRefundsBothStakes()
    {
        var store = new InMemoryChallengeStore();
        var economics = new FakeEconomicsService { StartingBalance = 0 };
        var challenge = NewChallenge(amount: 40, status: ChallengeStatus.Accepted);
        store.Challenges[challenge.Id] = challenge;

        var result = await MakeService(store, economics).CompleteAcceptedAsync(challenge, challengerRoll: 4, targetRoll: 4, default);

        Assert.True(result.IsTie);
        Assert.Equal(ChallengeStatus.Completed, store.Challenges[challenge.Id].Status);
        Assert.Contains(economics.Credits, c => c.UserId == challenge.ChallengerId && c.Amount == 40 && string.Equals(c.Reason, "challenge.tie_refund", StringComparison.Ordinal));
        Assert.Contains(economics.Credits, c => c.UserId == challenge.TargetId && c.Amount == 40 && string.Equals(c.Reason, "challenge.tie_refund", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BeginAcceptAsync_MissingChallenge_ReturnsNotFound()
    {
        var result = await MakeService().BeginAcceptAsync(Guid.NewGuid(), 2, default);
        Assert.Equal(ChallengeAcceptError.NotFound, result.Error);
    }

    [Fact]
    public async Task BeginAcceptAsync_ExpiredChallenge_MarksFailed()
    {
        var store = new InMemoryChallengeStore();
        var challenge = NewChallenge() with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        store.Challenges[challenge.Id] = challenge;

        var result = await MakeService(store).BeginAcceptAsync(challenge.Id, challenge.TargetId, default);

        Assert.Equal(ChallengeAcceptError.Expired, result.Error);
        Assert.Equal(ChallengeStatus.Failed, store.Challenges[challenge.Id].Status);
    }

    [Fact]
    public async Task BeginAcceptAsync_ChallengerWithoutCoins_MarksFailed()
    {
        var store = new InMemoryChallengeStore();
        var economics = new FakeEconomicsService { StartingBalance = 100 };
        var challenge = NewChallenge(amount: 25);
        store.Challenges[challenge.Id] = challenge;
        economics.SetBalance(challenge.ChallengerId, challenge.ChatId, 0);

        var result = await MakeService(store, economics).BeginAcceptAsync(challenge.Id, challenge.TargetId, default);

        Assert.Equal(ChallengeAcceptError.ChallengerNotEnoughCoins, result.Error);
        Assert.Equal(ChallengeStatus.Failed, store.Challenges[challenge.Id].Status);
        Assert.Empty(economics.Credits);
    }

    [Fact]
    public async Task BeginAcceptAsync_SuccessDebitsBothAndTracksAcceptance()
    {
        var store = new InMemoryChallengeStore();
        var economics = new FakeEconomicsService();
        var analytics = new RecordingAnalyticsService();
        var challenge = NewChallenge(amount: 25);
        store.Challenges[challenge.Id] = challenge;

        var result = await MakeService(store, economics, analytics: analytics)
            .BeginAcceptAsync(challenge.Id, challenge.TargetId, default);

        Assert.Equal(ChallengeAcceptError.None, result.Error);
        Assert.Equal(ChallengeStatus.Accepted, store.Challenges[challenge.Id].Status);
        Assert.Equal(2, economics.Debits.Count);
        Assert.All(economics.Debits, x => Assert.Equal(25, x.Amount));
        Assert.Contains(analytics.Events, x => x.EventName == "accepted");
    }

    [Fact]
    public async Task CompleteAcceptedAsync_TargetWinsAndFeeIsClampedToPot()
    {
        var store = new InMemoryChallengeStore();
        var economics = new FakeEconomicsService { StartingBalance = 0 };
        var analytics = new RecordingAnalyticsService();
        var challenge = NewChallenge(amount: 50, status: ChallengeStatus.Accepted);
        store.Challenges[challenge.Id] = challenge;
        var options = new ChallengeOptions { MinBet = 1, MaxBet = 1_000, HouseFeeBasisPoints = 20_000 };

        var result = await MakeService(store, economics, options, analytics)
            .CompleteAcceptedAsync(challenge, challengerRoll: 1, targetRoll: 6, default);

        Assert.Equal(challenge.TargetId, result.WinnerId);
        Assert.Equal(challenge.TargetName, result.WinnerName);
        Assert.Equal(100, result.Fee);
        Assert.Equal(0, result.Payout);
        Assert.Contains(analytics.Events, x => x.EventName == "completed");
    }

    [Fact]
    public async Task CreateDeclineTieAndFailure_TrackLifecycleAnalytics()
    {
        var store = new InMemoryChallengeStore();
        var analytics = new RecordingAnalyticsService();
        var service = MakeService(store, analytics: analytics);
        var created = await service.CreateAsync(
            1, "alice", new ChallengeUser(2, "bob"), 100, 20, ChallengeGame.Darts, default);
        Assert.NotNull(created.Challenge);
        await service.DeclineAsync(created.Challenge.Id, 2, default);

        var tie = NewChallenge(status: ChallengeStatus.Accepted);
        store.Challenges[tie.Id] = tie;
        await service.CompleteAcceptedAsync(tie, 3, 3, default);
        var failed = NewChallenge(status: ChallengeStatus.Accepted);
        store.Challenges[failed.Id] = failed;
        await service.FailAcceptedAsync(failed, default);

        Assert.Contains(analytics.Events, x => x.EventName == "created");
        Assert.Contains(analytics.Events, x => x.EventName == "declined");
        Assert.Contains(analytics.Events, x => x.EventName == "tie");
        Assert.Contains(analytics.Events, x => x.EventName == "failed_refunded");
    }

    private static Challenge NewChallenge(int amount = 10, ChallengeStatus status = ChallengeStatus.Pending) =>
        new(
            Guid.NewGuid(),
            ChatId: 100,
            ChallengerId: 1,
            ChallengerName: "alice",
            TargetId: 2,
            TargetName: "bob",
            Amount: amount,
            Game: ChallengeGame.Dice,
            Status: status,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5));

    private sealed class InMemoryChallengeStore : IChallengeStore
    {
        public Dictionary<Guid, Challenge> Challenges { get; } = new();

        public Task<ChallengeUser?> FindKnownUserByUsernameAsync(long chatId, string username, CancellationToken ct) =>
            Task.FromResult<ChallengeUser?>(null);

        public Task<bool> HasPendingAsync(long challengerId, long targetId, long chatId, CancellationToken ct) =>
            Task.FromResult(Challenges.Values.Any(c =>
                c.ChatId == chatId &&
                c.ChallengerId == challengerId &&
                c.TargetId == targetId &&
                c.Status == ChallengeStatus.Pending));

        public Task InsertAsync(Challenge challenge, CancellationToken ct)
        {
            Challenges[challenge.Id] = challenge;
            return Task.CompletedTask;
        }

        public Task<Challenge?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Challenges.GetValueOrDefault(id));

        public Task<bool> TryMarkStatusAsync(Guid id, ChallengeStatus expected, ChallengeStatus next, CancellationToken ct)
        {
            if (!Challenges.TryGetValue(id, out var challenge) || challenge.Status != expected)
                return Task.FromResult(false);

            Challenges[id] = challenge with { Status = next };
            return Task.FromResult(true);
        }
    }
}
