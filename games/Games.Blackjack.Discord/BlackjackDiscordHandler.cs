using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Blackjack.Contracts;
using Games.Blackjack.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Blackjack.Discord;

public sealed class BlackjackDiscordHandler(IBlackjackClient client) : IDiscordMessageHandler, IDiscordInteractionHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.TryParse(context, out var c) && c.Is("blackjack", "bj");
    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var c) || !c.TryGetPositiveInt(0, out var bet)) { await context.ReplyAsync("Usage: `blackjack <bet>`"); return; }
        var r = await client.StartAsync(context.UserId(), context.DisplayName(), context.ScopeId(), bet, context.Message.Id.ToString(), context.CancellationToken);
        await context.Message.Channel.SendMessageAsync(Format(r), components: Components(r.Snapshot));
    }
    public bool CanHandle(DiscordInteractionContext context) => context.Interaction is SocketMessageComponent c && c.Data.CustomId.StartsWith("bj:", StringComparison.Ordinal);
    public async Task HandleAsync(DiscordInteractionContext context)
    {
        var c = (SocketMessageComponent)context.Interaction; var uid = checked((long)c.User.Id);
        var r = c.Data.CustomId switch { "bj:hit" => await client.HitAsync(uid, context.CancellationToken), "bj:stand" => await client.StandAsync(uid, context.CancellationToken), "bj:double" => await client.DoubleAsync(uid, context.CancellationToken), _ => new BlackjackResult(BlackjackError.NoActiveHand, null) };
        await c.UpdateAsync(p => { p.Content = Format(r); p.Components = Components(r.Snapshot); });
    }
    private static MessageComponent? Components(BlackjackSnapshot? s) => s is null || s.Outcome is not null ? null : new ComponentBuilder().WithButton("Hit", "bj:hit", ButtonStyle.Primary).WithButton("Stand", "bj:stand", ButtonStyle.Secondary).WithButton("Double", "bj:double", ButtonStyle.Success, disabled: !s.CanDouble).Build();
    private static string Format(BlackjackResult r)
    {
        if (r.Error != BlackjackError.None || r.Snapshot is null) return $"Blackjack: **{r.Error}**";
        var s=r.Snapshot; var dealer=s.DealerHoleRevealed?string.Join(' ',s.DealerCards):$"{s.DealerCards.FirstOrDefault()} ??";
        return $"🃏 Player: {string.Join(' ',s.PlayerCards)} (**{s.PlayerTotal}**)\nDealer: {dealer}" + (s.DealerHoleRevealed?$" (**{s.DealerTotal}**)":"") + $"\nBet: **{s.Bet}**, balance: **{s.PlayerCoins}**" + (s.Outcome is null?"":$"\nResult: **{s.Outcome}**, payout: **{s.Payout}**");
    }
}
public static class BlackjackDiscordExtensions { public static IServiceCollection AddBlackjackDiscord(this IServiceCollection s) => s.AddScoped<IDiscordMessageHandler, BlackjackDiscordHandler>().AddScoped<IDiscordInteractionHandler, BlackjackDiscordHandler>(); }
