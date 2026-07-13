using BotFramework.Discord.Commands;
using BotFramework.Discord.Routing;
using Discord.WebSocket;
using Games.Transfer.Application.Results;
using Games.Transfer.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Transfer.Discord;

public sealed class TransferDiscordHandler(ITransferService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.TryParse(context, out var c) && c.Is("transfer", "pay");
    public async Task HandleAsync(DiscordMessageContext context)
    {
        if (!DiscordCommand.TryParse(context, out var command) || !command.TryGetPositiveInt(command.Arguments.Count - 1, out var amount))
        { await context.ReplyAsync("Usage: `transfer @user <amount>`"); return; }
        var message = context.Message as SocketUserMessage;
        var target = message?.MentionedUsers.FirstOrDefault(u => !u.IsBot);
        if (target is null) { await context.ReplyAsync("Mention a Discord user to receive the transfer."); return; }
        var from = context.UserId(); var to = checked((long)target.Id);
        var r = await service.TryTransferAsync(from, to, context.ScopeId(), context.DisplayName(), target.GlobalName ?? target.Username, amount, context.SourceMessageId(), context.CancellationToken);
        var text = r.Error switch
        {
            TransferError.None => $"Transferred **{r.NetToRecipient}** to {target.Mention}. Fee: **{r.FeeCoins}**. Your balance: **{r.SenderBalance}**.",
            TransferError.InsufficientFunds => $"Insufficient funds. Required: **{r.TotalDebited}**, balance: **{r.SenderBalance}**.",
            _ => $"Transfer failed: **{r.Error}**.",
        };
        await context.ReplyAsync(text);
    }
}
public static class TransferDiscordExtensions { public static IServiceCollection AddTransferDiscord(this IServiceCollection s) => s.AddScoped<IDiscordMessageHandler, TransferDiscordHandler>(); }
