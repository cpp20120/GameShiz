using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using BotFramework.Host.Composition.Builder;
using BotFramework.Rest;
using BotFramework.Sdk.MiniGames;
using Games.Blackjack.Infrastructure.Modules;
using Games.Blackjack.Rest;
using Games.Challenges.Rest;
using Games.Dice.Contracts.Play;
using Games.Dice.Infrastructure.Modules;
using Games.Dice.Rest;
using Games.DiceCube.Infrastructure.Modules;
using Games.Darts.Infrastructure.Modules;
using Games.Football.Infrastructure.Modules;
using Games.Basketball.Infrastructure.Modules;
using Games.Bowling.Infrastructure.Modules;
using Games.Horse.Infrastructure.Modules;
using Games.Horse.Rest;
using Games.Leaderboard.Infrastructure.Modules;
using Games.Leaderboard.Rest;
using Games.Meta.Infrastructure.Modules;
using Games.Meta.Rest;
using Games.Meta.Application.Tournaments;
using Games.NativeDice.Rest;
using Games.Pick.Infrastructure.Modules;
using Games.Pick.Rest;
using Games.PixelBattle.Infrastructure.Modules;
using Games.PixelBattle.Rest;
using Games.Poker.Infrastructure.Modules;
using Games.Poker.Rest;
using Games.Redeem.Infrastructure.Modules;
using Games.Redeem.Rest;
using Games.SecretHitler.Infrastructure.Modules;
using Games.SecretHitler.Rest;
using Games.Challenges.Infrastructure.Modules;
using Games.Transfer.Infrastructure.Modules;
using Games.Transfer.Rest;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Npgsql;
using Dapper;
using Testcontainers.PostgreSql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection("REST E2E")]
public sealed class RestE2ETests
{
    private readonly RestE2EFixture fixture;

