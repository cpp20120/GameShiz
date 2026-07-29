using System.Globalization;
using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Parsing;

public static class ChatSettingsCommandParser
{
    public static bool TryParse(
        string text,
        out string? key,
        out string? value,
        out string? error)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 1)
        {
            key = value = error = null;
            return true;
        }
        if (tokens.Length != 3)
        {
            key = value = null;
            error = "Использование: /settings <captcha|automod|flood|links|mentions|forwarded|newmember|commandspam|logchat> <on|off|значение>.";
            return false;
        }
        key = tokens[1].ToLowerInvariant();
        value = tokens[2].ToLowerInvariant();
        if (key is not ("captcha" or "automod" or "flood" or "links" or "mentions" or "forwarded" or "newmember" or "commandspam" or "logchat"))
        {
            error = "Неизвестная настройка. Доступно: captcha, automod, flood, links, mentions, forwarded, newmember, commandspam, logchat.";
            return false;
        }
        if (key is ("captcha" or "automod" or "forwarded" or "newmember") && value is not ("on" or "off"))
        {
            error = "Для этой настройки используйте on или off.";
            return false;
        }
        if (string.Equals(key, "flood", StringComparison.Ordinal)
            && (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit <= 0 || limit > 1000))
        {
            error = "Лимит flood должен быть целым числом от 1 до 1000.";
            return false;
        }
        if (string.Equals(key, "links", StringComparison.Ordinal) && value is not ("allow" or "deny"))
        {
            error = "Для links используйте allow или deny.";
            return false;
        }
        if (key is ("mentions" or "commandspam")
            && (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var spamLimit) || spamLimit <= 0 || spamLimit > 1000))
        {
            error = "Лимит должен быть целым числом от 1 до 1000.";
            return false;
        }
        if (string.Equals(key, "logchat", StringComparison.Ordinal)
            && !string.Equals(value, "off", StringComparison.Ordinal)
            && !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            error = "Для logchat укажите числовой chat ID или off.";
            return false;
        }
        error = null;
        return true;
    }
}
