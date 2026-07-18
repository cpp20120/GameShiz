namespace Games.Meta.Telegram;

public sealed class MetaTelegramModule : IModule
{
    public string Id => "meta";
    public string DisplayName => "⭐ Мета";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) => services
        .AddHandler<MetaMenuHandler>()
        .AddHandler<MetaHandler>()
        .AddHandler<TournamentHandler>()
        .AddHandler<RiskHandler>()
        .AddHandler<MyStatsHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() =>
    [
        new BotCommand("/menu", "meta.cmd.menu"),
        new BotCommand("/season", "meta.cmd.season"),
        new BotCommand("/profile", "meta.cmd.profile"),
        new BotCommand("/rank", "meta.cmd.rank"),
        new BotCommand("/topseason", "meta.cmd.topseason"),
        new BotCommand("/achievements", "meta.cmd.achievements"),
        new BotCommand("/streaks", "meta.cmd.streaks"),
        new BotCommand("/quests", "meta.cmd.quests"),
        new BotCommand("/quest", "meta.cmd.quest"),
        new BotCommand("/clan", "meta.cmd.clan"),
        new BotCommand("/tournament", "meta.cmd.tournament"),
        new BotCommand("/tour", "meta.cmd.tour"),
        new BotCommand("/risk", "meta.cmd.risk"),
        new BotCommand("/mystats", "meta.cmd.mystats"),
    ];
    public IReadOnlyList<LocaleBundle> GetLocales() =>
    [
        new LocaleBundle("ru", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["display_name"] = "Мета",
            ["cmd.menu"] = "Интерактивное меню и профиль",
            ["cmd.season"] = "Текущий сезон",
            ["cmd.profile"] = "Профиль сезона",
            ["cmd.rank"] = "Ранг сезона",
            ["cmd.topseason"] = "Сезонный топ",
            ["cmd.achievements"] = "Ачивки сезона",
            ["cmd.streaks"] = "Стрики по играм",
            ["cmd.quests"] = "Квесты сезона",
            ["cmd.quest"] = "Забрать награду за квест",
            ["cmd.clan"] = "Кланы сезона",
            ["cmd.tournament"] = "Турниры сезона",
            ["cmd.tour"] = "Турниры сезона",
            ["cmd.risk"] = "Risk control",
            ["cmd.mystats"] = "Моя статистика и лимиты",
        }),
    ];
}
