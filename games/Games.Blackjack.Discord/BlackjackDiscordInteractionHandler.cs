using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Blackjack.Contracts;

namespace Games.Blackjack.Discord;

public sealed class BlackjackDiscordInteractionHandler(
    IBlackjackClient client,
    IDiscordComponentTokenStore tokens) : IDiscordInteractionHandler
{
    private static readonly int[] AutocompleteAmounts = [10, 25, 50, 100, 250, 500];

    public IEnumerable<ApplicationCommandProperties> BuildCommands()
    {
        yield return new SlashCommandBuilder()
            .WithName("blackjack")
            .WithDescription("Играть в Blackjack")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("start")
                .WithDescription("Начать новую раздачу")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("bet", ApplicationCommandOptionType.Integer, "Ставка", isRequired: false, isAutocomplete: true, minValue: 1))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("state")
                .WithDescription("Показать текущую раздачу")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .Build();
    }

    public bool CanHandle(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand command => string.Equals(command.Data.Name, "blackjack", StringComparison.Ordinal),
        SocketAutocompleteInteraction autocomplete => string.Equals(autocomplete.Data.CommandName, "blackjack", StringComparison.Ordinal),
        SocketMessageComponent component => tokens.TryResolve(component.Data.CustomId, out var componentToken)
            && componentToken.Action.StartsWith("blackjack:", StringComparison.Ordinal),
        SocketModal modal => tokens.TryResolve(modal.Data.CustomId, out var modalToken)
            && modalToken.Action.StartsWith("blackjack:", StringComparison.Ordinal),
        _ => false,
    };

    public async Task HandleAsync(DiscordInteractionContext context)
    {
        if (context.Interaction is SocketAutocompleteInteraction autocomplete)
        {
            var choices = AutocompleteAmounts
                .Select(amount => new AutocompleteResult(amount.ToString(System.Globalization.CultureInfo.InvariantCulture), amount));
            await autocomplete.RespondAsync(choices);
            return;
        }

        var userId = DiscordInteraction.UserId(context);
        object result;

        if (context.Interaction is SocketModal modal)
        {
            var action = tokens.TryResolve(modal.Data.CustomId, out var modalToken) ? modalToken.Action : string.Empty;
            if (!string.Equals(action, "blackjack:bet", StringComparison.Ordinal))
            {
                await DiscordInteraction.ReplyAsync(context, DiscordLocalization.Get("modal.unknown", context.CultureCode), ephemeral: true);
                return;
            }

            if (!int.TryParse(DiscordInteraction.ModalValue(modal, "bet"), System.Globalization.CultureInfo.InvariantCulture, out var modalBet) || modalBet <= 0)
            {
                await DiscordInteraction.ReplyAsync(context, DiscordLocalization.Get("modal.bet.invalid", context.CultureCode), ephemeral: true);
                return;
            }

            result = await client.StartAsync(
                userId,
                DiscordInteraction.DisplayName(context),
                DiscordInteraction.ScopeId(context),
                modalBet,
                context.Interaction.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                context.CancellationToken);
            await DiscordInteraction.ReplyResultAsync(context, result, "Blackjack", ephemeral: true);
            return;
        }

        if (context.Interaction is SocketSlashCommand command)
        {
            var subcommand = DiscordInteraction.Subcommand(command);
            if (subcommand is not null && string.Equals(subcommand.Name, "start", StringComparison.Ordinal))
            {
                var betOption = subcommand.Options.FirstOrDefault(option => string.Equals(option.Name, "bet", StringComparison.Ordinal));
                if (betOption?.Value is null)
                {
                    await context.Interaction.RespondWithModalAsync(DiscordInteraction.TextModal(
                        tokens.Issue("blackjack:bet"),
                        DiscordLocalization.Get("modal.bet.title", context.CultureCode),
                        "bet",
                        DiscordLocalization.Get("modal.bet.label", context.CultureCode),
                        DiscordLocalization.Get("modal.bet.placeholder", context.CultureCode),
                        maxLength: 9));
                    return;
                }

                var bet = DiscordInteraction.Value<long>(subcommand.Options, "bet");
                result = await client.StartAsync(
                    userId,
                    DiscordInteraction.DisplayName(context),
                    DiscordInteraction.ScopeId(context),
                    checked((int)bet),
                    context.Interaction.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    context.CancellationToken);
            }
            else
            {
                result = await client.GetStateAsync(userId, context.CancellationToken);
            }
        }
        else
        {
            var component = (SocketMessageComponent)context.Interaction;
            var action = tokens.TryResolve(component.Data.CustomId, out var componentToken) ? componentToken.Action : string.Empty;
            if (string.Equals(action, "blackjack:bet-modal", StringComparison.Ordinal))
            {
                await context.Interaction.RespondWithModalAsync(DiscordInteraction.TextModal(
                    tokens.Issue("blackjack:bet"),
                    DiscordLocalization.Get("modal.bet.title", context.CultureCode),
                    "bet",
                    DiscordLocalization.Get("modal.bet.label", context.CultureCode),
                    DiscordLocalization.Get("modal.bet.placeholder", context.CultureCode),
                    maxLength: 9));
                return;
            }

            result = action switch
            {
                "blackjack:hit" => await client.HitAsync(userId, context.CancellationToken),
                "blackjack:stand" => await client.StandAsync(userId, context.CancellationToken),
                "blackjack:double" => await client.DoubleAsync(userId, context.CancellationToken),
                _ => await client.GetStateAsync(userId, context.CancellationToken),
            };
        }

        var controls = new ComponentBuilder()
            .WithButton("Ещё карту", tokens.Issue("blackjack:hit"), ButtonStyle.Primary, new Emoji("🃏"))
            .WithButton("Хватит", tokens.Issue("blackjack:stand"), ButtonStyle.Success, new Emoji("✋"))
            .WithButton("Удвоить", tokens.Issue("blackjack:double"), ButtonStyle.Secondary, new Emoji("💸"))
            .WithButton("Обновить", tokens.Issue("blackjack:state"), ButtonStyle.Secondary, new Emoji("🔄"))
            .WithButton(DiscordLocalization.Get("button.bet", context.CultureCode), tokens.Issue("blackjack:bet-modal"), ButtonStyle.Primary, new Emoji("🎲"), row: 1)
            .Build();
        await DiscordInteraction.ReplyResultAsync(context, result, "Blackjack", controls, ephemeral: true);
    }
}
