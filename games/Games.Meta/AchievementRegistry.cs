using BotFramework.Sdk;

namespace Games.Meta;

public static class AchievementRegistry
{
    public static IReadOnlyList<AchievementDefinition> All { get; } =
    [
        new("first_game", "Первый заход", "Сыграть первую игру в сезоне.", "season", true, false),
        new("first_win", "Первая победа", "Выиграть первую игру в сезоне.", "season", true, false),
        new("ten_games", "Разогрев", "Сыграть 10 игр в сезоне.", "season", true, false),
        new("fifty_games", "Гриндер", "Сыграть 50 игр в сезоне.", "season", true, false),
        new("ten_wins", "Победная серия", "Выиграть 10 игр в сезоне.", "season", true, false),
        new("high_roller", "High Roller", "Поставить суммарно 10 000 монет за сезон.", "economy", true, false),
        new("big_payout", "Большой занос", "Получить выплату от 1 000 монет за одну игру.", "economy", true, false),
        new("dice_player", "Слоты крутятся", "Сыграть в слот-машину /dice.", "games", true, false),
        new("cube_player", "Кубовод", "Сыграть в /cube.", "games", true, false),
        new("darts_player", "В яблочко", "Сыграть в /darts.", "games", true, false),
        new("football_player", "Гол-машина", "Сыграть в /football.", "games", true, false),
        new("basketball_player", "Бросок сезона", "Сыграть в /basketball.", "games", true, false),
        new("bowling_player", "Страйк рядом", "Сыграть в /bowling.", "games", true, false),
    ];

    public static IReadOnlyList<AchievementDefinition> Evaluate(GameCompletedMetaEvent ev, SeasonPlayer player)
    {
        var unlocked = new List<AchievementDefinition>();

        AddIf(unlocked, "first_game", player.GamesPlayed >= 1);
        AddIf(unlocked, "first_win", player.Wins >= 1);
        AddIf(unlocked, "ten_games", player.GamesPlayed >= 10);
        AddIf(unlocked, "fifty_games", player.GamesPlayed >= 50);
        AddIf(unlocked, "ten_wins", player.Wins >= 10);
        AddIf(unlocked, "high_roller", player.TotalStaked >= 10_000);
        AddIf(unlocked, "big_payout", ev.Payout >= 1_000);

        AddIf(unlocked, "dice_player", ev.GameKey == MiniGameIds.Dice);
        AddIf(unlocked, "cube_player", ev.GameKey == MiniGameIds.DiceCube);
        AddIf(unlocked, "darts_player", ev.GameKey == MiniGameIds.Darts);
        AddIf(unlocked, "football_player", ev.GameKey == MiniGameIds.Football);
        AddIf(unlocked, "basketball_player", ev.GameKey == MiniGameIds.Basketball);
        AddIf(unlocked, "bowling_player", ev.GameKey == MiniGameIds.Bowling);

        return unlocked;
    }

    private static void AddIf(List<AchievementDefinition> result, string id, bool condition)
    {
        if (!condition) return;
        var achievement = All.First(x => x.Id == id);
        result.Add(achievement);
    }
}
