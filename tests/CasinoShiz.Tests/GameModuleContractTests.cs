using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class GameModuleContractTests
{
    private static readonly IModule[] AllModules =
    [
        new AdminModule(),
        new BasketballModule(),
        new BlackjackModule(),
        new BowlingModule(),
        new ChallengeModule(),
        new DartsModule(),
        new DiceModule(),
        new DiceCubeModule(),
        new FootballModule(),
        new HorseModule(),
        new LeaderboardModule(),
        new MetaModule(),
        new Games.Pick.Infrastructure.Modules.PickModule(),
        new PixelBattleModule(),
        new PokerModule(),
        new RedeemModule(),
        new SecretHitlerModule(),
        new TransferModule(),
    ];

    public static TheoryData<IModule> Modules
    {
        get
        {
            var data = new TheoryData<IModule>();
            foreach (var module in AllModules)
                data.Add(module);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Metadata_IsCompleteAndVersionIsNumeric(IModule module)
    {
        Assert.Matches("^[a-z0-9_]+$", module.Id);
        Assert.False(string.IsNullOrWhiteSpace(module.DisplayName));
        Assert.True(Version.TryParse(module.Version, out _), $"Invalid version for {module.Id}: {module.Version}");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void ConfigureServices_RegistersModuleComponents(IModule module)
    {
        var serviceCollection = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var registrations = new ModuleRegistrations();
        var adapter = new ModuleServiceCollectionAdapter(serviceCollection, configuration, registrations);

        module.ConfigureServices(adapter);

        Assert.NotEmpty(serviceCollection);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Locales_ContainDisplayNameAndEveryAdvertisedCommand(IModule module)
    {
        var locales = module.GetLocales();

        Assert.NotEmpty(locales);
        Assert.Equal(locales.Count, locales.Select(locale => locale.CultureCode).Distinct(StringComparer.Ordinal).Count());
        foreach (var locale in locales)
        {
            Assert.True(locale.Strings.ContainsKey("display_name"), $"{module.Id}/{locale.CultureCode} lacks display_name");
            foreach (var command in module.GetBotCommands())
            {
                Assert.StartsWith("/", command.Command, StringComparison.Ordinal);
                Assert.True(
                    locale.Strings.ContainsKey(command.DescriptionKey.Replace($"{module.Id}.", "", StringComparison.Ordinal)),
                    $"{module.Id}/{locale.CultureCode} lacks {command.DescriptionKey}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Migrations_HaveUniqueNonEmptyIds(IModule module)
    {
        var migrations = module.GetMigrations();
        if (migrations is null)
            return;

        Assert.False(string.IsNullOrWhiteSpace(migrations.ModuleId));
        Assert.Equal(
            migrations.Migrations.Count,
            migrations.Migrations.Select(migration => migration.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(migrations.Migrations, migration =>
        {
            Assert.False(string.IsNullOrWhiteSpace(migration.Id));
            Assert.False(string.IsNullOrWhiteSpace(migration.Sql));
        });
    }

    [Fact]
    public void AllModules_HaveUniqueIdsAndCommands()
    {
        var modules = AllModules;

        Assert.Equal(modules.Length, modules.Select(module => module.Id).Distinct(StringComparer.Ordinal).Count());
        var commands = modules.SelectMany(module => module.GetBotCommands()).Select(command => command.Command).ToArray();
        Assert.Equal(commands.Length, commands.Distinct(StringComparer.Ordinal).Count());
    }
}
