using BotFramework.Sdk.Modules;
using BotFramework.Sdk.Modules.Migrations;

namespace Games.Dice.Telegram;

/// <summary>
/// Telegram adapter-only module. It contributes routes and presentation metadata
/// but no backend service, persistence, migrations, or jobs.
/// </summary>
public sealed class DiceTelegramModule : IModule
{
    public string Id => "dice";
    public string DisplayName => "🎰 Слоты";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services) =>
        services.AddHandler<DiceHandler>();

    public IModuleMigrations? GetMigrations() => null;
    public IReadOnlyList<BotCommand> GetBotCommands() => [];
    public IReadOnlyList<LocaleBundle> GetLocales() =>
    [
        new LocaleBundle("ru", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["display_name"] = "Слоты",
            ["err.forwarded"] = "Не обманывай меня! 😠",
            ["err.not_enough_coins"] = "Кажется, у кого-то закончились монеты. 😢\nКрутить барабан стоит {0} монет.",
            ["err.daily_roll_limit"] = "Лимит спинов слотов на сегодня исчерпан ({0}/{1}). Попробуй завтра. 🎰",
            ["result.win"] = "Поздравляю, ты выиграл <i>{0} - {1} (ставка) = <b>{2} монет</b></i>! 🎉",
            ["result.lose"] = "Ой-ой, сегодня удача не на твоей стороне. Ты потерял <i>{0} - {1} (компенсация) = <b>{2} монет</b></i> 💸",
            ["result.balance"] = "Твой баланс: <b>{0} монет</b>",
            ["result.gas"] = "<i>Кстати, за эту операцию сняли ещё {0} монет</i>",
            ["result.daily_roll_remaining"] = "Осталось попыток <b>{0}/{1}</b>",
        }),
    ];
}
