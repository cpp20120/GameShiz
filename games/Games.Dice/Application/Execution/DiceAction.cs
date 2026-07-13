using BotFramework.Sdk.Execution;

namespace Games.Dice.Application.Execution;

public sealed class DiceAction : IGameAction<DiceCommand, NoGameState, DicePlayResult>
{
    public const string DailyRollQuota = "dice.daily-roll";
    public const string RedeemDropEntropy = "redeem-drop";

    private static readonly string[] Stickers = ["bar", "cherry", "lemon", "seven"];
    private static readonly int[] StakePrice = [1, 1, 2, 3];

    public GameDecision<NoGameState, DicePlayResult> Decide(
        GameActionInput<NoGameState, DiceCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var command = input.Command;
        if (command.IsForwarded)
            return Reject(new DicePlayResult(DiceOutcome.Forwarded), "forwarded");

        if (!input.Quotas.TryGetValue(DailyRollQuota, out var quota))
            throw new InvalidOperationException($"Required quota '{DailyRollQuota}' was not supplied.");

        if (quota.Limit > 0 && quota.Used >= quota.Limit)
        {
            return Reject(
                new DicePlayResult(
                    DiceOutcome.DailyRollLimitExceeded,
                    DailyDiceUsed: checked((int)quota.Used),
                    DailyDiceLimit: checked((int)quota.Limit)),
                "daily_roll_limit",
                new DiceRollRejected(
                    command.ChatId,
                    command.UserId,
                    command.DiceValue,
                    "daily_roll_limit",
                    0,
                    input.UtcNow.ToUnixTimeMilliseconds()));
        }

        var gas = GetGasTax(command.Cost);
        var loss = checked(command.Cost + gas);
        if (input.Wallet.Balance < loss)
        {
            return Reject(
                new DicePlayResult(DiceOutcome.NotEnoughCoins, Loss: loss),
                "insufficient_balance",
                new DiceRollRejected(
                    command.ChatId,
                    command.UserId,
                    command.DiceValue,
                    "insufficient_balance",
                    loss,
                    input.UtcNow.ToUnixTimeMilliseconds()));
        }

        var rolls = DecodeRolls(command.DiceValue);
        var (maxFrequent, maxFrequency) = GetMaxFrequency(rolls);
        var prize = GetPrize(maxFrequent, maxFrequency, rolls);
        var rolledAt = input.UtcNow;
        var economy = prize > 0
            ? new[] { EconomyEffect.Debit(loss, "dice.stake"), EconomyEffect.Credit(prize, "dice.prize") }
            : [EconomyEffect.Debit(loss, "dice.stake")];

        var domainEvents = new List<IDomainEvent>
        {
            new DiceRollCompleted(command.UserId, command.DiceValue, prize, loss, rolledAt.ToUnixTimeMilliseconds()),
            new GameCompletedMetaEvent(
                command.ChatId,
                command.UserId,
                command.DisplayName,
                MiniGameIds.Dice,
                loss,
                prize,
                prize > loss,
                loss > 0 ? decimal.Divide(prize, loss) : 0m,
                rolledAt.ToUnixTimeMilliseconds()),
        };

        if (command.RedeemDropChance > 0 &&
            input.Entropy.GetDouble(RedeemDropEntropy) < command.RedeemDropChance)
        {
            domainEvents.Add(new TelegramMiniGameRedeemCodeDropRequested(
                command.UserId,
                command.ChatId,
                MiniGameIds.Dice,
                rolledAt.ToUnixTimeMilliseconds()));
        }

        return new GameDecision<NoGameState, DicePlayResult>(
            DecisionStatus.Accepted,
            input.State,
            new DicePlayResult(
                DiceOutcome.Played,
                prize,
                loss,
                checked((int)(input.Wallet.Balance - loss + prize)),
                gas,
                quota.Limit > 0 ? checked((int)quota.Used + 1) : 0,
                checked((int)quota.Limit)),
            economy,
            quota.Limit > 0 ? [QuotaEffect.Consume(DailyRollQuota)] : [],
            [new DiceRollRecord(command.UserId, command.DiceValue, prize, loss, rolledAt)],
            domainEvents,
            []);
    }

    private static GameDecision<NoGameState, DicePlayResult> Reject(
        DicePlayResult result,
        string reason,
        params IDomainEvent[] events) =>
        new(DecisionStatus.Rejected, default, result, [], [], [], events, [], reason);

    private static int[] DecodeRolls(int value)
    {
        if (value is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Telegram slot value must be between 1 and 64.");

        return
        [
            ((value - 1) >> 0) & 0b11,
            ((value - 1) >> 2) & 0b11,
            ((value - 1) >> 4) & 0b11,
        ];
    }

    private static (int MaxFrequent, int MaxFrequency) GetMaxFrequency(int[] rolls)
    {
        var map = new Dictionary<int, int>();
        foreach (var item in rolls)
            map[item] = map.GetValueOrDefault(item) + 1;

        var maxFrequency = map.Values.Max();
        return (map.First(pair => pair.Value == maxFrequency).Key, maxFrequency);
    }

    private static int GetPrize(int maxFrequent, int maxFrequency, int[] rolls)
    {
        var sticker = Stickers[maxFrequent];
        var rollsSum = rolls.Sum(value => StakePrice[value]);

        return (sticker, maxFrequency) switch
        {
            ("seven", 3) => 77,
            ("lemon", 3) => 30,
            ("cherry", 3) => 23,
            ("bar", 3) => 21,
            ("seven", 2) => 10 + rollsSum,
            ("lemon", 2) => 6 + rollsSum,
            (_, 2) => 4 + rollsSum,
            _ => rollsSum - 3,
        };
    }

    private static int GetGasTax(int tradeVolume)
    {
        const double gasDefault = 0.0285;
        var gasModifier = Math.Sqrt(2);
        var gas = tradeVolume < 10
            ? Math.Max(1, (Math.Pow(tradeVolume + 1, Math.Log10(tradeVolume + 1)) - 1) / 39.15) * gasModifier
            : tradeVolume * gasDefault * gasModifier;
        return (int)Math.Round(gas, MidpointRounding.ToEven);
    }
}
