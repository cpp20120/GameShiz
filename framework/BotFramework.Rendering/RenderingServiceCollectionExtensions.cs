using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BotFramework.Rendering;

public static class RenderingServiceCollectionExtensions
{
    public static IServiceCollection AddBotFrameworkRendering(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<RenderingOptions>()
            .Bind(configuration.GetSection(RenderingOptions.SectionName))
            .Validate(static options => options.QueueCapacity > 0, "Rendering:QueueCapacity must be positive.")
            .Validate(static options => options.MaxParallelism >= 0, "Rendering:MaxParallelism cannot be negative.")
            .Validate(static options => options.MaxArtifactBytes > 0, "Rendering:MaxArtifactBytes must be positive.")
            .Validate(static options => !options.Minio.Enabled
                || (!string.IsNullOrWhiteSpace(options.Minio.Endpoint)
                    && !string.IsNullOrWhiteSpace(options.Minio.AccessKey)
                    && !string.IsNullOrWhiteSpace(options.Minio.SecretKey)
                    && !string.IsNullOrWhiteSpace(options.Minio.Bucket)),
                "Rendering:Minio endpoint, credentials and bucket are required when enabled.")
            .ValidateOnStart();

        if (configuration.GetValue<bool>($"{RenderingOptions.SectionName}:Minio:Enabled"))
            services.AddSingleton<IRenderArtifactStore, MinioRenderArtifactStore>();
        else
            services.AddSingleton<IRenderArtifactStore, InMemoryRenderArtifactStore>();

        services.AddSingleton<TplRenderWorker>();
        services.AddSingleton<IRenderQueue>(static provider => provider.GetRequiredService<TplRenderWorker>());
        services.AddSingleton<IRenderHistory>(static provider => provider.GetRequiredService<TplRenderWorker>());
        services.AddHostedService(static provider => provider.GetRequiredService<TplRenderWorker>());
        return services;
    }

    public static IServiceCollection AddRenderJob<TSpec, TJob>(this IServiceCollection services)
        where TJob : class, IRenderJob<TSpec>
    {
        services.AddScoped<IRenderJob<TSpec>, TJob>();
        return services;
    }
}
