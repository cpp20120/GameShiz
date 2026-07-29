namespace Games.Fun.Telegram;

public sealed class FunTelegramModule : IModule
{
    public string Id => "fun";
    public string DisplayName => "🎲 Фан-команды";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) => services
        .BindOptions<FunOptions>(FunOptions.SectionName)
        .AddSingleton<IRandomSource, CryptoRandomSource>()
        .AddSingleton<FunService>()
        .AddHandler<FunHandler>();

    public IModuleMigrations? GetMigrations() => null;

    public IReadOnlyList<BotCommand> GetBotCommands() =>
    [
        new BotCommand("/roll", "fun.cmd.roll"),
        new BotCommand("/choose", "fun.cmd.choose"),
        new BotCommand("/ben", "fun.cmd.ben"),
    ];

    public IReadOnlyList<LocaleBundle> GetLocales() =>
    [
        new LocaleBundle("ru", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["display_name"] = "Фан-команды",
            ["cmd.roll"] = "Случайный процент или прогноз",
            ["cmd.choose"] = "Случайно выбрать вариант",
            ["cmd.ben"] = "Отправить Talking Ben",
            ["roll.percent"] = "{0}%",
            ["roll.question"] = "<b>{0}</b>\n{1}% — примерно {2} случай из {3}.\n{4}",
            ["roll.band.veryunlikely"] = "Шансы невелики.",
            ["roll.band.unlikely"] = "Скорее нет, чем да.",
            ["roll.band.uncertain"] = "Пятьдесят на пятьдесят.",
            ["roll.band.likely"] = "Похоже, да.",
            ["roll.band.verylikely"] = "Почти наверняка.",
            ["choose.usage"] = "Использование: /choose вариант 1, вариант 2",
            ["choose.too_few"] = "Нужно минимум 2 варианта, разделённых запятыми или переводами строк.",
            ["choose.too_many"] = "Слишком много вариантов: максимум 50.",
            ["choose.too_long"] = "Каждый вариант должен содержать не более 50 символов.",
            ["choose.empty_option"] = "Между разделителями не должно быть пустых вариантов.",
            ["choose.result"] = "🎯 Выбираю: <b>{0}</b>",
            ["ben.not_configured"] = "Talking Ben пока не настроен: укажи 2 основных и 3 редких GIF в Games:fun.",
            ["ben.failed"] = "Не получилось отправить Talking Ben. Проверь источники GIF.",
            ["ben.caption"] = "Talking Ben 🐶",
        }),
    ];
}
