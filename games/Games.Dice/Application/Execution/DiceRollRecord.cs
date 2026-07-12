using BotFramework.Sdk.Execution;

namespace Games.Dice.Application.Execution;

public sealed record DiceRollRecord(
    long UserId,
    int DiceValue,
    int Prize,
    int Loss,
    DateTimeOffset RolledAt) : IGameRecord;
