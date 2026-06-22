using BotFramework.Sdk;

namespace BotFramework.Host.Composition.Modules;

public sealed record LoadedModules(
    IReadOnlyList<IModule> Modules,
    IReadOnlyDictionary<string, Dictionary<string, string>> LocalesByCulture,
    IReadOnlyList<BotCommand> BotCommands,
    IReadOnlyList<IModuleMigrations> Migrations);
