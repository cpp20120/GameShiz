namespace BotFramework.Sdk.Modules;

public interface IModule
{
    /// <summary>
    /// 
    /// </summary>
    /// Stable machine identifier: "poker", "sh", "blackjack". Used as the config
    /// section key (<c>Games:&lt;Id&gt;</c>), the event-type prefix, and the admin URL segment.
    string Id { get; }

    /// <summary>
    /// Human-readable name for menus and admin UI. Localized via GetLocales.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Semver for the module. Surfaced to admin UI; also gates migrations.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Called once at Host startup. Bind options, register services, register
    /// handlers, register repositories. Modules MUST NOT touch services other
    /// modules own - only the Host-provided abstractions.
    /// </summary>
    void ConfigureServices(IModuleServiceCollection services);

    /// Per-module schema. Host applies migrations against the module's own
    /// tracking table (__module_migrations) before any service starts.
    IModuleMigrations? GetMigrations() => null;

    /// <summary>
    /// Returns the module's localization resources.
    /// </summary>
    IReadOnlyList<LocaleBundle> GetLocales();

    /// <summary>
    /// Optional. Returns Telegram bot-menu commands this module contributes.
    /// </summary>
    IReadOnlyList<BotCommand> GetBotCommands() => [];

    /// <summary>
    /// Optional. Called when the Host is shutting down.
    /// </summary>
    Task ShutdownAsync(CancellationToken ct) => Task.CompletedTask;
}
