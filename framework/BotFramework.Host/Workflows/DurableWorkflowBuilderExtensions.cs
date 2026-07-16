using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using BotFramework.Host.Workflows;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

namespace BotFramework.Host.Composition.Builder;

public static class DurableWorkflowBuilderExtensions
{
    /// <summary>
    /// Adds the framework-owned durable workflow infrastructure. Application
    /// assemblies only provide immutable command records and Wolverine
    /// handlers; storage, retries, replay and generic saga state stay here.
    /// The PostgreSQL store belongs to the current service. Cross-service
    /// coordination must use contracts/outbox or a remote transport, not a
    /// shared domain database.
    /// </summary>
    public static WebApplicationBuilder AddDurableWorkflows(
        this WebApplicationBuilder builder,
        params Assembly[] handlerAssemblies)
    {
        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required for durable workflows.");
        var schema = builder.Configuration["DurableWorkflow:Schema"] ?? "durable_workflow";

        builder.Services.AddSingleton<IDurableWorkflowStepStore, PostgresDurableWorkflowStepStore>();
        builder.Services.AddScoped<IDurableWorkflowDispatcher, DurableWorkflowDispatcher>();
        builder.Services.AddScoped<IDurableWorkflowStepExecutor, DurableWorkflowStepExecutor>();
        builder.Services.AddScoped<IDurableWorkflowReplayService, DurableWorkflowReplayService>();

        builder.Host.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(connectionString, schema);
            opts.Policies.UseDurableLocalQueues();
            opts.Policies.OnException<Exception>().ScheduleRetryIndefinitely(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));
            opts.MessagePartitioning.UseInferredMessageGrouping();
            opts.Discovery.IncludeAssembly(typeof(DurableWorkflowStepHandler).Assembly);
            foreach (var assembly in handlerAssemblies.Where(static assembly => assembly is not null).Distinct())
                opts.Discovery.IncludeAssembly(assembly);
        });

        return builder;
    }
}