    public RestE2ETests(RestE2EFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task DiceRoll_ThroughRest_UsesPostgresAndIsIdempotent()
    {
        var application = fixture.Application;
        using var client = application.CreateClient();
        const string path = "/api/v1/tenants/e2e/scopes/42/dice/roll";

        using (var unauthorized = await SendDiceAsync(client, path, null, authenticated: false))
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using (var missingKey = await SendDiceAsync(client, path, null, authenticated: true))
            Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);

        using var first = await SendDiceAsync(client, path, "rest-e2e-dice-1", authenticated: true);
        Assert.True(
            first.StatusCode == HttpStatusCode.OK,
            $"REST dice command failed with {(int)first.StatusCode}: {await first.Content.ReadAsStringAsync()}");
        var firstResult = await ReadResultAsync(first);

        using var duplicate = await SendDiceAsync(client, path, "rest-e2e-dice-1", authenticated: true);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateResult = await ReadResultAsync(duplicate);

        Assert.Equal(firstResult, duplicateResult);
        Assert.Equal(DicePlayStatus.Played, firstResult.Status);
        Assert.Equal(7, firstResult.Prize);
        Assert.Equal(6, firstResult.Stake);
        Assert.Equal(101, firstResult.Balance);
        Assert.Equal(1, firstResult.Tax);
        Assert.Equal(1, firstResult.DailyRollsUsed);
        Assert.Equal(5, firstResult.DailyRollLimit);

        Assert.Equal(1, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM dice_rolls WHERE user_id = 42"));
        Assert.Equal(2, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM economics_ledger WHERE telegram_user_id = 42 AND balance_scope_id = 42"));
        Assert.Equal(101, await fixture.ScalarAsync<int>(
            "SELECT coins FROM users WHERE telegram_user_id = 42 AND balance_scope_id = 42"));
        Assert.Equal(1, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM tenant_idempotency_keys WHERE idempotency_key LIKE 'dice:roll:%:42'"));
    }

    [Fact]
    public async Task MetaPokerAndSecretHitler_ThroughRest_ExposeStateAndStatuses()
    {
        var application = fixture.Application;
        using var client = application.CreateClient();
        const string root = "/api/v1/tenants/e2e/scopes/42";
        const string bearerToken = "e2e-secondary";

        using var profile = await GetJsonAsync(client, $"{root}/meta/profile", HttpStatusCode.OK, bearerToken);
        Assert.Equal("active", profile.RootElement.GetProperty("season").GetProperty("status").GetString());

        using var quests = await GetJsonAsync(client, $"{root}/meta/quests", HttpStatusCode.OK, bearerToken);
        Assert.Equal(JsonValueKind.Array, quests.RootElement.ValueKind);
        Assert.NotEmpty(quests.RootElement.EnumerateArray());

        using var invalidTop = await GetJsonAsync(client, $"{root}/meta/top?limit=0", HttpStatusCode.BadRequest, bearerToken);
        Assert.Contains("limit", invalidTop.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var invalidClan = await PostJsonAsync(
            client,
            $"{root}/meta/clan",
            new { tag = "", name = "" },
            HttpStatusCode.BadRequest,
            bearerToken: bearerToken);
        Assert.Contains("tag", invalidClan.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var invalidTournament = await PostJsonAsync(
            client,
            $"{root}/meta/tournaments",
            new { gameKey = "dice", entryFee = 0, maxPlayers = 1 },
            HttpStatusCode.BadRequest,
            bearerToken: bearerToken);
        Assert.Contains("Tournament", invalidTournament.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var unknownQuest = await PostJsonAsync(
            client,
            $"{root}/meta/quests/unknown/claim",
            new { },
            HttpStatusCode.NotFound,
            bearerToken: bearerToken);

        using var clanCreate = await PostJsonAsync(
            client,
            $"{root}/meta/clan",
            new { tag = "REST", name = "REST E2E Clan" },
            HttpStatusCode.OK,
            bearerToken: bearerToken);
        Assert.True(clanCreate.RootElement.GetProperty("created").GetBoolean());
        Assert.Equal("REST", clanCreate.RootElement.GetProperty("clan").GetProperty("tag").GetString());

        using var clan = await GetJsonAsync(client, $"{root}/meta/clan", HttpStatusCode.OK, bearerToken);
        Assert.Equal("REST", clan.RootElement.GetProperty("tag").GetString());

        using var pokerMissingKey = await PostJsonAsync(
            client,
            $"{root}/poker/tables",
            new { },
            HttpStatusCode.BadRequest,
            bearerToken: bearerToken);
        Assert.Contains("Idempotency-Key", pokerMissingKey.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        using var pokerCreate = await PostJsonAsync(
            client,
            $"{root}/poker/tables",
            new { },
            HttpStatusCode.Created,
            "rest-poker-create",
            bearerToken);
        Assert.Equal("None", pokerCreate.RootElement.GetProperty("error").GetString());
        var pokerInviteCode = pokerCreate.RootElement.GetProperty("inviteCode").GetString();
        Assert.False(string.IsNullOrWhiteSpace(pokerInviteCode));

        using var pokerStartTooEarly = await PostJsonAsync(
            client,
            $"{root}/poker/tables/{pokerInviteCode}/start",
            new { },
            HttpStatusCode.Conflict,
            "rest-poker-start-too-early",
            bearerToken);
        Assert.Contains("poker", pokerStartTooEarly.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var pokerInvalidAction = await PostJsonAsync(
            client,
            $"{root}/poker/tables/{pokerInviteCode}/actions",
            new { verb = "", amount = 0 },
            HttpStatusCode.BadRequest,
            "rest-poker-invalid-action",
            bearerToken);
        Assert.Contains("Verb", pokerInvalidAction.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var pokerUnknownTable = await PostJsonAsync(
            client,
            $"{root}/poker/tables/UNKNOWN/join",
            new { },
            HttpStatusCode.NotFound,
            "rest-poker-unknown-table",
            bearerToken);
        Assert.Contains("table", pokerUnknownTable.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var pokerDuplicate = await PostJsonAsync(
            client,
            $"{root}/poker/tables",
            new { },
            HttpStatusCode.Created,
            "rest-poker-create",
            bearerToken);
        Assert.Equal(pokerInviteCode, pokerDuplicate.RootElement.GetProperty("inviteCode").GetString());

        using var pokerTable = await GetJsonAsync(client, $"{root}/poker/tables/me", HttpStatusCode.OK, bearerToken);
        Assert.Equal(pokerInviteCode, pokerTable.RootElement.GetProperty("table").GetProperty("inviteCode").GetString());
        Assert.Equal("Seating", pokerTable.RootElement.GetProperty("table").GetProperty("status").GetString());
        Assert.Single(pokerTable.RootElement.GetProperty("seats").EnumerateArray());

        using var shCreate = await PostJsonAsync(
            client,
            $"{root}/secrethitler/game",
            new { playerChatId = 42 },
            HttpStatusCode.OK,
            bearerToken: bearerToken);
        Assert.Equal("None", shCreate.RootElement.GetProperty("error").GetString());
        var shInviteCode = shCreate.RootElement.GetProperty("inviteCode").GetString();
        Assert.False(string.IsNullOrWhiteSpace(shInviteCode));

        using var shStartTooEarly = await PostJsonAsync(
            client,
            $"{root}/secrethitler/game/start",
            new { },
            HttpStatusCode.OK,
            bearerToken: bearerToken);
        Assert.Equal("NotEnoughPlayers", shStartTooEarly.RootElement.GetProperty("error").GetString());

        using var shMissingJoinCode = await PostJsonAsync(
            client,
            $"{root}/secrethitler/game/join",
            new { code = "", playerChatId = 42 },
            HttpStatusCode.BadRequest,
            bearerToken: bearerToken);
        Assert.Contains("Code", shMissingJoinCode.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var shInvalidVote = await PostJsonAsync(
            client,
            $"{root}/secrethitler/game/vote",
            new { vote = "invalid" },
            HttpStatusCode.BadRequest,
            bearerToken: bearerToken);

        using var shGame = await GetJsonAsync(client, $"{root}/secrethitler/game", HttpStatusCode.OK, bearerToken);
        Assert.Equal(shInviteCode, shGame.RootElement.GetProperty("snapshot").GetProperty("game").GetProperty("inviteCode").GetString());
        Assert.Equal("Lobby", shGame.RootElement.GetProperty("snapshot").GetProperty("game").GetProperty("status").GetString());
        Assert.Single(shGame.RootElement.GetProperty("snapshot").GetProperty("players").EnumerateArray());
    }

    [Fact]
    public async Task Tournaments_ThroughRest_ExecuteLifecycleAndValidation()
    {
        var application = fixture.Application;
        using var client = application.CreateClient();
        const string root = "/api/v1/tenants/e2e/scopes/501";
        const string ownerToken = "e2e-tournament-owner";
        const string playerToken = "e2e-tournament-player";
        const string outsiderToken = "e2e-tournament-outsider";

        using var invalidOpen = await GetJsonAsync(
            client, $"{root}/meta/tournaments/open?limit=0", HttpStatusCode.BadRequest, ownerToken);
        Assert.Contains("limit", invalidOpen.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var unsupported = await PostJsonAsync(
            client, $"{root}/meta/tournaments",
            new { gameKey = "blackjack", entryFee = 0, maxPlayers = 2 }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-unsupported", bearerToken: ownerToken);
        Assert.False(unsupported.RootElement.GetProperty("created").GetBoolean());

        var tournamentId = await CreateTournamentAsync(client, root, ownerToken, "dice", 0, 2);

        using var unknown = await GetJsonAsync(
            client, $"{root}/meta/tournaments/{long.MaxValue}", HttpStatusCode.NotFound, ownerToken);
        Assert.Equal(JsonValueKind.Object, unknown.RootElement.ValueKind);

        using var open = await GetJsonAsync(
            client, $"{root}/meta/tournaments/open?limit=100", HttpStatusCode.OK, ownerToken);
        Assert.Contains(
            open.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetInt64() == tournamentId);

        using var playersBeforeJoin = await GetJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/players", HttpStatusCode.OK, ownerToken);
        Assert.Empty(playersBeforeJoin.RootElement.EnumerateArray());

        using var wrongChatJoin = await PostJsonAsync(
            client,
            $"/api/v1/tenants/e2e/scopes/502/meta/tournaments/{tournamentId}/join",
            new { }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-wrong-chat", bearerToken: "e2e-tournament-wrong-chat");
        Assert.False(wrongChatJoin.RootElement.GetProperty("joined").GetBoolean());
        Assert.Contains("другом чате", wrongChatJoin.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

        using var ownerJoin = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/join", new { }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-owner-join", bearerToken: ownerToken);
        Assert.True(ownerJoin.RootElement.GetProperty("joined").GetBoolean());
        Assert.Equal(1, ownerJoin.RootElement.GetProperty("tournament").GetProperty("playerCount").GetInt32());

        using var duplicateOwnerJoin = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/join", new { }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-owner-duplicate-join", bearerToken: ownerToken);
        Assert.False(duplicateOwnerJoin.RootElement.GetProperty("joined").GetBoolean());

        using var playerJoin = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/join", new { }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-player-join", bearerToken: playerToken);
        Assert.True(playerJoin.RootElement.GetProperty("joined").GetBoolean());
        Assert.Equal(2, playerJoin.RootElement.GetProperty("tournament").GetProperty("playerCount").GetInt32());

        using var outsiderStart = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/start", new { }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-outsider-start", bearerToken: outsiderToken);
        Assert.False(outsiderStart.RootElement.GetBoolean());

        using var started = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/start", new { }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-owner-start", bearerToken: ownerToken);
        Assert.True(started.RootElement.GetBoolean());

        using var duplicateStart = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/start", new { }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-owner-duplicate-start", bearerToken: ownerToken);
        Assert.False(duplicateStart.RootElement.GetBoolean());

        using var tournamentStarted = await GetJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}", HttpStatusCode.OK, ownerToken);
        Assert.Equal("started", tournamentStarted.RootElement.GetProperty("status").GetString());

        using var matches = await GetJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/matches", HttpStatusCode.OK, ownerToken);
        var match = Assert.Single(matches.RootElement.EnumerateArray().ToArray());
        Assert.Equal("ready", match.GetProperty("status").GetString());
        Assert.Equal(55, match.GetProperty("player1UserId").GetInt64());
        Assert.Equal(56, match.GetProperty("player2UserId").GetInt64());
        var matchId = match.GetProperty("id").GetInt64();

        using var forbiddenReport = await PostJsonAsync(
            client, $"{root}/meta/tournaments/matches/{matchId}/report",
            new { victorUserId = 56 }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-outsider-report", bearerToken: outsiderToken);
        Assert.False(forbiddenReport.RootElement.GetProperty("updated").GetBoolean());

        using var invalidVictor = await PostJsonAsync(
            client, $"{root}/meta/tournaments/matches/{matchId}/report",
            new { victorUserId = 999 }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-invalid-report", bearerToken: ownerToken);
        Assert.False(invalidVictor.RootElement.GetProperty("updated").GetBoolean());

        using var report = await PostJsonAsync(
            client, $"{root}/meta/tournaments/matches/{matchId}/report",
            new { victorUserId = 56 }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-report", bearerToken: ownerToken);
        Assert.True(report.RootElement.GetProperty("updated").GetBoolean());
        Assert.True(report.RootElement.GetProperty("finished").GetBoolean());

        using var finished = await GetJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}", HttpStatusCode.OK, ownerToken);
        Assert.Equal("finished", finished.RootElement.GetProperty("status").GetString());

        using var finalPlayers = await GetJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/players", HttpStatusCode.OK, ownerToken);
        var finalPlayerRows = finalPlayers.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, finalPlayerRows.Length);
        Assert.Contains(finalPlayerRows, item =>
            item.GetProperty("userId").GetInt64() == 56 && item.GetProperty("status").GetString() == "winner");
        Assert.Contains(finalPlayerRows, item =>
            item.GetProperty("userId").GetInt64() == 55 && item.GetProperty("status").GetString() == "eliminated");

        var manualFinishId = await CreateTournamentAsync(client, root, ownerToken, "dice", 0, 2);
        await JoinTournamentAsync(client, root, manualFinishId, ownerToken);
        await JoinTournamentAsync(client, root, manualFinishId, playerToken);
        using var manualStart = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{manualFinishId}/start", new { }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-manual-start", bearerToken: ownerToken);
        Assert.True(manualStart.RootElement.GetBoolean());

        using var manualFinish = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{manualFinishId}/finish",
            new { victorUserId = 55 }, HttpStatusCode.OK,
            idempotencyKey: "rest-e2e-tournament-manual-finish", bearerToken: ownerToken);
        Assert.Equal("winner", manualFinish.RootElement.GetProperty("status").GetString());

        var cancelId = await CreateTournamentAsync(client, root, ownerToken, "dice", 0, 2);
        using var cancelled = await DeleteJsonAsync(
            client, $"{root}/meta/tournaments/{cancelId}", HttpStatusCode.OK, ownerToken);
        Assert.Empty(cancelled.RootElement.EnumerateArray());

        using var cancelledState = await GetJsonAsync(
            client, $"{root}/meta/tournaments/{cancelId}", HttpStatusCode.OK, ownerToken);
        Assert.Equal("cancelled", cancelledState.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Tournaments_ThroughRest_SerializeConcurrentJoinStartAndReportCommands()
    {
        var application = fixture.Application;
        using var client = application.CreateClient();
        const string root = "/api/v1/tenants/e2e/scopes/501";
        const string ownerToken = "e2e-tournament-owner";
        const string playerToken = "e2e-tournament-player";
        const string outsiderToken = "e2e-tournament-outsider";

        var tournamentId = await CreateTournamentAsync(client, root, ownerToken, "dice", 0, 2);

        var joinResponses = await Task.WhenAll(
            PostJsonAsync(client, $"{root}/meta/tournaments/{tournamentId}/join", new { }, HttpStatusCode.OK,
                $"rest-e2e-race-join-owner-{Guid.NewGuid():N}", ownerToken),
            PostJsonAsync(client, $"{root}/meta/tournaments/{tournamentId}/join", new { }, HttpStatusCode.OK,
                $"rest-e2e-race-join-player-{Guid.NewGuid():N}", playerToken),
            PostJsonAsync(client, $"{root}/meta/tournaments/{tournamentId}/join", new { }, HttpStatusCode.OK,
                $"rest-e2e-race-join-outsider-{Guid.NewGuid():N}", outsiderToken));

        try
        {
            Assert.Equal(2, joinResponses.Count(response => response.RootElement.GetProperty("joined").GetBoolean()));
            Assert.Equal(1, joinResponses.Count(response => !response.RootElement.GetProperty("joined").GetBoolean()));

            using var players = await GetJsonAsync(
                client, $"{root}/meta/tournaments/{tournamentId}/players", HttpStatusCode.OK, ownerToken);
            Assert.Equal(2, players.RootElement.GetArrayLength());

            var startResponses = await Task.WhenAll(
                PostJsonAsync(client, $"{root}/meta/tournaments/{tournamentId}/start", new { }, HttpStatusCode.OK,
                    $"rest-e2e-race-start-a-{Guid.NewGuid():N}", ownerToken),
                PostJsonAsync(client, $"{root}/meta/tournaments/{tournamentId}/start", new { }, HttpStatusCode.OK,
                    $"rest-e2e-race-start-b-{Guid.NewGuid():N}", ownerToken));

            try
            {
                Assert.Equal(1, startResponses.Count(response => response.RootElement.GetBoolean()));
                Assert.Equal(1, startResponses.Count(response => !response.RootElement.GetBoolean()));
            }
            finally
            {
                foreach (var response in startResponses) response.Dispose();
            }

            using var matches = await GetJsonAsync(
                client, $"{root}/meta/tournaments/{tournamentId}/matches", HttpStatusCode.OK, ownerToken);
            var match = Assert.Single(matches.RootElement.EnumerateArray().ToArray());
            Assert.Equal("ready", match.GetProperty("status").GetString());
            var matchId = match.GetProperty("id").GetInt64();
            var victorUserId = match.GetProperty("player1UserId").GetInt64();

            var reportResponses = await Task.WhenAll(
                PostJsonAsync(client, $"{root}/meta/tournaments/matches/{matchId}/report",
                    new { victorUserId }, HttpStatusCode.OK,
                    $"rest-e2e-race-report-a-{Guid.NewGuid():N}", ownerToken),
                PostJsonAsync(client, $"{root}/meta/tournaments/matches/{matchId}/report",
                    new { victorUserId }, HttpStatusCode.OK,
                    $"rest-e2e-race-report-b-{Guid.NewGuid():N}", ownerToken));

            try
            {
                Assert.Equal(1, reportResponses.Count(response => response.RootElement.GetProperty("updated").GetBoolean()));
                Assert.Equal(1, reportResponses.Count(response => !response.RootElement.GetProperty("updated").GetBoolean()));
            }
            finally
            {
                foreach (var response in reportResponses) response.Dispose();
            }

            using var finished = await GetJsonAsync(
                client, $"{root}/meta/tournaments/{tournamentId}", HttpStatusCode.OK, ownerToken);
            Assert.Equal("finished", finished.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            foreach (var response in joinResponses) response.Dispose();
        }
    }

    private async Task<long> CreateTournamentAsync(
        HttpClient client,
        string root,
        string bearerToken,
        string gameKey,
        int entryFee,
        int maxPlayers)
    {
        using var response = await PostJsonAsync(
            client, $"{root}/meta/tournaments",
            new { gameKey, entryFee, maxPlayers }, HttpStatusCode.OK,
            idempotencyKey: $"rest-e2e-tournament-create-{Guid.NewGuid():N}", bearerToken: bearerToken);
        Assert.True(response.RootElement.GetProperty("created").GetBoolean(), response.RootElement.GetRawText());
        return response.RootElement.GetProperty("tournament").GetProperty("id").GetInt64();
    }

    private static async Task JoinTournamentAsync(
        HttpClient client,
        string root,
        long tournamentId,
        string bearerToken)
    {
        using var response = await PostJsonAsync(
            client, $"{root}/meta/tournaments/{tournamentId}/join", new { }, HttpStatusCode.OK,
            idempotencyKey: $"rest-e2e-tournament-join-{tournamentId}-{Guid.NewGuid():N}", bearerToken: bearerToken);
        Assert.True(response.RootElement.GetProperty("joined").GetBoolean());
    }

    [Fact]
    public async Task BlackjackPick_ThroughRest_ExecuteAndValidate()
    {
        var application = fixture.Application;
        using var client = application.CreateClient();
        const string root = "/api/v1/tenants/e2e/scopes/42";
        const string bearerToken = "e2e-games";

        using var blackjackMissingState = await GetJsonAsync(
            client, $"{root}/blackjack/state", HttpStatusCode.NotFound, bearerToken);

        using var blackjackInvalidBet = await PostJsonAsync(
            client, $"{root}/blackjack/start", new { bet = 0 }, HttpStatusCode.BadRequest,
            bearerToken: bearerToken);
        Assert.Contains("Bet", blackjackInvalidBet.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var blackjackStart = await PostJsonAsync(
            client, $"{root}/blackjack/start", new { bet = 1 }, HttpStatusCode.OK,
            "rest-e2e-blackjack-start", bearerToken);
        Assert.Equal("None", blackjackStart.RootElement.GetProperty("error").GetString());

        using var blackjackMissingKey = await PostJsonAsync(
            client, $"{root}/blackjack/hit", new { }, HttpStatusCode.BadRequest,
            bearerToken: bearerToken);
        Assert.Contains("Idempotency-Key", blackjackMissingKey.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        using var pickInvalid = await PostJsonAsync(
            client, $"{root}/pick", new { amount = 1, variants = new[] { "only" }, backedIndices = new[] { 0 } },
            HttpStatusCode.BadRequest, bearerToken: bearerToken);
        Assert.Contains("Variants", pickInvalid.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        using var pick = await PostJsonAsync(
            client, $"{root}/pick",
            new { amount = 1, variants = new[] { "red", "black" }, backedIndices = new[] { 0 } },
            HttpStatusCode.OK, "rest-e2e-pick-play", bearerToken);
        Assert.Equal("None", pick.RootElement.GetProperty("error").GetString());
        Assert.True(pick.RootElement.GetProperty("variants").GetArrayLength() == 2);

        using var lottery = await PostJsonAsync(
            client, $"{root}/pick/lottery", new { stake = 1 }, HttpStatusCode.OK,
            "rest-e2e-pick-lottery", bearerToken);
        Assert.Equal("Ok", lottery.RootElement.GetProperty("status").GetString());

        using var lotteryInfo = await GetJsonAsync(
            client, $"{root}/pick/lottery", HttpStatusCode.OK, bearerToken);
        Assert.Equal(JsonValueKind.Object, lotteryInfo.RootElement.ValueKind);

        using var dailyInvalidLimit = await GetJsonAsync(
            client, $"{root}/pick/daily/history?limit=0", HttpStatusCode.BadRequest, bearerToken);
        Assert.Contains("limit", dailyInvalidLimit.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var schedule = await GetJsonAsync(
            client, $"{root}/pick/daily/schedule", HttpStatusCode.OK, bearerToken);
        Assert.Equal(JsonValueKind.Object, schedule.RootElement.ValueKind);

    }

    [Fact]
    public async Task BlackjackAndPick_ThroughRest_ExecuteTurnLotteryDailyAndChainBranches()
    {
        var application = fixture.Application;
        using var client = application.CreateClient();
        const string root = "/api/v1/tenants/e2e/scopes/42";

        await ExecuteBlackjackActionAsync(client, root, "e2e-blackjack-stand", "stand");
        await ExecuteBlackjackActionAsync(client, root, "e2e-blackjack-hit", "hit");
        await ExecuteBlackjackActionAsync(client, root, "e2e-blackjack-double", "double");

        const string lotteryRoot = "/api/v1/tenants/e2e/scopes/420";
        using var lottery = await PostJsonAsync(
            client, $"{lotteryRoot}/pick/lottery", new { stake = 1 }, HttpStatusCode.OK,
            "rest-e2e-pick-extended-lottery-open", "e2e-pick-lottery");
        Assert.Equal("Ok", lottery.RootElement.GetProperty("status").GetString());

        using var joinedLottery = await PostJsonAsync(
            client, $"{lotteryRoot}/pick/lottery/join", new { }, HttpStatusCode.OK,
            "rest-e2e-pick-extended-lottery-join", "e2e-pick-lottery-target");
        Assert.Equal("Ok", joinedLottery.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, joinedLottery.RootElement.GetProperty("entrants").GetInt32());

        using var lotteryInfo = await GetJsonAsync(
            client, $"{lotteryRoot}/pick/lottery", HttpStatusCode.OK, "e2e-pick-lottery");
        Assert.Equal(2, lotteryInfo.RootElement.GetProperty("entrants").GetInt32());

        using var cancelledLottery = await DeleteJsonAsync(
            client, $"{lotteryRoot}/pick/lottery", HttpStatusCode.OK, "e2e-pick-lottery");
        Assert.Equal("Cancelled", cancelledLottery.RootElement.GetProperty("kind").GetString());

        const string dailyRoot = "/api/v1/tenants/e2e/scopes/430";
        using var invalidDailyBuy = await PostJsonAsync(
            client, $"{dailyRoot}/pick/daily", new { count = 0 }, HttpStatusCode.BadRequest,
            bearerToken: "e2e-pick-daily");
        Assert.Contains("Count", invalidDailyBuy.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        using var dailyBuy = await PostJsonAsync(
            client, $"{dailyRoot}/pick/daily", new { count = 1 }, HttpStatusCode.OK,
            "rest-e2e-pick-daily-buy", "e2e-pick-daily");
        Assert.Equal("Ok", dailyBuy.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, dailyBuy.RootElement.GetProperty("ticketsBought").GetInt32());

        using var dailyInfo = await GetJsonAsync(
            client, $"{dailyRoot}/pick/daily", HttpStatusCode.OK, "e2e-pick-daily");
        Assert.Equal(JsonValueKind.Object, dailyInfo.RootElement.ValueKind);

        using var dailyHistory = await GetJsonAsync(
            client, $"{dailyRoot}/pick/daily/history?limit=1", HttpStatusCode.OK, "e2e-pick-daily");
        Assert.Equal(JsonValueKind.Array, dailyHistory.RootElement.ValueKind);

        const string chainRoot = "/api/v1/tenants/e2e/scopes/440";
        var chainId = Guid.NewGuid();
        var chain = new
        {
            id = chainId,
            userId = 53,
            chatId = 440,
            displayName = "Chain",
            stakeForNext = 1,
            depth = 0,
            variants = new[] { "yes", "no" },
            backedIndices = new[] { 0 },
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        };

        using var forbiddenChain = await PostJsonAsync(
            client, $"{chainRoot}/pick/chains/continue", chain, HttpStatusCode.Forbidden,
            bearerToken: "e2e-pick-chain-other");
        Assert.Contains("belong", forbiddenChain.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var continuedChain = await PostJsonAsync(
            client, $"{chainRoot}/pick/chains/continue", chain, HttpStatusCode.OK,
            "rest-e2e-pick-chain-continue", "e2e-pick-chain");
        Assert.Equal("None", continuedChain.RootElement.GetProperty("error").GetString());

        using var unknownChain = await PostJsonAsync(
            client, $"{chainRoot}/pick/chains/{Guid.NewGuid()}/claim", new { }, HttpStatusCode.NotFound,
            bearerToken: "e2e-pick-chain");
        Assert.Equal(JsonValueKind.Object, unknownChain.RootElement.ValueKind);
    }

    private static async Task ExecuteBlackjackActionAsync(
        HttpClient client, string root, string bearerToken, string action)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var start = await PostJsonAsync(
                client, $"{root}/blackjack/start", new { bet = 1 }, HttpStatusCode.OK,
                $"rest-e2e-{action}-start-{attempt}", bearerToken);
            Assert.Equal("None", start.RootElement.GetProperty("error").GetString());

            var snapshot = start.RootElement.GetProperty("snapshot");
            if (snapshot.ValueKind == JsonValueKind.Null || snapshot.GetProperty("dealerHoleRevealed").GetBoolean())
                continue;

            using var result = await PostJsonAsync(
                client, $"{root}/blackjack/{action}", new { }, HttpStatusCode.OK,
                $"rest-e2e-{action}-{attempt}", bearerToken);
            Assert.Equal("None", result.RootElement.GetProperty("error").GetString());
            return;
        }

        throw new Xunit.Sdk.XunitException($"Could not get an active blackjack hand for '{action}'.");
    }

    [Fact]
    public async Task RemainingGames_ThroughRest_ExposeStateAndValidation()
    {
        var application = fixture.Application;
        using var client = application.CreateClient();
        const string root = "/api/v1/tenants/e2e/scopes/42";
        const string bearerToken = "e2e-games";
        const string targetToken = "e2e-target";

        using var horseInfo = await GetJsonAsync(
            client, $"{root}/horse/info", HttpStatusCode.OK, bearerToken);
        Assert.Equal(JsonValueKind.Object, horseInfo.RootElement.ValueKind);

        using var horseResult = await GetJsonAsync(
            client, $"{root}/horse/result", HttpStatusCode.OK, bearerToken);
        Assert.Equal(JsonValueKind.Object, horseResult.RootElement.ValueKind);

        using var horseInvalid = await PostJsonAsync(
            client, $"{root}/horse/bet", new { horseId = 0, amount = 1 }, HttpStatusCode.BadRequest,
            bearerToken: bearerToken);
        Assert.Contains("HorseId", horseInvalid.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        using var horseBet = await PostJsonAsync(
            client, $"{root}/horse/bet", new { horseId = 1, amount = 1 }, HttpStatusCode.OK,
            "rest-e2e-horse-bet", bearerToken);
        Assert.Equal(JsonValueKind.Object, horseBet.RootElement.ValueKind);

        using var topInvalid = await GetJsonAsync(
            client, $"{root}/leaderboard?limit=0", HttpStatusCode.BadRequest, bearerToken);
        Assert.Contains("limit", topInvalid.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var top = await GetJsonAsync(
            client, $"{root}/leaderboard?limit=10", HttpStatusCode.OK, bearerToken);
        Assert.Equal(JsonValueKind.Object, top.RootElement.ValueKind);

        using var grid = await GetJsonAsync(
            client, $"{root}/pixelbattle/grid", HttpStatusCode.OK, bearerToken);
        Assert.Equal(32_000, grid.RootElement.GetProperty("tiles").GetArrayLength());

        using var pixelInvalid = await PostJsonAsync(
            client, $"{root}/pixelbattle/pixels", new { index = -1, color = "#000000" },
            HttpStatusCode.BadRequest, bearerToken: bearerToken);
        Assert.Contains("pixel", pixelInvalid.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var pixel = await PostJsonAsync(
            client, $"{root}/pixelbattle/pixels", new { index = 0, color = "#000000" },
            HttpStatusCode.OK, "rest-e2e-pixelbattle-update", bearerToken);
        Assert.Equal("Updated", pixel.RootElement.GetProperty("status").GetString());

        using var transferInvalid = await PostJsonAsync(
            client, $"{root}/transfer", new { toUserId = 0, recipientDisplayName = "Target", amount = 1 },
            HttpStatusCode.BadRequest, bearerToken: bearerToken);
        Assert.Contains("ToUserId", transferInvalid.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        using var targetSeed = await PostJsonAsync(
            client, $"{root}/blackjack/start", new { bet = 1 }, HttpStatusCode.OK,
            "rest-e2e-target-seed", targetToken);
        Assert.Equal("None", targetSeed.RootElement.GetProperty("error").GetString());

        using var transfer = await PostJsonAsync(
            client, $"{root}/transfer",
            new { toUserId = 45, recipientDisplayName = "Target", amount = 1 },
            HttpStatusCode.OK, "rest-e2e-transfer", bearerToken);
        Assert.Equal(JsonValueKind.Object, transfer.RootElement.ValueKind);

        using var challengeInvalid = await PostJsonAsync(
            client, $"{root}/challenges",
            new { targetUserId = 44, targetDisplayName = "Games", amount = 1, game = "Dice" },
            HttpStatusCode.BadRequest, bearerToken: bearerToken);
        Assert.Contains("another", challengeInvalid.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var challengeCreate = await PostJsonAsync(
            client, $"{root}/challenges",
            new { targetUserId = 45, targetDisplayName = "Target", amount = 1, game = "Dice" },
            HttpStatusCode.OK, bearerToken: bearerToken);
        Assert.Equal("None", challengeCreate.RootElement.GetProperty("error").GetString());
        var challengeId = challengeCreate.RootElement.GetProperty("challenge").GetProperty("id").GetGuid();

        using var challengeAccept = await PostJsonAsync(
            client, $"{root}/challenges/{challengeId}/accept", new { }, HttpStatusCode.OK,
            bearerToken: targetToken);
        Assert.Equal("None", challengeAccept.RootElement.GetProperty("error").GetString());
        Assert.Equal("Completed", challengeAccept.RootElement.GetProperty("challenge").GetProperty("status").GetString());

        using var redeemInvalid = await PostJsonAsync(
            client, $"{root}/redeem/begin", new { code = "" }, HttpStatusCode.BadRequest,
            bearerToken: bearerToken);
        Assert.Contains("Code", redeemInvalid.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        using var redeemUnknown = await PostJsonAsync(
            client, $"{root}/redeem/begin", new { code = "not-a-real-code" }, HttpStatusCode.OK,
            bearerToken: bearerToken);
        Assert.Equal("InvalidCode", redeemUnknown.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task NativeDiceGames_ThroughRest_ExecuteValidationAndSettlement()
    {
        var application = fixture.Application;
        using var client = application.CreateClient();
        const string bearerToken = "e2e-games";

        await AssertNativeDiceGameAsync(
            client, "/api/v1/tenants/e2e/scopes/42", "/api/v1/tenants/e2e/scopes/42",
            "dicecube", "roll", "Rolled", 6, bearerToken);
        await AssertNativeQuickPlayAsync(
            client, "/api/v1/tenants/e2e/scopes/42", "darts", 6, bearerToken);
        await AssertNativeDiceGameAsync(
            client, "/api/v1/tenants/e2e/scopes/42", "/api/v1/tenants/e2e/scopes/42",
            "football", "roll", "Thrown", 5, bearerToken);
        await AssertNativeDiceGameAsync(
            client, "/api/v1/tenants/e2e/scopes/42", "/api/v1/tenants/e2e/scopes/42",
            "basketball", "roll", "Thrown", 5, bearerToken);
        await AssertNativeDiceGameAsync(
            client, "/api/v1/tenants/e2e/scopes/42", "/api/v1/tenants/e2e/scopes/42",
            "bowling", "roll", "Rolled", 6, bearerToken);
    }

    private static async Task AssertNativeQuickPlayAsync(
        HttpClient client,
        string root,
        string game,
        int maxFace,
        string bearerToken)
    {
        using var invalid = await PostJsonAsync(
            client, $"{root}/{game}/play", new { amount = 0 }, HttpStatusCode.BadRequest,
            $"rest-e2e-{game}-invalid", bearerToken);
        Assert.Contains("positive", invalid.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var play = await PostJsonAsync(
            client, $"{root}/{game}/play", new { amount = 1 }, HttpStatusCode.OK,
            $"rest-e2e-{game}-play", bearerToken);
        var face = play.RootElement.GetProperty("face").GetInt32();
        var result = play.RootElement.GetProperty("result");
        Assert.InRange(face, 1, maxFace);
        Assert.Equal(face, result.GetProperty("face").GetInt32());
        Assert.Equal("Thrown", result.GetProperty("outcome").GetString());
        Assert.Equal(1, result.GetProperty("bet").GetInt32());
        Assert.True(result.GetProperty("multiplier").GetInt32() >= 0);
        Assert.True(result.GetProperty("payout").GetInt32() >= 0);
    }

    private static async Task AssertNativeDiceGameAsync(
        HttpClient client,
        string pendingRoot,
        string playRoot,
        string game,
        string playResultProperty,
        string outcome,
        int maxFace,
        string bearerToken)
    {
        using var invalid = await PostJsonAsync(
            client, $"{playRoot}/{game}/play", new { amount = 0 }, HttpStatusCode.BadRequest,
            $"rest-e2e-{game}-invalid", bearerToken);
        Assert.Contains("positive", invalid.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        using var bet = await PostJsonAsync(
            client, $"{pendingRoot}/{game}/bet", new { amount = 1 }, HttpStatusCode.OK,
            $"rest-e2e-{game}-bet", bearerToken);
        Assert.Equal("None", bet.RootElement.GetProperty("error").GetString());

        using var settled = await PostJsonAsync(
            client, $"{pendingRoot}/{game}/roll", new { face = maxFace }, HttpStatusCode.OK,
            $"rest-e2e-{game}-roll", bearerToken);

        Assert.Equal(outcome, settled.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(maxFace, settled.RootElement.GetProperty("face").GetInt32());
        Assert.Equal(2, settled.RootElement.GetProperty("multiplier").GetInt32());
        Assert.Equal(2, settled.RootElement.GetProperty("payout").GetInt32());

        using var play = await PostJsonAsync(
            client, $"{playRoot}/{game}/play", new { amount = 1 }, HttpStatusCode.OK,
            $"rest-e2e-{game}-play", bearerToken);
        var playFace = play.RootElement.GetProperty("face").GetInt32();
        Assert.InRange(playFace, 1, maxFace);
        Assert.Equal("None", play.RootElement.GetProperty("bet").GetProperty("error").GetString());
        Assert.Equal(outcome, play.RootElement.GetProperty(playResultProperty).GetProperty("outcome").GetString());
        Assert.Equal(playFace, play.RootElement.GetProperty(playResultProperty).GetProperty("face").GetInt32());
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string path,
        HttpStatusCode expectedStatus,
        string bearerToken = "e2e")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus} but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    private static async Task<JsonDocument> PostJsonAsync(
        HttpClient client,
        string path,
        object payload,
        HttpStatusCode expectedStatus,
        string? idempotencyKey = null,
        string bearerToken = "e2e")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus} but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    private static async Task<JsonDocument> DeleteJsonAsync(
        HttpClient client,
        string path,
        HttpStatusCode expectedStatus,
        string bearerToken = "e2e")
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus} but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    private static async Task<HttpResponseMessage> SendDiceAsync(
        HttpClient client,
        string path,
        string? idempotencyKey,
        bool authenticated)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { slotValue = 6 }),
        };
        if (authenticated)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "e2e");
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task<DicePlayResponse> ReadResultAsync(HttpResponseMessage response)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var result = await response.Content.ReadFromJsonAsync<DicePlayResponse>(options);
        return result ?? throw new InvalidOperationException("REST response did not contain a dice result.");
    }
}

[CollectionDefinition("REST E2E", DisableParallelization = true)]
public sealed class RestE2ETestCollection : ICollectionFixture<RestE2EFixture>;

public sealed class RestE2EFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("casinoshiz_rest_e2e")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private RestE2EApplication? application;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        application = await StartApplicationAsync();
    }

    public async Task DisposeAsync()
    {
        if (application is not null)
            await application.DisposeAsync();
        await database.DisposeAsync();
    }

    public RestE2EApplication Application => application
        ?? throw new InvalidOperationException("REST E2E application is not initialized.");

    public async Task<RestE2EApplication> StartApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RestE2EFixture).Assembly.GetName().Name,
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http1));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = database.GetConnectionString(),
            ["Bot:Enabled"] = "false",
            ["Bot:StartingCoins"] = "100",
            ["Bot:Admins:0"] = "925337014",
            ["Redis:Enabled"] = "false",
            ["ClickHouse:Enabled"] = "false",
            ["Rest:ApiVersion"] = "v1",
            ["Rest:OpenApiEnabled"] = "false",
            ["Rest:RequireTenantClaim"] = "true",
            ["Rest:RequireScopeClaim"] = "true",
            ["Rest:RequireIdempotencyKeyForCommands"] = "true",
            ["DurableWorkflow:Mode"] = "Solo",
            ["DurableWorkflow:AutoCreate"] = "true",
            ["Games:dice:Cost"] = "5",
            ["Games:dice:RedeemDropChance"] = "0",
            ["Games:dicecube:MinSecondsBetweenBets"] = "0",
            ["Games:dicecube:RedeemDropChance"] = "0",
            ["Games:darts:RedeemDropChance"] = "0",
            ["Games:football:RedeemDropChance"] = "0",
            ["Games:basketball:RedeemDropChance"] = "0",
            ["Games:bowling:RedeemDropChance"] = "0",
            ["Bot:TelegramDiceDailyLimit:MaxRollsPerUserPerDayByGame:dice"] = "5",
            ["Games:poker:BuyIn"] = "10",
            ["Games:sh:BuyIn"] = "10",
        });

        builder.AddDurableWorkflows(typeof(TournamentWorkflowHandler).Assembly);
        builder.AddBackendFramework()
            .AddModule<DiceModule>()
            .AddModule<DiceCubeModule>()
            .AddModule<DartsRemoteModule>()
            .AddModule<FootballModule>()
            .AddModule<BasketballModule>()
            .AddModule<BowlingModule>()
            .AddModule<BlackjackModule>()
            .AddModule<ChallengeModule>()
            .AddModule<HorseModule>()
            .AddModule<LeaderboardModule>()
            .AddModule<MetaModule>()
            .AddModule<PickModule>()
            .AddModule<PixelBattleModule>()
            .AddModule<PokerModule>()
            .AddModule<RedeemModule>()
            .AddModule<SecretHitlerModule>()
            .AddModule<TransferModule>();

        builder.AddRestFramework();
        builder.Services.AddBlackjackRest();
        builder.Services.AddChallengesRest();
        builder.Services.AddDiceRest();
        builder.Services.AddNativeDiceRest();
        builder.Services.AddSingleton<IMiniGameSessionGhostHeal, NullMiniGameSessionGhostHeal>();
        builder.Services.AddHorseRest();
        builder.Services.AddLeaderboardRest();
        builder.Services.AddMetaRest();
        builder.Services.AddPickRest();
        builder.Services.AddPixelBattleRest();
        builder.Services.AddPokerRest();
        builder.Services.AddRedeemRest();
        builder.Services.AddSecretHitlerRest();
        builder.Services.AddTransferRest();
        builder.Services.AddSingleton<BotFramework.Contracts.Identity.IPlayerDirectory, BotFramework.Contracts.Identity.NullPlayerDirectory>();
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                static _ => { });

        var app = builder.Build();
        app.UseRestFramework();
        app.MapRestFramework();
        await app.StartAsync();
        await WaitForWolverineStoreAsync();

        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;
        var address = addresses?.SingleOrDefault()
            ?? throw new InvalidOperationException("REST E2E application did not publish an address.");
        return new RestE2EApplication(app, new Uri(address, UriKind.Absolute));
    }

    private async Task WaitForWolverineStoreAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var connection = new NpgsqlConnection(database.GetConnectionString());
            await connection.OpenAsync();
            var tables = (await connection.QueryAsync<string>(
                "SELECT table_schema || '.' || table_name "
                + "FROM information_schema.tables "
                + "WHERE table_name ILIKE '%wolverine%' OR table_name ILIKE '%incoming%' OR table_name ILIKE '%outgoing%' "
                + "ORDER BY table_schema, table_name")).ToArray();
            if (tables.Any(static table => table.EndsWith(".wolverine_incoming_envelopes", StringComparison.Ordinal)))
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        await using var diagnosticConnection = new NpgsqlConnection(database.GetConnectionString());
        await diagnosticConnection.OpenAsync();
        var existingTables = (await diagnosticConnection.QueryAsync<string>(
            "SELECT table_schema || '.' || table_name "
            + "FROM information_schema.tables "
            + "WHERE table_name ILIKE '%wolverine%' OR table_name ILIKE '%incoming%' OR table_name ILIKE '%outgoing%' "
            + "ORDER BY table_schema, table_name")).ToArray();
        throw new InvalidOperationException(
            $"Wolverine PostgreSQL message store did not become ready. Tables: {string.Join(", ", existingTables)}");
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        var result = await connection.ExecuteScalarAsync<T>(sql);
        return result is null ? throw new InvalidOperationException("Scalar query returned null.") : result;
    }

}

public sealed class RestE2EApplication(WebApplication app, Uri address) : IAsyncDisposable
{
    public HttpClient CreateClient() => new() { BaseAddress = address };

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "RestE2E";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.Authorization.Any(value =>
                value is { } header && header.StartsWith("Bearer e2e", StringComparison.Ordinal)))
            return Task.FromResult(AuthenticateResult.NoResult());

        var authorization = Request.Headers.Authorization.FirstOrDefault();
        var token = authorization is { } value && value.StartsWith("Bearer ", StringComparison.Ordinal)
            ? value["Bearer ".Length..]
            : string.Empty;
        var userId = token switch
        {
            "e2e-blackjack-stand" => "46",
            "e2e-blackjack-hit" => "47",
            "e2e-blackjack-double" => "48",
            "e2e-pick-lottery" => "50",
            "e2e-pick-lottery-target" => "51",
            "e2e-pick-daily" => "52",
            "e2e-pick-chain" => "53",
            "e2e-pick-chain-other" => "54",
            "e2e-tournament-owner" => "55",
            "e2e-tournament-player" => "56",
            "e2e-tournament-outsider" => "57",
            "e2e-tournament-wrong-chat" => "58",
            "e2e-fuzz-commands" => "70",
            "e2e-fuzz-validation" => "71",
            "e2e-target" => "45",
            "e2e-games" => "44",
            "e2e-secondary" => "43",
            _ => "42",
        };
        var scopeId = token switch
        {
            "e2e-pick-lottery" or "e2e-pick-lottery-target" => "420",
            "e2e-pick-daily" => "430",
            "e2e-pick-chain" or "e2e-pick-chain-other" => "440",
            "e2e-tournament-owner" or "e2e-tournament-player" or "e2e-tournament-outsider" => "501",
            "e2e-tournament-wrong-chat" => "502",
            _ => "42",
        };
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("name", userId switch
            {
                "43" => "Bob",
                "44" => "Games",
                "45" => "Target",
                "46" => "Stand",
                "47" => "Hit",
                "48" => "Double",
                "50" => "Lottery opener",
                "51" => "Lottery joiner",
                "52" => "Daily player",
                "53" => "Chain player",
                "54" => "Other chain player",
                "55" => "Tournament owner",
                "56" => "Tournament player",
                "57" => "Tournament outsider",
                "58" => "Tournament wrong chat",
                "70" => "Fuzz commands",
                "71" => "Fuzz validation",
                _ => "Alice",
            }),
            new Claim("tenant_id", "e2e"),
            new Claim("scope_id", scopeId),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
