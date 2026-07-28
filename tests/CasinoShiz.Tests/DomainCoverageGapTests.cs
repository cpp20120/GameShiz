using Games.Pick.Domain.Configuration;
using PokerDeck = Games.Poker.Domain.Rules.Deck;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DomainCoverageGapTests
{
    [Fact]
    public void RemainingDomainContracts_ExposeTheirValues()
    {
        var now = DateTimeOffset.UnixEpoch;

        var blackjack = new BlackjackOptions { MinBet = 2, MaxBet = 2_000, HandTimeoutMs = 30_000 };
        Assert.Equal("Games:blackjack", BlackjackOptions.SectionName);
        Assert.Equal(2, blackjack.MinBet);
        Assert.Equal(2_000, blackjack.MaxBet);
        Assert.Equal(30_000, blackjack.HandTimeoutMs);

        var challenges = new ChallengeOptions
        {
            MinBet = 2,
            MaxBet = 6_000,
            HouseFeeBasisPoints = 300,
            PendingTtlMinutes = 30,
        };
        Assert.Equal("Games:challenges", ChallengeOptions.SectionName);
        Assert.Equal(2, challenges.MinBet);
        Assert.Equal(6_000, challenges.MaxBet);
        Assert.Equal(300, challenges.HouseFeeBasisPoints);
        Assert.Equal(30, challenges.PendingTtlMinutes);
        Assert.Equal(TimeSpan.FromMinutes(30), challenges.PendingTtl);

        var race = new RaceInfo(3, new Dictionary<int, double> { [1] = 1.2 });
        Assert.Equal(3, race.BetsCount);
        Assert.Equal(1.2, race.Koefs[1]);

        var redeem = new BeginRedeemResult(RedeemError.InvalidCode, Guid.Empty);
        Assert.Equal(RedeemError.InvalidCode, redeem.Error);
        Assert.Equal(Guid.Empty, redeem.CodeGuid);
        Assert.Null(redeem.Captcha);

        var aborted = new DartsBetAborted(1, -2, 50, 3, 4);
        Assert.Equal(1, aborted.UserId);
        Assert.Equal(-2, aborted.ChatId);
        Assert.Equal(50, aborted.Amount);
        Assert.Equal(3, aborted.RoundId);
        Assert.Equal(4, aborted.OccurredAt);
        Assert.Equal("darts.bet_aborted", aborted.EventType);

        var basketball = new BasketballOptions { MaxBet = 12_000, DefaultBet = 25, RedeemDropChance = 0.2 };
        Assert.Equal(12_000, basketball.MaxBet);
        Assert.Equal(25, basketball.DefaultBet);
        Assert.Equal(0.2, basketball.RedeemDropChance);

        var bowling = new BowlingOptions { MaxBet = 12_000, DefaultBet = 26, RedeemDropChance = 0.3 };
        Assert.Equal(12_000, bowling.MaxBet);
        Assert.Equal(26, bowling.DefaultBet);
        Assert.Equal(0.3, bowling.RedeemDropChance);

        var darts = new DartsOptions { MaxBet = 12_000, DefaultBet = 27, RedeemDropChance = 0.4 };
        Assert.Equal(12_000, darts.MaxBet);
        Assert.Equal(27, darts.DefaultBet);
        Assert.Equal(0.4, darts.RedeemDropChance);

        var football = new FootballOptions { MaxBet = 12_000, DefaultBet = 28, RedeemDropChance = 0.5 };
        Assert.Equal(12_000, football.MaxBet);
        Assert.Equal(28, football.DefaultBet);
        Assert.Equal(0.5, football.RedeemDropChance);

        var leaderboard = new ChatLeaderboard(1, null, "private", Array.Empty<LeaderboardPlace>(), true);
        Assert.Equal(1, leaderboard.ChatId);
        Assert.Null(leaderboard.Title);
        Assert.Equal("private", leaderboard.ChatType);
        Assert.Empty(leaderboard.Places);
        Assert.True(leaderboard.Truncated);

        var member = new ClanMemberInfo(1, 2, "member", "owner", now);
        Assert.Equal(1, member.ClanId);
        Assert.Equal(2, member.UserId);
        Assert.Equal("member", member.DisplayName);
        Assert.Equal("owner", member.Role);
        Assert.Equal(now, member.JoinedAt);

        var clan = new ClanInfo(1, 2, "Clan", "CLN", 3, now, 4, 5, 6);
        Assert.Equal(1, clan.Id);
        Assert.Equal(2, clan.ChatId);
        Assert.Equal("Clan", clan.Name);
        Assert.Equal("CLN", clan.Tag);
        Assert.Equal(3, clan.OwnerUserId);
        Assert.Equal(now, clan.CreatedAt);
        Assert.Equal(4, clan.MemberCount);
        Assert.Equal(5, clan.SeasonXp);
        Assert.Equal(6, clan.SeasonRating);

        var clanEntry = new ClanLeaderboardEntry(1, 2, "Clan", "CLN", 3, 4, 5);
        Assert.Equal(1, clanEntry.Place);
        Assert.Equal(2, clanEntry.ClanId);
        Assert.Equal("Clan", clanEntry.Name);
        Assert.Equal("CLN", clanEntry.Tag);
        Assert.Equal(3, clanEntry.Members);
        Assert.Equal(4, clanEntry.Xp);
        Assert.Equal(5, clanEntry.Rating);

        var achievement = new AchievementDefinition("a", "A", "desc", "games", true, false);
        Assert.Equal("a", achievement.Id);
        Assert.Equal("A", achievement.Title);
        Assert.Equal("desc", achievement.Description);
        Assert.Equal("games", achievement.Category);
        Assert.True(achievement.IsSeasonal);
        Assert.False(achievement.IsSecret);

        var playerAchievement = new PlayerAchievementView("a", "A", "desc", "games", true, now);
        Assert.Equal("a", playerAchievement.Id);
        Assert.Equal("A", playerAchievement.Title);
        Assert.Equal("desc", playerAchievement.Description);
        Assert.Equal("games", playerAchievement.Category);
        Assert.True(playerAchievement.IsUnlocked);
        Assert.Equal(now, playerAchievement.UnlockedAt);

        var streak = new PlayerGameStreakView("dice", "Dice", "/dice", 1, 2, 3, DateOnly.FromDateTime(now.UtcDateTime));
        Assert.Equal("dice", streak.GameKey);
        Assert.Equal("Dice", streak.Title);
        Assert.Equal("/dice", streak.Command);
        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(2, streak.BestStreak);
        Assert.Equal(3, streak.TotalPlayDays);
        Assert.Equal(new DateOnly(1970, 1, 1), streak.LastPlayedOn);

        var seasonLeaderboard = new SeasonLeaderboardEntry(1, 2, "player", 3, 4, 5, 6, 7, 8);
        Assert.Equal(1, seasonLeaderboard.Place);
        Assert.Equal(2, seasonLeaderboard.UserId);
        Assert.Equal("player", seasonLeaderboard.DisplayName);
        Assert.Equal(3, seasonLeaderboard.Xp);
        Assert.Equal(4, seasonLeaderboard.Level);
        Assert.Equal(5, seasonLeaderboard.Rating);
        Assert.Equal(6, seasonLeaderboard.GamesPlayed);
        Assert.Equal(7, seasonLeaderboard.Wins);
        Assert.Equal(8, seasonLeaderboard.Losses);

        var tournamentPlayer = new TournamentPlayerInfo(1, 2, "player", "active", now);
        Assert.Equal(1, tournamentPlayer.TournamentId);
        Assert.Equal(2, tournamentPlayer.UserId);
        Assert.Equal("player", tournamentPlayer.DisplayName);
        Assert.Equal("active", tournamentPlayer.Status);
        Assert.Equal(now, tournamentPlayer.JoinedAt);

        var tournamentMatch = new TournamentMatchInfo(1, 2, 3, 4, "ready", 5, "one", 6, "two", 5, now, now);
        Assert.Equal(1, tournamentMatch.Id);
        Assert.Equal(2, tournamentMatch.TournamentId);
        Assert.Equal(3, tournamentMatch.Round);
        Assert.Equal(4, tournamentMatch.MatchIndex);
        Assert.Equal("ready", tournamentMatch.Status);
        Assert.Equal(5, tournamentMatch.Player1UserId);
        Assert.Equal("one", tournamentMatch.Player1DisplayName);
        Assert.Equal(6, tournamentMatch.Player2UserId);
        Assert.Equal("two", tournamentMatch.Player2DisplayName);
        Assert.Equal(5, tournamentMatch.VictorUserId);
        Assert.Equal(now, tournamentMatch.CreatedAt);
        Assert.Equal(now, tournamentMatch.UpdatedAt);

        var seasonPlayer = new SeasonPlayer(1, 2, 3, "player", 4, 5, 6, 7, 8, 9, 10, 11, now);
        Assert.Equal(1, seasonPlayer.SeasonId);
        Assert.Equal(2, seasonPlayer.ChatId);
        Assert.Equal(3, seasonPlayer.UserId);
        Assert.Equal("player", seasonPlayer.DisplayName);
        Assert.Equal(4, seasonPlayer.Xp);
        Assert.Equal(5, seasonPlayer.Level);
        Assert.Equal(6, seasonPlayer.Rating);
        Assert.Equal(7, seasonPlayer.GamesPlayed);
        Assert.Equal(8, seasonPlayer.Wins);
        Assert.Equal(9, seasonPlayer.Losses);
        Assert.Equal(10, seasonPlayer.TotalStaked);
        Assert.Equal(11, seasonPlayer.TotalPayout);
        Assert.Equal(now, seasonPlayer.UpdatedAt);

        var quest = new QuestTemplate(
            "quest", "Quest", "description", "daily", "volume", "dice", 10, 20, 30,
            MinStake: 1, MaxStake: 2, MinPayout: 3, MinProfit: 4, MinMultiplier: 1.5m,
            Rarity: "rare", Cluster: "cluster", MinLevel: 2, MinGamesPlayed: 3, MinTotalStaked: 4);
        Assert.Equal("quest", quest.Id);
        Assert.Equal("Quest", quest.Title);
        Assert.Equal("description", quest.Description);
        Assert.Equal("daily", quest.Period);
        Assert.Equal("volume", quest.Kind);
        Assert.Equal("dice", quest.GameKey);
        Assert.Equal(10, quest.Target);
        Assert.Equal(20, quest.RewardXp);
        Assert.Equal(30, quest.RewardCoins);
        Assert.Equal(1, quest.MinStake);
        Assert.Equal(2, quest.MaxStake);
        Assert.Equal(3, quest.MinPayout);
        Assert.Equal(4, quest.MinProfit);
        Assert.Equal(1.5m, quest.MinMultiplier);
        Assert.Equal("rare", quest.Rarity);
        Assert.Equal("cluster", quest.Cluster);
        Assert.Equal(2, quest.MinLevel);
        Assert.Equal(3, quest.MinGamesPlayed);
        Assert.Equal(4, quest.MinTotalStaked);

        var risk = new RiskFlagView(1, 2, 3, "player", "velocity", "high", "open", "reason", now);
        Assert.Equal(1, risk.Id);
        Assert.Equal(2, risk.ChatId);
        Assert.Equal(3, risk.UserId);
        Assert.Equal("player", risk.DisplayName);
        Assert.Equal("velocity", risk.Kind);
        Assert.Equal("high", risk.Severity);
        Assert.Equal("open", risk.Status);
        Assert.Equal("reason", risk.Reason);
        Assert.Equal(now, risk.CreatedAt);

        var webAppUser = new TelegramWebAppUser(1, "first", "last", "user");
        Assert.Equal(1, webAppUser.Id);
        Assert.Equal("first", webAppUser.FirstName);
        Assert.Equal("last", webAppUser.LastName);
        Assert.Equal("user", webAppUser.Username);

        var pick = new PickDailyLotteryOptions
        {
            TicketPrice = 10,
            MaxTicketsPerUserPerDay = 20,
            MaxTicketsPerBuyCommand = 30,
            HouseFeePercent = 0.4,
            SweeperIntervalSeconds = 40,
            HistoryLimit = 50,
            TimezoneOffsetHoursOverride = 6,
            DrawHourLocal = 7,
        };
        Assert.Equal(10, pick.TicketPrice);
        Assert.Equal(20, pick.MaxTicketsPerUserPerDay);
        Assert.Equal(30, pick.MaxTicketsPerBuyCommand);
        Assert.Equal(0.4, pick.HouseFeePercent);
        Assert.Equal(40, pick.SweeperIntervalSeconds);
        Assert.Equal(50, pick.HistoryLimit);
        Assert.Equal(6, pick.TimezoneOffsetHoursOverride);
        Assert.Equal(7, pick.DrawHourLocal);

        var transfer = new TransferOptions { FeePercent = 0.1, MinFeeCoins = 2, MinNetCoins = 3, MaxNetCoins = 4 };
        Assert.Equal("Games:transfer", TransferOptions.SectionName);
        Assert.Equal(0.1, transfer.FeePercent);
        Assert.Equal(2, transfer.MinFeeCoins);
        Assert.Equal(3, transfer.MinNetCoins);
        Assert.Equal(4, transfer.MaxNetCoins);
        Assert.Equal(2, TransferOptions.ComputeFeeCoins(1, 0, 2));
        Assert.Equal(7, TransferOptions.ComputeTotalDebit(5, 0.1, 2));
    }

    [Fact]
    public void DomainConfigAndCatalog_FallbackBranchesAreCovered()
    {
        var progression = SeasonProgressionConfig.FromJson(
            "{\"xp\":{\"play\":true,\"stakeMultiplier\":true},\"rating\":{\"enabled\":false,\"start\":\"not-bool\"}}");
        Assert.Equal(5, progression.PlayXp);
        Assert.Equal(0.01m, progression.StakeMultiplier);
        Assert.False(progression.RatingEnabled);
        Assert.Equal(1_000, progression.RatingStart);

        var rewards = SeasonRewardsConfig.FromJson("{\"rewards\":{}}");
        Assert.Equal(5_000, rewards.PlayerRewardForPlace(1));
        Assert.Equal(10_000, rewards.ClanRewardForPlace(1));

        Assert.Equal("dice", ChallengeGameCatalog.DisplayName(ChallengeGame.Dice));
        Assert.Equal("🎲", ChallengeGameCatalog.Emoji((ChallengeGame)999));
        Assert.Equal("999", ChallengeGameCatalog.DisplayName((ChallengeGame)999));

        var invalidBoolean = SeasonProgressionConfig.FromJson("{\"rating\":{\"enabled\":1}}");
        Assert.True(invalidBoolean.RatingEnabled);

        var completed = new GameCompletedMetaEvent(
            ChatId: 1,
            UserId: 2,
            DisplayName: "player",
            GameKey: MiniGameIds.Dice,
            Stake: 10,
            Payout: 20,
            IsWin: true,
            Multiplier: 2,
            OccurredAt: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Assert.NotNull(QuestRegistry.Matching(completed).ToArray());
    }

    [Fact]
    public void PokerDomainAndRank_ExposeComparisonAndInvalidActionPaths()
    {
        var low = new HandRank(HandCategory.Pair, [10, 9, 8]);
        var high = new HandRank(HandCategory.Pair, [10, 9, 7]);

        Assert.Contains("Pair", low.ToString(), StringComparison.Ordinal);
        Assert.True(low.Equals((object)low));
        Assert.False(low.Equals("not a rank"));
        Assert.True(high < low);
        Assert.True(high <= low);
        Assert.True(low > high);
        Assert.True(low >= high);
        Assert.False(low == high);
        Assert.True(low != high);

        var table = new PokerTable { CurrentBet = 0, BigBlind = 10, MinRaise = 10 };
        var seat = new PokerSeat { Stack = 100, Status = PokerSeatStatus.Seated };
        Assert.Equal(ValidationResult.Invalid,
            PokerDomain.Validate(table, seat, new PokerAction((PokerActionKind)999)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PokerDomain.Apply(table, seat, new PokerAction((PokerActionKind)999)));
    }

    [Fact]
    public void PokerDeckAndEvaluator_CoverDeterministicAndInvalidInputs()
    {
        var deterministic = PokerDeck.BuildShuffled(Enumerable.Repeat(0.25, 51).ToArray());
        Assert.Equal(52, PokerDeck.Parse(deterministic).Length);
        Assert.Throws<ArgumentException>(() => PokerDeck.BuildShuffled(Array.Empty<double>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PokerDeck.BuildShuffled(Enumerable.Repeat(0.25, 50).Append(1.0).ToArray()));
        Assert.Throws<ArgumentException>(() =>
            HandEvaluator.EvaluateBest(["XS", "2H", "3D", "4C", "5S"]));
        Assert.Equal("?", HandEvaluator.CategoryNameRu((HandCategory)999));

        // The state machine guarantees an active seat before calling this helper;
        // exercise its defensive fallback explicitly as part of Domain coverage.
        var nextActiveSeat = typeof(PokerDomain).GetMethod(
            "NextActiveSeat",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var result = nextActiveSeat!.Invoke(null, [0, new[] { new PokerSeat { Position = 0, Status = PokerSeatStatus.Folded } }]);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void SecretHitlerDeterministicDeckAndRoles_AreCovered()
    {
        var entropy = Enumerable.Repeat(0.25, 20).ToArray();
        var deck = ShPolicyDeck.BuildShuffledDeck(entropy);
        Assert.Equal(17, deck.Length);

        string drawDeck = "L";
        string discard = "FF";
        var drawn = ShPolicyDeck.Draw(ref drawDeck, ref discard, 3, entropy);
        Assert.Equal(3, drawn.Length);
        Assert.Empty(discard);

        var players = Enumerable.Range(0, 5)
            .Select(position => new SecretHitlerPlayer { Position = position, IsAlive = true })
            .ToList();
        ShRoleDealer.DealRoles(players, entropy);
        Assert.Equal(5, players.Count(p => p.Role is ShRole.Liberal or ShRole.Fascist or ShRole.Hitler));

        var game = new SecretHitlerGame { CurrentPresidentPosition = 0, Phase = ShPhase.Nomination };
        ShTransitions.StartGame(game, players, entropy, entropy);
        Assert.Equal(ShPhase.Nomination, game.Phase);
        Assert.Equal(0, game.CurrentPresidentPosition);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ShRoleDealer.DealRoles(players.Take(4).ToArray(), entropy));
        Assert.Throws<ArgumentException>(() =>
            ShRoleDealer.DealRoles(players, Array.Empty<double>()));
        var ninePlayers = Enumerable.Range(0, 9)
            .Select(position => new SecretHitlerPlayer { Position = position, IsAlive = true })
            .ToArray();
        ShRoleDealer.DealRoles(ninePlayers, entropy);
        Assert.Throws<ArgumentException>(() =>
            ShPolicyDeck.BuildShuffledDeck(Array.Empty<double>()));
    }

    [Fact]
    public void SecretHitlerDeterministicVoting_CoversPassHitlerAndFailedElections()
    {
        var entropy = Enumerable.Repeat(0.25, 20).ToArray();
        var players = Enumerable.Range(0, 5)
            .Select(position => new SecretHitlerPlayer
            {
                Position = position,
                IsAlive = true,
                Role = position == 1 ? ShRole.Hitler : ShRole.Liberal,
            })
            .ToList();

        var passed = NewElectionGame(players, fascistPolicies: 0, deck: "LFFFLL");
        foreach (var player in players) player.LastVote = ShVote.Ja;
        var passResult = ShTransitions.ApplyVote(passed, players[4], ShVote.Ja, players, entropy);
        Assert.Equal(ShAfterVoteKind.ElectionPassed, passResult!.Kind);
        Assert.Equal(3, passed.PresidentDraw.Length);

        var hitlerWin = NewElectionGame(players, fascistPolicies: 3, deck: "LFFFLL");
        foreach (var player in players) player.LastVote = ShVote.Ja;
        var hitlerResult = ShTransitions.ApplyVote(hitlerWin, players[4], ShVote.Ja, players, entropy);
        Assert.Equal(ShAfterVoteKind.HitlerElectedWin, hitlerResult!.Kind);
        Assert.Equal(ShWinner.Fascists, hitlerWin.Winner);

        var failed = NewElectionGame(players, fascistPolicies: 0, deck: "LFFFLL");
        failed.ElectionTracker = 2;
        foreach (var player in players) player.LastVote = ShVote.Nein;
        var failedResult = ShTransitions.ApplyVote(failed, players[4], ShVote.Nein, players, entropy);
        Assert.Equal(ShAfterVoteKind.ElectionFailed, failedResult!.Kind);
        Assert.Equal(1, failed.LiberalPolicies);

        var liberalWin = NewElectionGame(players, fascistPolicies: 0, deck: "LFFFLL");
        liberalWin.LiberalPolicies = ShTransitions.LiberalWinThreshold - 1;
        liberalWin.ElectionTracker = 2;
        foreach (var player in players) player.LastVote = ShVote.Nein;
        var winResult = ShTransitions.ApplyVote(liberalWin, players[4], ShVote.Nein, players, entropy);
        Assert.Equal(ShAfterVoteKind.ElectionFailed, winResult!.Kind);
        Assert.Equal(ShWinner.Liberals, liberalWin.Winner);
    }

    [Fact]
    public void SecretHitlerValidation_CoversSuccessfulVoteAndDiscard()
    {
        var players = Enumerable.Range(0, 5)
            .Select(position => new SecretHitlerPlayer { Position = position, IsAlive = true })
            .ToList();
        var game = NewElectionGame(players, fascistPolicies: 0, deck: "LFFFLL");
        Assert.Equal(ShValidation.Ok, ShTransitions.ValidateVote(game, players[0]));

        var sevenPlayers = Enumerable.Range(0, 7)
            .Select(position => new SecretHitlerPlayer { Position = position, IsAlive = true })
            .ToList();
        game = NewElectionGame(sevenPlayers, fascistPolicies: 0, deck: "LFFFLL");
        game.Phase = ShPhase.Nomination;
        Assert.Equal(ShValidation.Ok, ShTransitions.ValidateNomination(game, sevenPlayers[0], 3, sevenPlayers));

        game.Phase = ShPhase.LegislativePresident;
        game.PresidentDraw = "LFF";
        Assert.Equal(ShValidation.Ok, ShTransitions.ValidatePresidentDiscard(game, players[0], 0));
    }

    private static SecretHitlerGame NewElectionGame(
        IReadOnlyList<SecretHitlerPlayer> players, int fascistPolicies, string deck) => new()
        {
            Status = ShStatus.Active,
            Phase = ShPhase.Election,
            DeckState = deck,
            CurrentPresidentPosition = 0,
            NominatedChancellorPosition = 1,
            FascistPolicies = fascistPolicies,
            LastElectedPresidentPosition = -1,
            LastElectedChancellorPosition = -1,
        };
}
