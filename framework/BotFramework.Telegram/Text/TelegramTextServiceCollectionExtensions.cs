using BotFramework.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BotFramework.Telegram.Text;

public static class TelegramTextServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform-neutral text pipeline and the Telegram message adapter.
    /// Effect handlers remain opt-in; call <see cref="AddTelegramTextEffectHandlers"/>
    /// when the consuming application wants the standard Telegram implementations.
    /// </summary>
    public static IServiceCollection AddTelegramTextProcessing(
        this IServiceCollection services,
        TextNormalizerOptions? normalizerOptions = null,
        MessageEffectExecutorOptions? effectExecutorOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTextProcessing(normalizerOptions, effectExecutorOptions);
        services.TryAddScoped<TelegramTextPipelineAdapter>();
        return services;
    }

    /// <summary>
    /// Registers the standard Telegram handlers for the transport-neutral reply, delete,
    /// add-reaction and set-reactions effects. Queueing, persistence, retries and business
    /// actions intentionally remain module-owned.
    /// </summary>
    public static IServiceCollection AddTelegramTextEffectHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMessageEffectHandler, TelegramReplyEffectHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMessageEffectHandler, TelegramDeleteMessageEffectHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMessageEffectHandler, TelegramAddReactionEffectHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMessageEffectHandler, TelegramSetMessageReactionsEffectHandler>());
        return services;
    }
}
