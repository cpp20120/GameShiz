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
        ChallengeOptions? options = null) =>
        new(
            store ?? new InMemoryChallengeStore(),
            economics ?? new FakeEconomicsService(),
            new NullAnalyticsService(),
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
