using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Configuration;
using Games.SecretHitler.Domain.Entities;
using Games.SecretHitler.Domain.Events;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class SecretHitlerServiceTests
{
    [Fact]
    public async Task FindMyGameAsync_ReturnsEmptyForMissingOrStalePlayer()
    {
        var players = new MemoryPlayers();
        var service = MakeService(players: players);

        Assert.Null((await service.FindMyGameAsync(1, default)).Snapshot);

        players.Items.Add(Player("MISSING", 1, 0));
        Assert.Null((await service.FindMyGameAsync(1, default)).Snapshot);
    }

    [Fact]
    public async Task FindMyGameAsync_ReturnsSnapshotAndCurrentPlayer()
    {
        var games = new MemoryGames();
        var players = new MemoryPlayers();
        games.Items["ABCD"] = Game("ABCD", 1);
        players.Items.Add(Player("ABCD", 1, 0));
        players.Items.Add(Player("ABCD", 2, 1));

        var result = await MakeService(games, players).FindMyGameAsync(2, default);

        Assert.Equal("ABCD", result.Snapshot?.Game.InviteCode);
        Assert.Equal(2, result.Snapshot?.Players.Count);
        Assert.Equal(2, result.Me?.UserId);
    }

    [Fact]
    public async Task CreateGameAsync_RejectsBalanceMembershipAndExistingChatGame()
    {
        var poor = new FakeEconomicsService { StartingBalance = 49 };
        Assert.Equal(ShError.NotEnoughCoins,
            (await MakeService(economics: poor).CreateGameAsync(1, "a", -1, 1, default)).Error);

        var players = new MemoryPlayers();
        players.Items.Add(Player("OLD", 1, 0));
        Assert.Equal(ShError.AlreadyInGame,
            (await MakeService(players: players).CreateGameAsync(1, "a", -1, 1, default)).Error);

        var games = new MemoryGames();
        games.Items["OPEN"] = Game("OPEN", 2, chatId: -1);
        Assert.Equal(ShError.GameInProgress,
            (await MakeService(games: games).CreateGameAsync(1, "a", -1, 1, default)).Error);
    }

    [Fact]
    public async Task CreateGameAsync_DebitsPersistsTracksAndPublishes()
    {
        var games = new MemoryGames();
        var players = new MemoryPlayers();
        var economics = new FakeEconomicsService();
        var analytics = new RecordingAnalyticsService();
        var events = new NullEventBus();

        var result = await MakeService(games, players, economics, analytics, events)
            .CreateGameAsync(1, "alice", -100, 1, default);

        Assert.Equal(ShError.None, result.Error);
        Assert.Equal(50, result.BuyIn);
        Assert.Single(games.Items);
        Assert.Single(players.Items);
        Assert.Single(economics.Debits);
        Assert.Contains(analytics.Events, x => x.EventName == "create");
        Assert.Single(events.Published.OfType<SecretHitlerGameCreated>());
    }

    [Fact]
    public async Task JoinGameAsync_RejectsMissingAndNonLobbyGames()
    {
        Assert.Equal(ShError.GameNotFound,
            (await MakeService().JoinGameAsync(2, "bob", 2, "missing", default)).Error);

        var games = new MemoryGames();
        games.Items["ABCD"] = Game("ABCD", 1, status: ShStatus.Active);
        Assert.Equal(ShError.GameInProgress,
            (await MakeService(games: games).JoinGameAsync(2, "bob", 2, "abcd", default)).Error);
    }

    [Fact]
    public async Task JoinGameAsync_UsesFirstFreePositionAndUpdatesPot()
    {
        var games = new MemoryGames();
        var players = new MemoryPlayers();
        var events = new NullEventBus();
        games.Items["ABCD"] = Game("ABCD", 1);
        players.Items.Add(Player("ABCD", 1, 0));
        players.Items.Add(Player("ABCD", 3, 2));

        var result = await MakeService(games, players, events: events)
            .JoinGameAsync(2, "bob", 2, "abcd", default);

        Assert.Equal(ShError.None, result.Error);
        Assert.Contains(players.Items, x => x.UserId == 2 && x.Position == 1);
        Assert.Equal(100, games.Items["ABCD"].Pot);
        Assert.Single(events.Published.OfType<SecretHitlerPlayerJoined>());
    }

    [Fact]
    public async Task StartGameAsync_ValidatesMembershipHostAndPlayerCount()
    {
        Assert.Equal(ShError.NotInGame, (await MakeService().StartGameAsync(1, default)).Error);

        var games = new MemoryGames();
        var players = new MemoryPlayers();
        games.Items["ABCD"] = Game("ABCD", 1);
        players.Items.Add(Player("ABCD", 1, 0));
        players.Items.Add(Player("ABCD", 2, 1));

        Assert.Equal(ShError.NotHost,
            (await MakeService(games, players).StartGameAsync(2, default)).Error);
        Assert.Equal(ShError.NotEnoughPlayers,
            (await MakeService(games, players).StartGameAsync(1, default)).Error);
    }

    [Fact]
    public async Task StartGameAsync_AssignsRolesPersistsAndPublishes()
    {
        var games = new MemoryGames();
        var players = new MemoryPlayers();
        var events = new NullEventBus();
        games.Items["ABCD"] = Game("ABCD", 1);
        for (var i = 0; i < 5; i++) players.Items.Add(Player("ABCD", i + 1, i));

        var result = await MakeService(games, players, events: events).StartGameAsync(1, default);

        Assert.Equal(ShError.None, result.Error);
        Assert.Equal(ShStatus.Active, result.Snapshot?.Game.Status);
        Assert.Equal(ShPhase.Nomination, result.Snapshot?.Game.Phase);
        Assert.Contains(players.Items, x => x.Role == ShRole.Hitler);
        Assert.Single(events.Published.OfType<SecretHitlerGameStarted>());
    }

    private static SecretHitlerService MakeService(
        MemoryGames? games = null,
        MemoryPlayers? players = null,
        FakeEconomicsService? economics = null,
        RecordingAnalyticsService? analytics = null,
        NullEventBus? events = null) =>
        new(games ?? new MemoryGames(), players ?? new MemoryPlayers(),
            economics ?? new FakeEconomicsService(), new ImmediateGameLock(),
            analytics ?? new RecordingAnalyticsService(), events ?? new NullEventBus(),
            Options.Create(new SecretHitlerOptions()), NullLogger<SecretHitlerService>.Instance);

    private static SecretHitlerGame Game(
        string code, long host, long chatId = -100, ShStatus status = ShStatus.Lobby) => new()
    {
        InviteCode = code,
        HostUserId = host,
        ChatId = chatId,
        Status = status,
        BuyIn = 50,
        Pot = 50,
        CreatedAt = 1,
        LastActionAt = 1,
    };

    private static SecretHitlerPlayer Player(string code, long userId, int position) => new()
    {
        InviteCode = code,
        UserId = userId,
        Position = position,
        DisplayName = $"u{userId}",
        ChatId = userId,
        JoinedAt = 1,
    };

    private sealed class ImmediateGameLock : IDistributedGameLock
    {
        public Task<IAsyncDisposable> AcquireAsync(string resource, CancellationToken ct) =>
            Task.FromResult<IAsyncDisposable>(new Releaser());

        private sealed class Releaser : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class MemoryGames : ISecretHitlerGameStore
    {
        public Dictionary<string, SecretHitlerGame> Items { get; } = new(StringComparer.Ordinal);
        public Task<SecretHitlerGame?> FindAsync(string inviteCode, CancellationToken ct) =>
            Task.FromResult(Items.GetValueOrDefault(inviteCode));
        public Task<SecretHitlerGame?> FindOpenByChatAsync(long chatId, CancellationToken ct) =>
            Task.FromResult(Items.Values.FirstOrDefault(x => x.ChatId == chatId && x.Status is ShStatus.Lobby or ShStatus.Active));
        public Task<bool> CodeExistsAsync(string inviteCode, CancellationToken ct) =>
            Task.FromResult(Items.ContainsKey(inviteCode));
        public Task InsertAsync(SecretHitlerGame game, CancellationToken ct)
        {
            Items.Add(game.InviteCode, game);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(SecretHitlerGame game, CancellationToken ct)
        {
            Items[game.InviteCode] = game;
            return Task.CompletedTask;
        }
        public Task UpsertStateMessageAsync(string inviteCode, int messageId, CancellationToken ct)
        {
            Items[inviteCode].StateMessageId = messageId;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryPlayers : ISecretHitlerPlayerStore
    {
        public List<SecretHitlerPlayer> Items { get; } = [];
        public Task<SecretHitlerPlayer?> FindByUserAsync(long userId, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(x => x.UserId == userId));
        public Task<List<SecretHitlerPlayer>> ListByGameAsync(string inviteCode, CancellationToken ct) =>
            Task.FromResult(Items.Where(x => x.InviteCode == inviteCode).OrderBy(x => x.Position).ToList());
        public Task<bool> AnyForUserAsync(long userId, CancellationToken ct) =>
            Task.FromResult(Items.Any(x => x.UserId == userId));
        public Task<int> CountByGameAsync(string inviteCode, CancellationToken ct) =>
            Task.FromResult(Items.Count(x => x.InviteCode == inviteCode));
        public Task InsertAsync(SecretHitlerPlayer player, CancellationToken ct)
        {
            Items.Add(player);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(SecretHitlerPlayer player, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(string inviteCode, int position, CancellationToken ct)
        {
            Items.RemoveAll(x => x.InviteCode == inviteCode && x.Position == position);
            return Task.CompletedTask;
        }
        public Task UpsertStateMessageAsync(long userId, int messageId, CancellationToken ct)
        {
            Items.First(x => x.UserId == userId).StateMessageId = messageId;
            return Task.CompletedTask;
        }
    }
}
