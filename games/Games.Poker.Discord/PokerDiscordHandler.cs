using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Poker.Application.Services;

namespace Games.Poker.Discord;

public sealed class PokerDiscordHandler(IPokerService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext c) => DiscordCommand.Is(c, "poker");

    public async Task HandleAsync(DiscordMessageContext c)
    {
        var p = DiscordCommand.Parts(c);
        var uid = DiscordCommand.UserId(c);
        var scope = DiscordCommand.ScopeId(c);
        object? r;
        if (p.Length < 2)
        {
            var x = await service.FindMyTableAsync(uid, scope, c.CancellationToken);
            r = x.Snapshot;
        }
        else switch (p[1].ToLowerInvariant())
        {
            case "create": r = await service.CreateTableAsync(uid, DiscordCommand.DisplayName(c), scope, DiscordCommand.SourceId(c), c.CancellationToken); break;
            case "join" when p.Length > 2: r = await service.JoinTableAsync(uid, DiscordCommand.DisplayName(c), scope, p[2], DiscordCommand.SourceId(c), c.CancellationToken); break;
            case "start": r = await service.StartHandAsync(uid, scope, c.CancellationToken); break;
            case "check": case "call": case "fold": r = await service.ApplyPlayerActionAsync(uid, scope, p[1], 0, c.CancellationToken); break;
            case "raise" when p.Length > 2 && int.TryParse(p[2], out var amount): r = await service.ApplyPlayerActionAsync(uid, scope, "raise", amount, c.CancellationToken); break;
            case "leave": r = await service.LeaveTableAsync(uid, scope, c.CancellationToken); break;
            case "state": var x = await service.FindMyTableAsync(uid, scope, c.CancellationToken); r = x.Snapshot; break;
            default:
                await DiscordCommand.ReplyAsync(c, "`poker create|join <code>|start|state|check|call|fold|raise <amount>|leave`");
                return;
        }
        await DiscordCommand.ReplyResultAsync(c, r, "Poker");
    }
}

