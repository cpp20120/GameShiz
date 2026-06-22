using BotFramework.Sdk;
using Microsoft.Extensions.Configuration;

namespace BotFramework.Host.Composition;

public interface IBotFrameworkBuilder
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }

    IBotFrameworkBuilder AddModule<TModule>() where TModule : class, IModule, new();
}
