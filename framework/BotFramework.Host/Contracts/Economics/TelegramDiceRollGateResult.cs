namespace BotFramework.Host.Contracts.Economics;

public readonly record struct TelegramDiceRollGateResult(
    TelegramDiceRollGateStatus Status,
    int UsedToday = 0,
    int Limit = 0);