public sealed class PokerDiscordInteractionHandler(
    IPokerService service,
    IDiscordComponentTokenStore tokens) : IDiscordInteractionHandler
{
    public IEnumerable<ApplicationCommandProperties> BuildCommands()
    {
        yield return new SlashCommandBuilder()
            .WithName("poker")
            .WithDescription("Управление Poker")
            .AddOption(Subcommand("create", "Создать стол"))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("join").WithDescription("Войти за стол").WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("code", ApplicationCommandOptionType.String, "Код стола", isRequired: true, isAutocomplete: true))
            .AddOption(Subcommand("start", "Начать раздачу"))
            .AddOption(Subcommand("state", "Показать стол"))
            .AddOption(Subcommand("leave", "Выйти из-за стола"))
            .Build();
    }

    public bool CanHandle(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand command => string.Equals(command.Data.Name, "poker", StringComparison.Ordinal),
        SocketAutocompleteInteraction autocomplete => string.Equals(autocomplete.Data.CommandName, "poker", StringComparison.Ordinal),
        SocketMessageComponent component => tokens.TryResolve(component.Data.CustomId, out var componentToken)
            && componentToken.Action.StartsWith("poker:", StringComparison.Ordinal),
        SocketModal modal => tokens.TryResolve(modal.Data.CustomId, out var modalToken)
            && modalToken.Action.StartsWith("poker:", StringComparison.Ordinal),
        _ => false,
    };

    public async Task HandleAsync(DiscordInteractionContext context)
    {
        if (context.Interaction is SocketAutocompleteInteraction autocomplete)
        {
            if (!string.Equals(autocomplete.Data.Current?.Name, "code", StringComparison.OrdinalIgnoreCase))
            {
                await autocomplete.RespondAsync(Array.Empty<AutocompleteResult>());
                return;
            }

            await autocomplete.RespondAsync(
                Array.Empty<AutocompleteResult>());
            return;
        }

        var userId = DiscordInteraction.UserId(context);
        var scopeId = DiscordInteraction.ScopeId(context);
        object? result;

        if (context.Interaction is SocketModal modal)
        {
            var modalAction = tokens.TryResolve(modal.Data.CustomId, out var modalToken) ? modalToken.Action : string.Empty;
            if (!string.Equals(modalAction, "poker:raise", StringComparison.Ordinal))
            {
                await DiscordInteraction.ReplyAsync(context, DiscordLocalization.Get("modal.unknown", context.CultureCode), ephemeral: true);
                return;
            }

            if (!int.TryParse(DiscordInteraction.ModalValue(modal, "amount"), out var amount) || amount <= 0)
            {
                await DiscordInteraction.ReplyAsync(context, DiscordLocalization.Get("modal.raise.invalid", context.CultureCode), ephemeral: true);
                return;
            }

            result = await service.ApplyPlayerActionAsync(userId, scopeId, "raise", amount, context.CancellationToken);
            await ReplyWithControlsAsync(context, result);
            return;
        }

        if (context.Interaction is SocketSlashCommand command)
        {
            var sub = DiscordInteraction.Subcommand(command);
            result = sub?.Name switch
            {
                "create" => await service.CreateTableAsync(userId, DiscordInteraction.DisplayName(context), scopeId, DiscordInteraction.SourceId(context), context.CancellationToken),
                "join" => await service.JoinTableAsync(userId, DiscordInteraction.DisplayName(context), scopeId, DiscordInteraction.Value<string>(sub.Options, "code") ?? string.Empty, DiscordInteraction.SourceId(context), context.CancellationToken),
                "start" => await service.StartHandAsync(userId, scopeId, context.CancellationToken),
                "leave" => await service.LeaveTableAsync(userId, scopeId, context.CancellationToken),
                _ => (await service.FindMyTableAsync(userId, scopeId, context.CancellationToken)).Snapshot,
            };
        }
        else
        {
            var component = (SocketMessageComponent)context.Interaction;
            var customId = tokens.TryResolve(component.Data.CustomId, out var componentToken) ? componentToken.Action : string.Empty;
            if (customId == "poker:refresh")
                result = (await service.FindMyTableAsync(userId, scopeId, context.CancellationToken)).Snapshot;
            else if (customId == "poker:start")
                result = await service.StartHandAsync(userId, scopeId, context.CancellationToken);
            else if (customId == "poker:leave")
                result = await service.LeaveTableAsync(userId, scopeId, context.CancellationToken);
            else if (customId == "poker:raise")
            {
                var amount = int.Parse(component.Data.Values.Single(), System.Globalization.CultureInfo.InvariantCulture);
                result = await service.ApplyPlayerActionAsync(userId, scopeId, "raise", amount, context.CancellationToken);
            }
            else if (customId == "poker:raise-modal")
            {
                await context.Interaction.RespondWithModalAsync(DiscordInteraction.TextModal(
                    tokens.Issue("poker:raise"),
                    DiscordLocalization.Get("modal.raise.title", context.CultureCode),
                    "amount",
                    DiscordLocalization.Get("modal.raise.label", context.CultureCode),
                    DiscordLocalization.Get("modal.raise.placeholder", context.CultureCode),
                    maxLength: 9));
                return;
            }
            else
            {
                var action = customId["poker:".Length..];
                result = await service.ApplyPlayerActionAsync(userId, scopeId, action, 0, context.CancellationToken);
            }
        }

        await ReplyWithControlsAsync(context, result);
    }

    private async Task ReplyWithControlsAsync(DiscordInteractionContext context, object? result)
    {
        var raise = new SelectMenuBuilder()
            .WithCustomId(tokens.Issue("poker:raise"))
            .WithPlaceholder("Размер повышения")
            .WithMinValues(1).WithMaxValues(1)
            .AddOption("10", "10")
            .AddOption("25", "25")
            .AddOption("50", "50")
            .AddOption("100", "100");
        var controls = new ComponentBuilder()
            .WithButton("Check", tokens.Issue("poker:check"), ButtonStyle.Secondary)
            .WithButton("Call", tokens.Issue("poker:call"), ButtonStyle.Primary)
            .WithButton("Fold", tokens.Issue("poker:fold"), ButtonStyle.Danger)
            .WithButton("Начать", tokens.Issue("poker:start"), ButtonStyle.Success)
            .WithButton("Обновить", tokens.Issue("poker:refresh"), ButtonStyle.Secondary)
            .WithSelectMenu(raise)
            .WithButton("Выйти", tokens.Issue("poker:leave"), ButtonStyle.Danger, row: 2)
            .WithButton(DiscordLocalization.Get("button.raise", context.CultureCode), tokens.Issue("poker:raise-modal"), ButtonStyle.Primary, row: 2)
            .Build();
        await DiscordInteraction.ReplyResultAsync(context, result, "Poker", controls, ephemeral: true);
    }

    private static SlashCommandOptionBuilder Subcommand(string name, string description) => new SlashCommandOptionBuilder()
        .WithName(name)
        .WithDescription(description)
        .WithType(ApplicationCommandOptionType.SubCommand);
}

public static class PokerDiscordModule
{
    public static IServiceCollection AddPokerDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, PokerDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, PokerDiscordInteractionHandler>();
}
