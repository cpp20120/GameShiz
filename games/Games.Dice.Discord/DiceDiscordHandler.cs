using System.Globalization;
using System.Security.Cryptography;
using BotFramework.Contracts.Messaging;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Games.Dice.Contracts.Play;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Dice.Discord;

public sealed class DiceDiscordHandler(IDiceClient client) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) =>
        DiscordCommand.TryParse(context, out var command) && command.Is("slot", "slots");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var command)) return;
        if (command.Arguments.Count > 0 && !string.Equals(command.Arguments[0], "spin", StringComparison.OrdinalIgnoreCase))
        {
            await context.ReplyAsync("Usage: `slot` or `slot spin`");
            return;
        }

        var face = RandomNumberGenerator.GetInt32(1, 65);
        var userId = context.UserId();
        var scopeId = context.ScopeId();
        var request = new DicePlayRequest(userId, context.DisplayName(), face, scopeId,
            context.Message.Id.ToString(CultureInfo.InvariantCulture), false);
        var metadata = RequestMetadata.Create("discord", userId.ToString(CultureInfo.InvariantCulture),
            scopeId.ToString(CultureInfo.InvariantCulture), "en");
        var result = await client.PlayAsync(request, metadata, context.CancellationToken);

        var text = result.Status switch
        {
            DicePlayStatus.NotEnoughCoins => $"Not enough coins. Stake: **{result.Stake}**.",
            DicePlayStatus.DailyRollLimitExceeded => $"Daily slot limit reached ({result.DailyRollsUsed}/{result.DailyRollLimit}).",
            DicePlayStatus.Forwarded => "Forwarded Discord messages cannot be used for games.",
            _ => FormatPlayed(face, result),
        };
        await context.ReplyAsync(text);
    }

    private static string FormatPlayed(int face, DicePlayResponse result)
    {
        var net = result.Prize - result.Stake;
        var outcome = net > 0 ? $"won **{net}**" : net < 0 ? $"lost **{-net}**" : "broke even";
        return $"🎰 roll **{face}** — {outcome}\nStake: **{result.Stake}**, prize: **{result.Prize}**\nBalance: **{result.Balance}**";
    }
}

public static class DiceDiscordServiceCollectionExtensions
{
    public static IServiceCollection AddDiceDiscord(this IServiceCollection services) =>
        services.AddScoped<IDiscordMessageHandler, DiceDiscordHandler>();
}
