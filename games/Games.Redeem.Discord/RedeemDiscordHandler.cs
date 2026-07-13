using System.Collections.Concurrent;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Redeem.Contracts;

namespace Games.Redeem.Discord;

public sealed class RedeemDiscordHandler(IRedeemClient client) : IDiscordMessageHandler
{
    private static readonly ConcurrentDictionary<long, Guid> Pending = new();
    public bool CanHandle(DiscordMessageContext c) => DiscordCommand.Is(c, "redeem");

    public async Task HandleAsync(DiscordMessageContext c)
    {
        var p = DiscordCommand.Parts(c);
        var uid = DiscordCommand.UserId(c);
        var scope = DiscordCommand.ScopeId(c);
        if (p.Length >= 3 && p[1].Equals("captcha", StringComparison.OrdinalIgnoreCase) && int.TryParse(p[2], out var choice) && Pending.TryGetValue(uid, out var codeGuid))
        {
            if (!await client.VerifyCaptchaAsync(uid, codeGuid, choice, c.CancellationToken))
            {
                await DiscordCommand.ReplyAsync(c, "Неверная капча.");
                return;
            }
            var done = await client.CompleteAsync(uid, scope, codeGuid, c.CancellationToken);
            Pending.TryRemove(uid, out _);
            await DiscordCommand.ReplyResultAsync(c, done, "Redeem");
            return;
        }
        if (p.Length != 2)
        {
            await DiscordCommand.ReplyAsync(c, "`redeem <code>` затем `redeem captcha <id>`");
            return;
        }
        var begun = await client.BeginAsync(uid, scope, DiscordCommand.DisplayName(c), p[1], c.CancellationToken);
        if (begun.Error != RedeemClientError.None)
        {
            await DiscordCommand.ReplyResultAsync(c, begun, "Redeem");
            return;
        }
        if (begun.Captcha is null)
        {
            var done = await client.CompleteAsync(uid, scope, begun.CodeGuid, c.CancellationToken);
            await DiscordCommand.ReplyResultAsync(c, done, "Redeem");
            return;
        }
        Pending[uid] = begun.CodeGuid;
        var options = string.Join("\n", begun.Captcha.Items.Select(x => $"`{x.Data}` — {x.Text}"));
        await DiscordCommand.ReplyAsync(c, $"**{begun.Captcha.Pattern}**\n{options}\nОтвет: `redeem captcha <id>`");
    }
}

public sealed class RedeemDiscordInteractionHandler(IRedeemClient client) : IDiscordInteractionHandler
{
    public IEnumerable<ApplicationCommandProperties> BuildCommands()
    {
        yield return new SlashCommandBuilder()
            .WithName("redeem")
            .WithDescription("Активировать промокод")
            .AddOption("code", ApplicationCommandOptionType.String, "Промокод", isRequired: true)
            .Build();
    }

    public bool CanHandle(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand command => command.Data.Name == "redeem",
        SocketMessageComponent component => component.Data.CustomId.StartsWith("redeem:captcha:", StringComparison.Ordinal),
        _ => false,
    };

    public async Task HandleAsync(DiscordInteractionContext context)
    {
        var userId = DiscordInteraction.UserId(context);
        var scopeId = DiscordInteraction.ScopeId(context);
        if (context.Interaction is SocketMessageComponent component)
        {
            var codeGuid = Guid.Parse(component.Data.CustomId["redeem:captcha:".Length..]);
            var choice = int.Parse(component.Data.Values.Single(), System.Globalization.CultureInfo.InvariantCulture);
            if (!await client.VerifyCaptchaAsync(userId, codeGuid, choice, context.CancellationToken))
            {
                await DiscordInteraction.ReplyAsync(context, "Неверная капча.", ephemeral: true);
                return;
            }
            var completed = await client.CompleteAsync(userId, scopeId, codeGuid, context.CancellationToken);
            await DiscordInteraction.ReplyResultAsync(context, completed, "Redeem", ephemeral: true);
            return;
        }

        var command = (SocketSlashCommand)context.Interaction;
        var code = DiscordInteraction.Value<string>(command.Data.Options, "code") ?? string.Empty;
        var begun = await client.BeginAsync(userId, scopeId, DiscordInteraction.DisplayName(context), code, context.CancellationToken);
        if (begun.Error != RedeemClientError.None)
        {
            await DiscordInteraction.ReplyResultAsync(context, begun, "Redeem", ephemeral: true);
            return;
        }
        if (begun.Captcha is null)
        {
            var completed = await client.CompleteAsync(userId, scopeId, begun.CodeGuid, context.CancellationToken);
            await DiscordInteraction.ReplyResultAsync(context, completed, "Redeem", ephemeral: true);
            return;
        }

        var select = new SelectMenuBuilder()
            .WithCustomId($"redeem:captcha:{begun.CodeGuid:D}")
            .WithPlaceholder("Выбери правильный ответ")
            .WithMinValues(1)
            .WithMaxValues(1);
        foreach (var item in begun.Captcha.Items)
            select.AddOption(item.Text, item.Data.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var components = new ComponentBuilder().WithSelectMenu(select).Build();
        await DiscordInteraction.ReplyAsync(context, $"**{begun.Captcha.Pattern}**", components, ephemeral: true);
    }
}

public static class RedeemDiscordModule
{
    public static IServiceCollection AddRedeemDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, RedeemDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, RedeemDiscordInteractionHandler>();
}
