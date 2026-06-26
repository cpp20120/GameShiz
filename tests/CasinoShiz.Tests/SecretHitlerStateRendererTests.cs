using Xunit;

namespace CasinoShiz.Tests;

public sealed class SecretHitlerStateRendererTests
{
    private static readonly ILocalizer Loc = new EchoLocalizer();

    [Fact]
    public void RenderBoard_Election_IncludesTracksPlayersAndVoteCount()
    {
        var game = Game(ShPhase.Election);
        game.LiberalPolicies = 2;
        game.FascistPolicies = 3;
        game.ElectionTracker = 1;
        game.CurrentPresidentPosition = 0;
        game.NominatedChancellorPosition = 2;
        var players = Players();
        players[0].LastVote = ShVote.Ja;
        players[2].LastVote = ShVote.Nein;
        players[3].IsAlive = false;

        var board = SecretHitlerStateRenderer.RenderBoard(game, players, Loc);

        Assert.Contains("ABCD", board, StringComparison.Ordinal);
        Assert.Contains("🟦🟦", board, StringComparison.Ordinal);
        Assert.Contains("🟥🟥🟥", board, StringComparison.Ordinal);
        Assert.Contains("⚠️", board, StringComparison.Ordinal);
        Assert.Contains("Alice", board, StringComparison.Ordinal);
        Assert.Contains("Cleo", board, StringComparison.Ordinal);
        Assert.Contains("2", board, StringComparison.Ordinal);
        Assert.Contains("3", board, StringComparison.Ordinal);
        Assert.Contains("💀", board, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBoard_GameEnd_IncludesEndSummaryInsteadOfPlayerList()
    {
        var game = Game(ShPhase.GameEnd);
        game.Winner = ShWinner.Liberals;
        game.WinReason = ShWinReason.HitlerExecuted;

        var board = SecretHitlerStateRenderer.RenderBoard(game, Players(), Loc);

        Assert.Contains("end.liberals_win", board, StringComparison.Ordinal);
        Assert.Contains("end.reason.hitler_executed", board, StringComparison.Ordinal);
        Assert.Contains("role.hitler", board, StringComparison.Ordinal);
        Assert.DoesNotContain("board.players", board, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderRoleCard_Fascist_ListsFascistAndHitlerAllies()
    {
        var players = Players();
        var me = players[1];

        var card = SecretHitlerStateRenderer.RenderRoleCard(me, players, playerCount: players.Count, Loc);

        Assert.Contains("role.fascist", card, StringComparison.Ordinal);
        Assert.Contains("role.your_allies", card, StringComparison.Ordinal);
        Assert.Contains("Cleo", card, StringComparison.Ordinal);
        Assert.Contains("role.hitler_short", card, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderRoleCard_HitlerInSmallGame_ListsFascists()
    {
        var players = Players();
        var me = players[2];

        var card = SecretHitlerStateRenderer.RenderRoleCard(me, players, playerCount: 6, Loc);

        Assert.Contains("role.hitler", card, StringComparison.Ordinal);
        Assert.Contains("role.your_fascists", card, StringComparison.Ordinal);
        Assert.Contains("Bob", card, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildBoardMarkup_Nomination_ReturnsEligibleChancellorButtons()
    {
        var game = Game(ShPhase.Nomination);
        game.CurrentPresidentPosition = 0;
        game.LastElectedPresidentPosition = 1;
        game.LastElectedChancellorPosition = 2;
        var players = Players();

        var markup = SecretHitlerStateRenderer.BuildBoardMarkup(game, players[0], players, Loc);
        var buttons = markup?.InlineKeyboard.SelectMany(row => row).ToArray();

        Assert.NotNull(buttons);
        Assert.DoesNotContain(buttons, b => string.Equals(b.CallbackData, "sh:nominate:0", StringComparison.Ordinal));
        Assert.DoesNotContain(buttons, b => string.Equals(b.CallbackData, "sh:nominate:2", StringComparison.Ordinal));
        Assert.Contains(buttons, b => string.Equals(b.CallbackData, "sh:nominate:1", StringComparison.Ordinal));
        Assert.Contains(buttons, b => string.Equals(b.CallbackData, "sh:nominate:3", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildBoardMarkup_Election_AllowsAliveUnvotedViewerToVote()
    {
        var game = Game(ShPhase.Election);

        var markup = SecretHitlerStateRenderer.BuildBoardMarkup(game, Players()[0], Players(), Loc);
        Assert.NotNull(markup);
        var callbacks = markup.InlineKeyboard.SelectMany(row => row).Select(b => b.CallbackData!).ToArray();

        Assert.Equal(["sh:vote:ja", "sh:vote:nein"], callbacks);
    }

    [Fact]
    public void BuildBoardMarkup_Election_ReturnsNullForAlreadyVotedViewer()
    {
        var players = Players();
        players[0].LastVote = ShVote.Ja;

        var markup = SecretHitlerStateRenderer.BuildBoardMarkup(Game(ShPhase.Election), players[0], players, Loc);

        Assert.Null(markup);
    }

    [Fact]
    public void BuildBoardMarkup_LegislativePresident_ReturnsDiscardButtons()
    {
        var game = Game(ShPhase.LegislativePresident);
        game.CurrentPresidentPosition = 0;
        game.PresidentDraw = "LFF";
        var players = Players();

        var markup = SecretHitlerStateRenderer.BuildBoardMarkup(game, players[0], players, Loc);
        Assert.NotNull(markup);
        var callbacks = markup.InlineKeyboard.Single().Select(b => b.CallbackData!).ToArray();

        Assert.Equal(["sh:discard:0", "sh:discard:1", "sh:discard:2"], callbacks);
    }

    [Fact]
    public void BuildBoardMarkup_LegislativeChancellor_ReturnsEnactButtons()
    {
        var game = Game(ShPhase.LegislativeChancellor);
        game.NominatedChancellorPosition = 2;
        game.ChancellorReceived = "LF";
        var players = Players();

        var markup = SecretHitlerStateRenderer.BuildBoardMarkup(game, players[2], players, Loc);
        Assert.NotNull(markup);
        var callbacks = markup.InlineKeyboard.Single().Select(b => b.CallbackData!).ToArray();

        Assert.Equal(["sh:enact:0", "sh:enact:1"], callbacks);
    }

    [Fact]
    public void BuildPublicMarkup_LobbyAndElection_ReturnsExpectedCallbacks()
    {
        var lobby = SecretHitlerStateRenderer.BuildPublicMarkup(Game(ShPhase.None), Players(), Loc);
        var electionGame = Game(ShPhase.Election);
        electionGame.Status = ShStatus.Active;
        var election = SecretHitlerStateRenderer.BuildPublicMarkup(electionGame, Players(), Loc);

        Assert.NotNull(lobby);
        Assert.NotNull(election);
        Assert.Equal(["sh:join:ABCD", "sh:start"], lobby.InlineKeyboard.Single().Select(b => b.CallbackData!).ToArray());
        Assert.Equal(["sh:vote:ja", "sh:vote:nein"], election.InlineKeyboard.Single().Select(b => b.CallbackData!).ToArray());
    }

    [Theory]
    [InlineData(ShWinner.Fascists, ShWinReason.FascistPolicies, "end.fascists_win", "end.reason.fascist_policies")]
    [InlineData(ShWinner.Fascists, ShWinReason.HitlerElected, "end.fascists_win", "end.reason.hitler_elected")]
    [InlineData(ShWinner.Liberals, ShWinReason.LiberalPolicies, "end.liberals_win", "end.reason.liberal_policies")]
    [InlineData(ShWinner.None, ShWinReason.None, "end.generic", "<i></i>")]
    public void RenderEndSummary_MapsWinnerAndReason(
        ShWinner winner,
        ShWinReason reason,
        string expectedWinner,
        string expectedReason)
    {
        var game = Game(ShPhase.GameEnd);
        game.Winner = winner;
        game.WinReason = reason;

        var summary = SecretHitlerStateRenderer.RenderEndSummary(game, Players(), Loc);

        Assert.Contains(expectedWinner, summary, StringComparison.Ordinal);
        Assert.Contains(expectedReason, summary, StringComparison.Ordinal);
        Assert.Contains("end.roles_header", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderVoteReveal_ListsOnlyAlivePlayersWithVoteMarks()
    {
        var players = Players();
        players[0].LastVote = ShVote.Ja;
        players[1].LastVote = ShVote.Nein;
        players[2].LastVote = ShVote.None;
        players[3].IsAlive = false;

        var reveal = SecretHitlerStateRenderer.RenderVoteReveal(players, Loc);

        Assert.Contains("vote.ja", reveal, StringComparison.Ordinal);
        Assert.Contains("vote.nein", reveal, StringComparison.Ordinal);
        Assert.Contains("—", reveal, StringComparison.Ordinal);
        Assert.DoesNotContain("Dora", reveal, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ShPolicy.Liberal, "policy.liberal")]
    [InlineData(ShPolicy.Fascist, "policy.fascist")]
    public void PolicyLabel_MapsPolicies(ShPolicy policy, string expected)
    {
        Assert.Equal(expected, SecretHitlerStateRenderer.PolicyLabel(policy, Loc));
    }

    [Theory]
    [InlineData(ShRole.Liberal, "role.liberal")]
    [InlineData(ShRole.Fascist, "role.fascist")]
    [InlineData(ShRole.Hitler, "role.hitler")]
    public void RoleLabel_MapsRoles(ShRole role, string expected)
    {
        Assert.Equal(expected, SecretHitlerStateRenderer.RoleLabel(role, Loc));
    }

    private static SecretHitlerGame Game(ShPhase phase) => new()
    {
        InviteCode = "ABCD",
        Status = phase == ShPhase.None ? ShStatus.Lobby : ShStatus.Active,
        Phase = phase,
        CurrentPresidentPosition = 0,
        NominatedChancellorPosition = -1,
        Pot = 120,
    };

    private static List<SecretHitlerPlayer> Players() =>
    [
        new() { InviteCode = "ABCD", Position = 0, UserId = 10, DisplayName = "Alice", Role = ShRole.Liberal },
        new() { InviteCode = "ABCD", Position = 1, UserId = 11, DisplayName = "Bob", Role = ShRole.Fascist },
        new() { InviteCode = "ABCD", Position = 2, UserId = 12, DisplayName = "Cleo", Role = ShRole.Hitler },
        new() { InviteCode = "ABCD", Position = 3, UserId = 13, DisplayName = "Dora", Role = ShRole.Liberal },
    ];

    private sealed class EchoLocalizer : ILocalizer
    {
        private static readonly Dictionary<string, string> Templates = new(StringComparer.Ordinal)
        {
            ["board.header"] = "{0} pot {1}",
            ["board.liberals"] = "{0} {1}/{2}",
            ["board.fascists"] = "{0} {1}/{2}",
            ["board.election_tracker"] = "{0}",
            ["board.phase"] = "{0}",
            ["board.president"] = "{0}",
            ["board.chancellor"] = "{0}",
            ["board.votes"] = "{0}/{1}",
            ["role.your_role"] = "{0}",
            ["btn.discard"] = "{0} {1}",
            ["btn.enact"] = "{0} {1}",
        };

        public string Get(string moduleId, string key, string cultureCode = "ru") =>
            Templates.GetValueOrDefault(key, key);

        public string GetPlural(string moduleId, string key, int count, string cultureCode = "ru") => key;
    }
}
