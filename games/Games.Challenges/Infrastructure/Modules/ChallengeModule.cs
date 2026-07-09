
namespace Games.Challenges.Infrastructure.Modules;

public sealed class ChallengeModule : IModule
{
    public string Id => "challenges";
    public string DisplayName => "PvP Challenges";
    public string Version => "1.0.0";

    public void ConfigureServices(IModuleServiceCollection services)
    {
        services
            .BindOptions<ChallengeOptions>(ChallengeOptions.SectionName)
            .AddScoped<ChallengeDbContext>()
            .AddScoped<IChallengeStore, ChallengeStore>()
            .AddScoped<IChallengeService, ChallengeService>();
    }

    public IModuleMigrations GetMigrations() => new ChallengeMigrations();

    public IReadOnlyList<BotCommand> GetBotCommands() =>
    [
        new BotCommand("/challenge", "challenges.cmd.challenge"),
    ];

    public IReadOnlyList<LocaleBundle> GetLocales() => ChallengePresentationMetadata.Locales;
}
