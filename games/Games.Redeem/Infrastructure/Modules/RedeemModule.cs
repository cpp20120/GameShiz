
namespace Games.Redeem.Infrastructure.Modules;

using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Redeem.Application.Execution;

public sealed class RedeemModule : IModule
{
    public string Id => "redeem";
    public string DisplayName => "🎟 Redeem";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services)
    {
        services
            .BindOptions<RedeemOptions>(RedeemOptions.SectionName)
            .AddScoped<IGameAction<RedeemIssueCommand, RedeemExecutionState, Guid>, RedeemIssueAction>()
            .AddScoped<GameExecutionDescriptor<RedeemIssueCommand, RedeemExecutionState, Guid>, RedeemIssueDescriptor>()
            .AddScoped<IGameStateStore<RedeemIssueCommand, RedeemExecutionState>, RedeemIssueStateStore>()
            .AddScoped<IGameAction<RedeemCompleteCommand, RedeemExecutionState, CompleteRedeemResult>, RedeemCompleteAction>()
            .AddScoped<GameExecutionDescriptor<RedeemCompleteCommand, RedeemExecutionState, CompleteRedeemResult>, RedeemCompleteDescriptor>()
            .AddScoped<IGameStateStore<RedeemCompleteCommand, RedeemExecutionState>, RedeemCompleteStateStore>()
            .AddScoped<IRedeemService, RedeemService>()
            .AddScoped<IRedeemClient, LocalRedeemClient>()
            .AddScoped<IRedeemStore, RedeemStore>()
            .AddDomainEventSubscription<RedeemDropSubscriber>("minigame.redeem_code_drop_requested")
            .AddDomainEventSubscription<DiscordRedeemDropSubscriber>("minigame.redeem_code_drop_requested");
    }

    public IModuleMigrations GetMigrations() => new RedeemMigrations();

    public IReadOnlyList<BotCommand> GetBotCommands() =>
    [
        new BotCommand("/redeem", "redeem.cmd.redeem"),
    ];

    public IReadOnlyList<LocaleBundle> GetLocales() =>
    [
        new LocaleBundle("ru", new Dictionary<string, string>
(StringComparer.Ordinal) {
            ["display_name"] = "Redeem",
            ["cmd.redeem"] = "Активировать код",

            ["captcha.prompt"] = "<b>{0}</b>\nВыбери снизу самый подходящий смайлик",
            ["captcha.wrong"] = "Увы, неверно. Сделайте /redeem снова",
            ["captcha.timeout"] = "Вы отвечали слишком долго, используйте /redeem снова",

            ["redeem.success"] = "🎉 Код активирован! Тебе доступен ещё один спин 🎰",
            ["drop.message"] = "🎟 Выпал фриспин!\nСкопируй это в личку боту:\n<code>/redeem {0}</code>",

            ["err.only_private"] = "Активировать код можно только в личных сообщениях 😄",
            ["err.invalid_code"] = "Код недействителен",
            ["err.already_redeemed"] = "Сорри, этот код уже кто-то успел активировать 🤯",
            ["err.self_redeem"] = "Упс, а вот свой код обналичить нельзя 🥲",
            ["err.no_user"] = "Похоже, ты ещё не зарегистрирован. Сделай /start",
            ["err.lost_race"] = "Ойй 😬, кажется пока кто-то решал капчу — кодик уже уплыл! 😜",
        }),
    ];
}
