using ChatAdministration.Application.Services;
using ChatAdministration.Telegram.Infrastructure;
using ChatAdministration.Telegram.Presentation;
using BotFramework.Sdk.Modules;
using BotFramework.Sdk.Modules.Migrations;

namespace ChatAdministration.Telegram.Composition;

public sealed class ChatAdministrationTelegramModule : IModule
{
    public string Id => "chat_administration";
    public string DisplayName => "🛡 Chat administration";
    public string Version => "0.1.0";

    public void ConfigureServices(IModuleServiceCollection services) => services
        .AddSingleton<IChatAdministrationStore, ChatAdministrationStore>()
        .AddSingleton<TelegramEffectExecutor>()
        .AddBackgroundJob<ModerationEffectWorker>()
        .AddBackgroundJob<VerificationExpirationJob>()
        .AddBackgroundJob<WarningExpirationJob>()
        .AddBackgroundJob<BotPermissionsReconciliationJob>()
        .AddBackgroundJob<RestrictionsReconciliationJob>()
        .AddBackgroundJob<RetentionCleanupJob>()
        .AddScoped<ModerationCommandService>()
        .AddScoped<ChatMetadataService>()
        .AddScoped<ITargetResolver, TelegramTargetResolver>()
        .AddScoped<VerificationService>()
        .AddScoped<MemberLifecycleService>()
        .AddScoped<LifecycleSettingsService>()
        .AddScoped<ModerationAnalyticsService>()
        .AddScoped<ModerationRuleService>()
        .AddScoped<CustomRoleService>()
        .AddScoped<WarningService>()
        .AddScoped<PurgeService>()
        .AddScoped<RoleService>()
        .AddScoped<CaseService>()
        .AddScoped<AppealService>()
        .AddHandler<ModerationTelegramHandler>()
        .AddHandler<ChatMetadataTelegramHandler>()
        .AddHandler<ManualModerationTelegramHandler>()
        .AddHandler<AutomoderationTelegramHandler>()
        .AddHandler<MemberLifecycleTelegramHandler>()
        .AddHandler<RulesTelegramHandler>()
        .AddHandler<LifecycleSettingsTelegramHandler>()
        .AddHandler<SettingsCallbackTelegramHandler>()
        .AddHandler<ModerationAnalyticsTelegramHandler>()
        .AddHandler<CaptchaCallbackTelegramHandler>()
        .AddHandler<WarningTelegramHandler>()
        .AddHandler<PurgeTelegramHandler>()
        .AddHandler<RoleTelegramHandler>()
        .AddHandler<CaseTelegramHandler>()
        .AddHandler<AppealTelegramHandler>()
        .AddHandler<ChatSettingsTelegramHandler>()
        .AddSingleton<IModerationRuleProvider, DefaultModerationRuleProvider>()
        .AddSingleton<IModerationRateLimitStore, RedisModerationRateLimitStore>();

    public IModuleMigrations GetMigrations() => new ChatAdministrationMigrations();

    public IReadOnlyList<BotCommand> GetBotCommands() =>
    [
        new BotCommand("/mute", "chat_administration.cmd.mute"),
        new BotCommand("/warn", "chat_administration.cmd.warn"),
        new BotCommand("/unmute", "chat_administration.cmd.unmute"),
        new BotCommand("/ban", "chat_administration.cmd.ban"),
        new BotCommand("/unban", "chat_administration.cmd.unban"),
        new BotCommand("/kick", "chat_administration.cmd.kick"),
        new BotCommand("/warnings", "chat_administration.cmd.warnings"),
        new BotCommand("/unwarn", "chat_administration.cmd.unwarn"),
        new BotCommand("/clearwarnings", "chat_administration.cmd.clearwarnings"),
        new BotCommand("/purge", "chat_administration.cmd.purge"),
        new BotCommand("/role", "chat_administration.cmd.role"),
        new BotCommand("/roles", "chat_administration.cmd.roles"),
        new BotCommand("/cases", "chat_administration.cmd.cases"),
        new BotCommand("/case", "chat_administration.cmd.case"),
        new BotCommand("/revoke", "chat_administration.cmd.revoke"),
        new BotCommand("/appeal", "chat_administration.cmd.appeal"),
        new BotCommand("/approveappeal", "chat_administration.cmd.approveappeal"),
        new BotCommand("/rejectappeal", "chat_administration.cmd.rejectappeal"),
        new BotCommand("/settings", "chat_administration.cmd.settings"),
        new BotCommand("/rules", "chat_administration.cmd.rules"),
        new BotCommand("/rule", "chat_administration.cmd.rule"),
        new BotCommand("/welcome", "chat_administration.cmd.welcome"),
        new BotCommand("/goodbye", "chat_administration.cmd.goodbye"),
        new BotCommand("/setwelcome", "chat_administration.cmd.setwelcome"),
        new BotCommand("/setgoodbye", "chat_administration.cmd.setgoodbye"),
        new BotCommand("/setrules", "chat_administration.cmd.setrules"),
        new BotCommand("/modstats", "chat_administration.cmd.modstats"),
    ];

    public IReadOnlyList<LocaleBundle> GetLocales() =>
    [
        new LocaleBundle("ru", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["display_name"] = "Модерация чатов",
            ["cmd.mute"] = "Временно ограничить участника",
            ["cmd.warn"] = "Предупредить участника",
            ["cmd.unmute"] = "Снять ограничение",
            ["cmd.ban"] = "Заблокировать участника",
            ["cmd.unban"] = "Разблокировать участника",
            ["cmd.kick"] = "Удалить участника",
            ["cmd.warnings"] = "Показать предупреждения",
            ["cmd.unwarn"] = "Снять предупреждение",
            ["cmd.clearwarnings"] = "Снять все предупреждения",
            ["cmd.purge"] = "Удалить сообщения",
            ["cmd.role"] = "Изменить роль",
            ["cmd.roles"] = "Показать роли",
            ["cmd.cases"] = "Показать moderation cases",
            ["cmd.case"] = "Показать moderation case",
            ["cmd.revoke"] = "Отменить moderation case",
            ["cmd.appeal"] = "Открыть appeal по case",
            ["cmd.approveappeal"] = "Одобрить appeal",
            ["cmd.rejectappeal"] = "Отклонить appeal",
            ["cmd.settings"] = "Настройки модерации",
            ["cmd.rules"] = "Показать правила чата",
            ["cmd.rule"] = "Включить или выключить правило",
            ["cmd.welcome"] = "Включить или выключить welcome",
            ["cmd.goodbye"] = "Включить или выключить goodbye",
            ["cmd.setwelcome"] = "Настроить welcome",
            ["cmd.setgoodbye"] = "Настроить goodbye",
            ["cmd.setrules"] = "Настроить правила чата",
            ["cmd.modstats"] = "Статистика модерации",
        }),
    ];
}
