using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Results;

namespace Games.SecretHitler.Discord;

public sealed class SecretHitlerDiscordInteractionHandler(
    ISecretHitlerService service,
    IDiscordComponentTokenStore tokens) : IDiscordInteractionHandler
{
    public IEnumerable<ApplicationCommandProperties> BuildCommands()
    {
        yield return new SlashCommandBuilder()
            .WithName("secret-hitler")
            .WithDescription("Управление Secret Hitler")
            .AddOption(Subcommand("create", "Создать игру"))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("join").WithDescription("Войти в игру").WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("code", ApplicationCommandOptionType.String, "Код игры", isRequired: true, isAutocomplete: true))
            .AddOption(Subcommand("start", "Начать игру"))
            .AddOption(Subcommand("state", "Показать состояние"))
            .AddOption(Subcommand("leave", "Покинуть игру"))
            .Build();
    }

    public bool CanHandle(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand command => string.Equals(command.Data.Name, "secret-hitler", StringComparison.Ordinal),
        SocketAutocompleteInteraction autocomplete => string.Equals(autocomplete.Data.CommandName, "secret-hitler", StringComparison.Ordinal),
        SocketMessageComponent component => tokens.TryResolve(component.Data.CustomId, out var componentToken)
            && componentToken.Action.StartsWith("secret-hitler:", StringComparison.Ordinal),
        _ => false,
    };

    public async Task HandleAsync(DiscordInteractionContext context)
    {
        if (context.Interaction is SocketAutocompleteInteraction autocomplete)
        {
            await autocomplete.RespondAsync(Array.Empty<AutocompleteResult>());
            return;
        }

        var userId = DiscordInteraction.UserId(context);
        object? result;

        if (context.Interaction is SocketSlashCommand command)
        {
            var sub = DiscordInteraction.Subcommand(command);
            result = sub?.Name switch
            {
                "create" => await service.CreateGameAsync(userId, DiscordInteraction.DisplayName(context), DiscordInteraction.ScopeId(context), DiscordInteraction.ScopeId(context), context.CancellationToken),
                "join" => await service.JoinGameAsync(userId, DiscordInteraction.DisplayName(context), DiscordInteraction.ScopeId(context), DiscordInteraction.Value<string>(sub.Options, "code") ?? string.Empty, context.CancellationToken),
                "start" => await service.StartGameAsync(userId, context.CancellationToken),
                "leave" => await service.LeaveAsync(userId, context.CancellationToken),
                _ => (await service.FindMyGameAsync(userId, context.CancellationToken)).Snapshot,
            };
        }
        else
        {
            var component = (SocketMessageComponent)context.Interaction;
            var id = tokens.TryResolve(component.Data.CustomId, out var componentToken) ? componentToken.Action : string.Empty;
            result = id switch
            {
                "secret-hitler:start" => await service.StartGameAsync(userId, context.CancellationToken),
                "secret-hitler:vote:ja" => await service.VoteAsync(userId, ShVote.Ja, context.CancellationToken),
                "secret-hitler:vote:nein" => await service.VoteAsync(userId, ShVote.Nein, context.CancellationToken),
                "secret-hitler:leave" => await service.LeaveAsync(userId, context.CancellationToken),
                "secret-hitler:nominate" => await service.NominateAsync(userId, int.Parse(component.Data.Values.Single(), System.Globalization.CultureInfo.InvariantCulture), context.CancellationToken),
                "secret-hitler:discard" => await service.PresidentDiscardAsync(userId, int.Parse(component.Data.Values.Single(), System.Globalization.CultureInfo.InvariantCulture), context.CancellationToken),
                "secret-hitler:enact" => await service.ChancellorEnactAsync(userId, int.Parse(component.Data.Values.Single(), System.Globalization.CultureInfo.InvariantCulture), context.CancellationToken),
                _ => (await service.FindMyGameAsync(userId, context.CancellationToken)).Snapshot,
            };
        }

        var nominate = new SelectMenuBuilder().WithCustomId(tokens.Issue("secret-hitler:nominate")).WithPlaceholder("Номинировать игрока").WithMinValues(1).WithMaxValues(1);
        for (var i = 1; i <= 10; i++) nominate.AddOption($"Позиция {i}", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var discard = new SelectMenuBuilder().WithCustomId(tokens.Issue("secret-hitler:discard")).WithPlaceholder("Президент: сбросить карту").WithMinValues(1).WithMaxValues(1)
            .AddOption("Карта 0", "0").AddOption("Карта 1", "1").AddOption("Карта 2", "2");
        var enact = new SelectMenuBuilder().WithCustomId(tokens.Issue("secret-hitler:enact")).WithPlaceholder("Канцлер: принять карту").WithMinValues(1).WithMaxValues(1)
            .AddOption("Карта 0", "0").AddOption("Карта 1", "1");
        var controls = new ComponentBuilder()
            .WithButton("Начать", tokens.Issue("secret-hitler:start"), ButtonStyle.Success)
            .WithButton("Ja", tokens.Issue("secret-hitler:vote:ja"), ButtonStyle.Success)
            .WithButton("Nein", tokens.Issue("secret-hitler:vote:nein"), ButtonStyle.Danger)
            .WithButton("Обновить", tokens.Issue("secret-hitler:refresh"), ButtonStyle.Secondary)
            .WithButton("Выйти", tokens.Issue("secret-hitler:leave"), ButtonStyle.Danger)
            .WithSelectMenu(nominate)
            .WithSelectMenu(discard)
            .WithSelectMenu(enact)
            .Build();
        await DiscordInteraction.ReplyResultAsync(context, result, "Secret Hitler", controls, ephemeral: true);
    }

    private static SlashCommandOptionBuilder Subcommand(string name, string description) => new SlashCommandOptionBuilder()
        .WithName(name).WithDescription(description).WithType(ApplicationCommandOptionType.SubCommand);
}
