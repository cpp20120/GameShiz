namespace BotFramework.Discord;

public static class DiscordLocalization
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Text =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ru"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["casino.title"] = "CasinoShiz",
                ["casino.menu.description"] = "Выбери раздел. Slash-команды покажут доступные параметры автоматически.",
                ["casino.menu.placeholder"] = "Выбери раздел",
                ["casino.games"] = "Игры: `/blackjack`, `/poker`, `/secret-hitler`, `/pick`, `/horse`, `/pixelbattle`.",
                ["casino.economy"] = "Экономика: `/profile balance`, `/profile daily`, `/transfer`, `/redeem`.",
                ["casino.social"] = "Социальное: `/challenge`, `/profile top`, `/profile global-top`.",
                ["casino.admin"] = "Администрирование: `/casino-admin` (только для разрешённых пользователей и ролей).",
                ["casino.fallback"] = "Используй slash-команды — Discord покажет доступные параметры автоматически.",
                ["casino.games.label"] = "Игры",
                ["casino.games.hint"] = "Карточные, PvP и мини-игры",
                ["casino.economy.label"] = "Экономика",
                ["casino.economy.hint"] = "Баланс, бонусы, переводы и промокоды",
                ["casino.social.label"] = "Социальное",
                ["casino.social.hint"] = "Челленджи и рейтинги",
                ["casino.admin.label"] = "Администрирование",
                ["casino.admin.hint"] = "Защищённые admin-команды",
                ["component.stale"] = "Эта кнопка устарела после перезапуска бота. Открой меню заново.",
                ["interaction.unhandled"] = "Эта Discord-команда пока не поддерживается.",
                ["rate.limited"] = "Слишком быстро. Повтори через {0} сек.",
                ["data.empty"] = "Нет данных.",
                ["modal.code.title"] = "Активировать промокод",
                ["modal.code.label"] = "Промокод",
                ["modal.code.placeholder"] = "Введи код",
                ["modal.bet.title"] = "Новая ставка",
                ["modal.bet.label"] = "Ставка",
                ["modal.bet.placeholder"] = "Например, 100",
                ["modal.raise.title"] = "Повышение ставки",
                ["modal.raise.label"] = "Сумма повышения",
                ["modal.raise.placeholder"] = "Например, 50",
                ["modal.unknown"] = "Эта форма устарела. Открой её заново.",
                ["modal.bet.invalid"] = "Введите положительную ставку.",
                ["modal.raise.invalid"] = "Введите положительную сумму повышения.",
                ["button.code"] = "Ввести код",
                ["button.bet"] = "Новая ставка",
                ["button.raise"] = "Повысить вручную",
            },
            ["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["casino.title"] = "CasinoShiz",
                ["casino.menu.description"] = "Choose a section. Slash commands show available parameters automatically.",
                ["casino.menu.placeholder"] = "Choose a section",
                ["casino.games"] = "Games: `/blackjack`, `/poker`, `/secret-hitler`, `/pick`, `/horse`, `/pixelbattle`.",
                ["casino.economy"] = "Economy: `/profile balance`, `/profile daily`, `/transfer`, `/redeem`.",
                ["casino.social"] = "Social: `/challenge`, `/profile top`, `/profile global-top`.",
                ["casino.admin"] = "Administration: `/casino-admin` (allowlisted users and roles only).",
                ["casino.fallback"] = "Use slash commands — Discord will show available parameters automatically.",
                ["casino.games.label"] = "Games",
                ["casino.games.hint"] = "Card, PvP and mini games",
                ["casino.economy.label"] = "Economy",
                ["casino.economy.hint"] = "Balance, bonuses, transfers and promo codes",
                ["casino.social.label"] = "Social",
                ["casino.social.hint"] = "Challenges and leaderboards",
                ["casino.admin.label"] = "Administration",
                ["casino.admin.hint"] = "Protected admin commands",
                ["component.stale"] = "This button expired when the bot restarted. Open the menu again.",
                ["interaction.unhandled"] = "This Discord command is not supported yet.",
                ["rate.limited"] = "You're going too fast. Try again in {0} sec.",
                ["data.empty"] = "No data.",
                ["modal.code.title"] = "Redeem promo code",
                ["modal.code.label"] = "Promo code",
                ["modal.code.placeholder"] = "Enter a code",
                ["modal.bet.title"] = "New bet",
                ["modal.bet.label"] = "Bet",
                ["modal.bet.placeholder"] = "For example, 100",
                ["modal.raise.title"] = "Raise amount",
                ["modal.raise.label"] = "Raise amount",
                ["modal.raise.placeholder"] = "For example, 50",
                ["modal.unknown"] = "This form expired. Open it again.",
                ["modal.bet.invalid"] = "Enter a positive bet.",
                ["modal.raise.invalid"] = "Enter a positive raise amount.",
                ["button.code"] = "Enter code",
                ["button.bet"] = "New bet",
                ["button.raise"] = "Custom raise",
            },
        };

    public static string Normalize(string? culture, string fallback = "ru")
    {
        if (!string.IsNullOrWhiteSpace(culture) && culture.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return "en";

        if (!string.IsNullOrWhiteSpace(culture) && culture.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
            return "ru";

        return NormalizeFallback(fallback);
    }

    public static string Get(string key, string? culture = null, string fallback = "ru")
    {
        var normalized = Normalize(culture, fallback);
        if (Text.TryGetValue(normalized, out var dictionary) && dictionary.TryGetValue(key, out var value))
            return value;

        return Text["ru"].TryGetValue(key, out var russian) ? russian : key;
    }

    public static string Format(string key, string culture, params object[] arguments) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(key, culture), arguments);

    private static string NormalizeFallback(string fallback) =>
        fallback.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";
}
