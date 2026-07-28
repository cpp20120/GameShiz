using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace CasinoShiz.Tests;

[Collection("REST E2E")]
public sealed class RestEndpointFuzzTests(RestE2EFixture fixture)
{
    private const string Root = "/api/v1/tenants/e2e/scopes/42";
    private const string ValidationToken = "e2e-fuzz-validation";
    private const string CommandToken = "e2e-fuzz-commands";
    private const int RestRouteCount = 91;

    [Fact]
    public async Task RestValidationFuzz_ParallelInputsNeverReturnServerErrorsAndKeepEconomyConsistent()
    {
        using var client = fixture.Application.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var cases = GenerateValidationCases();

        for (var offset = 0; offset < cases.Count; offset += 12)
        {
            var batch = cases.Skip(offset).Take(12).ToArray();
            var responses = await Task.WhenAll(batch.Select(testCase => SendAsync(client, testCase)));
            foreach (var response in responses)
            {
                Assert.True(
                    (int)response.StatusCode < 500,
                    $"REST fuzz request returned {(int)response.StatusCode} for {response.Path}: {response.Body}");
                AssertValidJsonOrEmpty(response);
            }
        }

        await AssertEconomyInvariantsAsync();
    }

    [Fact]
    public async Task RestDiceFuzz_ParallelDuplicateCommandsCreateOneMutationPerIdempotencyKey()
    {
        using var client = fixture.Application.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var faces = Gen.Choose(1, 6).Sample(4, new Rnd(0xD1CE_2026UL), 20).ToArray();
        var duplicateCounts = Gen.Choose(2, 8).Sample(4, new Rnd(0xF00D_2026UL), 20).ToArray();
        var keys = faces.Select((_, index) => $"rest-fuzz-dice-{index}-{Guid.NewGuid():N}").ToArray();

        var groups = new Task<FuzzResponse[]>[faces.Length];
        for (var index = 0; index < faces.Length; index++)
        {
            var face = faces[index];
            var key = keys[index];
            groups[index] = Task.WhenAll(
                Enumerable.Range(0, duplicateCounts[index])
                    .Select(_ => SendAsync(client, new FuzzRequest(
                        HttpMethod.Post,
                        $"{Root}/dice/roll",
                        JsonSerializer.Serialize(new { slotValue = face }),
                        key,
                        CommandToken))));
        }

        var responsesByKey = await Task.WhenAll(groups);
        foreach (var responses in responsesByKey)
        {
            Assert.NotEmpty(responses);
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.All(responses, response =>
            {
                Assert.True(
                    response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                    $"Unexpected duplicate-command status {(int)response.StatusCode}: {response.Body}");
                AssertValidJsonOrEmpty(response);
            });
        }

        Assert.Equal(4, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM tenant_idempotency_keys WHERE idempotency_key LIKE 'dice:roll:%:70'"));
        Assert.Equal(4, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM dice_rolls WHERE user_id = 70"));
        Assert.Equal(8, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM economics_ledger WHERE telegram_user_id = 70 AND balance_scope_id = 42"));
        Assert.Equal(4, await fixture.ScalarAsync<long>(
            "SELECT roll_count FROM telegram_dice_daily_rolls WHERE telegram_user_id = 70 AND balance_scope_id = 42 AND game_id = 'dice'"));
        Assert.Equal(0, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM users WHERE telegram_user_id = 70 AND balance_scope_id = 42 AND coins < 0"));
    }

    private static IReadOnlyList<FuzzRequest> GenerateValidationCases()
    {
        var values = Gen.Choose(-500, 1_000)
            .Sample(24, new Rnd(0xBADC_0FFEUL), 100)
            .ToArray();
        var cases = new List<FuzzRequest>(values.Length * 7);

        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            var magnitude = Math.Abs(value);
            var keyPrefix = $"rest-fuzz-validation-{index}";
            cases.Add(new(HttpMethod.Get, $"{Root}/meta/top?limit={value}", null, null, ValidationToken));
            cases.Add(new(HttpMethod.Get, $"{Root}/meta/tournaments/open?limit={value}", null, null, ValidationToken));
            cases.Add(new(
                HttpMethod.Post,
                $"{Root}/meta/clan",
                ClanPayload(index, value),
                $"{keyPrefix}-clan",
                ValidationToken));
            cases.Add(new(
                HttpMethod.Post,
                $"{Root}/meta/tournaments",
                TournamentPayload(index, value),
                $"{keyPrefix}-tournament",
                ValidationToken));
            cases.Add(new(
                HttpMethod.Post,
                $"{Root}/meta/quests/{Uri.EscapeDataString($"unknown-{index}-{value}")}/claim",
                "{}",
                $"{keyPrefix}-quest",
                ValidationToken));
            cases.Add(new(
                HttpMethod.Post,
                $"{Root}/blackjack/start",
                BlackjackPayload(index, magnitude),
                $"{keyPrefix}-blackjack",
                ValidationToken));
            cases.Add(new(
                HttpMethod.Post,
                $"{Root}/poker/tables/{Uri.EscapeDataString($"fuzz-{index}-{value}")}/join",
                index % 3 == 0 ? "{}" : "null",
                $"{keyPrefix}-poker",
                ValidationToken));
        }

        for (var wave = 0; wave < 3; wave++)
            AddAllRestRouteCases(cases, values[wave], wave);

        Assert.Equal(24 * 7 + 3 * RestRouteCount, cases.Count);

        return cases;
    }

    private static void AddAllRestRouteCases(List<FuzzRequest> cases, int value, int wave)
    {
        var tableId = Uri.EscapeDataString($"fuzz-table-{wave}-{value}");
        var unknownTag = Uri.EscapeDataString($"FUZZ{wave}{Math.Abs(value) % 100}");
        var unknownQuest = Uri.EscapeDataString($"fuzz-quest-{wave}-{value}");
        var unknownGuid = Guid.Empty.ToString();
        var unknownLong = long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

        void Add(HttpMethod method, string path, string? json = null)
        {
            var idempotencyKey = method == HttpMethod.Get
                ? null
                : $"rest-fuzz-all-{wave}-{cases.Count}";
            cases.Add(new(method, path, json, idempotencyKey, ValidationToken));
        }

        void AddPost(string path, bool bodyRequired = true) =>
            Add(HttpMethod.Post, path, bodyRequired ? InvalidJson(wave + cases.Count) : null);

        // admin
        AddPost($"{Root}/admin/sync", bodyRequired: false);
        Add(HttpMethod.Get, $"{Root}/admin/users/{unknownLong}");
        AddPost($"{Root}/admin/pay");
        AddPost($"{Root}/admin/clear-bets", bodyRequired: false);
        AddPost($"{Root}/admin/rename");

        // blackjack
        Add(HttpMethod.Get, $"{Root}/blackjack/state");
        AddPost($"{Root}/blackjack/start");
        AddPost($"{Root}/blackjack/hit", bodyRequired: false);
        AddPost($"{Root}/blackjack/stand", bodyRequired: false);
        AddPost($"{Root}/blackjack/double", bodyRequired: false);

        // challenges
        AddPost($"{Root}/challenges");
        AddPost($"{Root}/challenges/{unknownGuid}/accept");
        AddPost($"{Root}/challenges/{unknownGuid}/decline");

        // dice and horse
        AddPost($"{Root}/dice/roll");
        Add(HttpMethod.Get, $"{Root}/horse/info?raceDate=not-a-date-{value}");
        Add(HttpMethod.Get, $"{Root}/horse/result?raceDate=not-a-date-{value}");
        AddPost($"{Root}/horse/bet");

        // leaderboard
        Add(HttpMethod.Get, $"{Root}/leaderboard?limit={value}");

        // meta
        Add(HttpMethod.Get, $"{Root}/meta/season");
        Add(HttpMethod.Get, $"{Root}/meta/profile");
        Add(HttpMethod.Get, $"{Root}/meta/top?limit={value}");
        Add(HttpMethod.Get, $"{Root}/meta/achievements");
        Add(HttpMethod.Get, $"{Root}/meta/streaks");
        Add(HttpMethod.Get, $"{Root}/meta/quests");
        AddPost($"{Root}/meta/quests/{unknownQuest}/claim");
        Add(HttpMethod.Get, $"{Root}/meta/clan");
        Add(HttpMethod.Get, $"{Root}/meta/clan/by-tag/{unknownTag}");
        Add(HttpMethod.Get, $"{Root}/meta/clan/members");
        Add(HttpMethod.Get, $"{Root}/meta/clan/top?limit={value}");
        AddPost($"{Root}/meta/clan");
        AddPost($"{Root}/meta/clan/join");
        Add(HttpMethod.Get, $"{Root}/meta/tournaments/open?limit={value}");
        AddPost($"{Root}/meta/tournaments");
        Add(HttpMethod.Get, $"{Root}/meta/tournaments/{unknownLong}");
        Add(HttpMethod.Get, $"{Root}/meta/tournaments/{unknownLong}/players");
        Add(HttpMethod.Get, $"{Root}/meta/tournaments/{unknownLong}/matches");
        AddPost($"{Root}/meta/tournaments/{unknownLong}/join");
        AddPost($"{Root}/meta/tournaments/{unknownLong}/start", bodyRequired: false);
        AddPost($"{Root}/meta/tournaments/matches/{unknownLong}/report");
        AddPost($"{Root}/meta/tournaments/{unknownLong}/finish");
        Add(HttpMethod.Delete, $"{Root}/meta/tournaments/{unknownLong}");
        Add(HttpMethod.Get, $"{Root}/meta/risk");
        AddPost($"{Root}/meta/risk/{unknownLong}/status");

        // native dice games
        foreach (var game in new[] { "basketball", "bowling", "dicecube", "football" })
        {
            AddPost($"{Root}/{game}/bet");
            AddPost($"{Root}/{game}/play");
            AddPost($"{Root}/{game}/roll");
        }

        AddPost($"{Root}/darts/play");
        AddPost($"{Root}/darts/bet");
        AddPost($"{Root}/darts/rounds/{unknownLong}/throw");

        // pick
        AddPost($"{Root}/pick");
        AddPost($"{Root}/pick/lottery");
        AddPost($"{Root}/pick/lottery/join", bodyRequired: false);
        Add(HttpMethod.Get, $"{Root}/pick/lottery");
        Add(HttpMethod.Delete, $"{Root}/pick/lottery");
        AddPost($"{Root}/pick/daily");
        Add(HttpMethod.Get, $"{Root}/pick/daily");
        Add(HttpMethod.Get, $"{Root}/pick/daily/history?limit={value}");
        Add(HttpMethod.Get, $"{Root}/pick/daily/schedule");
        AddPost($"{Root}/pick/chains/continue");
        AddPost($"{Root}/pick/chains/{unknownGuid}/claim");

        // pixel battle
        Add(HttpMethod.Get, $"{Root}/pixelbattle/grid");
        AddPost($"{Root}/pixelbattle/pixels");

        // poker
        Add(HttpMethod.Get, $"{Root}/poker/tables/me");
        AddPost($"{Root}/poker/tables", bodyRequired: false);
        AddPost($"{Root}/poker/tables/{tableId}/join", bodyRequired: false);
        AddPost($"{Root}/poker/tables/{tableId}/start", bodyRequired: false);
        AddPost($"{Root}/poker/tables/{tableId}/actions");
        Add(HttpMethod.Delete, $"{Root}/poker/tables/{tableId}/players/me");

        // redeem
        AddPost($"{Root}/redeem/begin");
        AddPost($"{Root}/redeem/verify");
        AddPost($"{Root}/redeem/complete");
        AddPost($"{Root}/redeem/issue");

        // Secret Hitler
        Add(HttpMethod.Get, $"{Root}/secrethitler/game");
        AddPost($"{Root}/secrethitler/game");
        AddPost($"{Root}/secrethitler/game/join");
        AddPost($"{Root}/secrethitler/game/start", bodyRequired: false);
        AddPost($"{Root}/secrethitler/game/nominate");
        AddPost($"{Root}/secrethitler/game/vote");
        AddPost($"{Root}/secrethitler/game/discard");
        AddPost($"{Root}/secrethitler/game/enact");
        Add(HttpMethod.Delete, $"{Root}/secrethitler/game");

        // transfer
        AddPost($"{Root}/transfer");
    }

    private static string InvalidJson(int value) => (Math.Abs(value) % 4) switch
    {
        0 => "null",
        1 => "[]",
        2 => "{\"fuzz\":",
        _ => JsonSerializer.Serialize(new { fuzz = value })
    };

    private static string ClanPayload(int index, int value) => (index % 4) switch
    {
        0 => JsonSerializer.Serialize(new { tag = $"FZ{index:00}", name = $"Fuzz clan {index}" }),
        1 => JsonSerializer.Serialize(new { tag = new string('x', Math.Abs(value) % 80), name = "x" }),
        2 => "null",
        _ => "{\"tag\":"
    };

    private static string TournamentPayload(int index, int value) => (index % 4) switch
    {
        0 => JsonSerializer.Serialize(new
        {
            gameKey = index % 2 == 0 ? "dice" : "cube",
            entryFee = value,
            maxPlayers = Math.Abs(value) % 66,
        }),
        1 => JsonSerializer.Serialize(new { gameKey = "blackjack", entryFee = 0, maxPlayers = 2 }),
        2 => "[]",
        _ => "{\"gameKey\":"
    };

    private static string BlackjackPayload(int index, int magnitude) => (index % 3) switch
    {
        0 => JsonSerializer.Serialize(new { bet = -Math.Max(1, magnitude) }),
        1 => "[]",
        _ => "null"
    };

    private static async Task<FuzzResponse> SendAsync(HttpClient client, FuzzRequest testCase)
    {
        using var request = new HttpRequestMessage(testCase.Method, testCase.Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", testCase.BearerToken);
        if (testCase.Json is not null)
            request.Content = new StringContent(testCase.Json, Encoding.UTF8, "application/json");
        if (testCase.IdempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", testCase.IdempotencyKey);

        using var response = await client.SendAsync(request);
        return new FuzzResponse(response.StatusCode, await response.Content.ReadAsStringAsync(), testCase.Path);
    }

    private static void AssertValidJsonOrEmpty(FuzzResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Body))
            return;

        using var document = JsonDocument.Parse(response.Body);
        Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.ValueKind);
    }

    private async Task AssertEconomyInvariantsAsync()
    {
        Assert.Equal(0, await fixture.ScalarAsync<long>("SELECT count(*) FROM users WHERE coins < 0"));
        Assert.Equal(0, await fixture.ScalarAsync<long>("SELECT count(*) FROM economics_ledger WHERE balance_after < 0"));
        Assert.Equal(0, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM tenant_idempotency_keys WHERE response_status >= 500"));
    }

    private sealed record FuzzRequest(
        HttpMethod Method,
        string Path,
        string? Json,
        string? IdempotencyKey,
        string BearerToken);

    private sealed record FuzzResponse(HttpStatusCode StatusCode, string Body, string Path);
}
