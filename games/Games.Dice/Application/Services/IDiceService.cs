// ─────────────────────────────────────────────────────────────────────────────
// DiceService — application service for the 🎰 slots roll.
//
// Ported from src/CasinoShiz.Core/Services/Dice/DiceService.cs, minus the
// per-user attempts counter and bank-tax windowing. The core gameplay — decode
// Telegram's encoded dice value, pick a sticker triple, compute prize from the
// published payout table — ships verbatim.
//
// Stateless by design: no aggregate, no repository. Each Telegram 🎰 message is
// one durable economics operation, keyed by chat/message/user so retries do not
// debit or credit twice.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using BotFramework.Sdk;

namespace Games.Dice;

public interface IDiceService
{
    Task<DicePlayResult> PlayAsync(
        long userId,
        string displayName,
        int diceValue,
        long chatId,
        int sourceMessageId,
        bool isForwarded,
        CancellationToken ct);
}
