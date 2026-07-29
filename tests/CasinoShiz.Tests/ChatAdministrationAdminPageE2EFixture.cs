using System.Net;
using BotFramework.Host.Admin.Auth;
using BotFramework.Host.Persistence.Connections;
using ChatAdministration.Telegram.Infrastructure;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class ChatAdministrationAdminPageE2EFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("casinoshiz_chat_admin_page_e2e")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private ChatAdministrationAdminPageE2EApplication? application;

    public ChatAdministrationAdminPageE2EApplication Application => application
        ?? throw new InvalidOperationException("ChatAdministration admin page E2E application is not initialized.");

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await ApplySchemaAsync();
        await SeedAsync();
        application = await StartApplicationAsync();
    }

    public async Task DisposeAsync()
    {
        if (application is not null)
            await application.DisposeAsync();
        await database.DisposeAsync();
    }

    private async Task ApplySchemaAsync()
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        foreach (var migration in new ChatAdministrationMigrations().Migrations)
            await connection.ExecuteAsync(migration.Sql);
    }

    private async Task SeedAsync()
    {
        const long chatId = 770001;
        var caseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var warningId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var effectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var now = DateTimeOffset.UtcNow;

        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO chat_admin_chats
                (chat_id, chat_type, title, is_enabled, settings, bot_permissions, created_at, updated_at)
            VALUES
                (@chatId, 'supergroup', 'E2E Moderation Group', true,
                 '{"AutoModerationEnabled":true,"WarningLimit":3}'::jsonb,
                 '{"CanRestrictMembers":true,"CanDeleteMessages":true}'::jsonb,
                 @now, @now);
            INSERT INTO chat_admin_members
                (chat_id, user_id, username, display_name, status, roles, trust_level, last_seen_at)
            VALUES
                (@chatId, 770002, 'target_user', 'Target User', 'active', '["Member"]'::jsonb, 'unknown', @now),
                (@chatId, 770003, 'moderator', 'Test Moderator', 'active', '["Moderator"]'::jsonb, 'trusted', @now);
            INSERT INTO chat_admin_warnings
                (warning_id, chat_id, target_user_id, actor_user_id, reason, created_at, is_active)
            VALUES
                (@warningId, @chatId, 770002, 770003, 'flood from integration test', @now, true);
            INSERT INTO chat_admin_cases
                (case_id, chat_id, target_user_id, actor_user_id, actor_type, action, reason,
                 created_at, expires_at, status, correlation_id, updated_at)
            VALUES
                (@caseId, @chatId, 770002, 770003, 'human', 'mute', 'flood from integration test',
                 @now, @now + interval '10 minutes', 'applied', 'e2e-correlation-1', @now);
            INSERT INTO chat_admin_effect_outbox
                (effect_id, effect_type, payload, importance, case_id, correlation_id, causation_id,
                 idempotency_key, status, attempt, maximum_attempts, created_at, not_before, completed_at,
                 dependencies, updated_at)
            VALUES
                (@effectId, 'RestrictMember', '{"ChatId":770001,"UserId":770002}'::jsonb, 'required',
                 @caseId, 'e2e-correlation-1', 'e2e-cause-1', 'e2e-mute-1', 'applied', 1, 8,
                 @now, @now, @now, '[]'::jsonb, @now);
            INSERT INTO chat_admin_audit_events
                (chat_id, actor_user_id, target_user_id, action, correlation_id, case_id, metadata, created_at)
            VALUES
                (@chatId, 770003, 770002, 'moderation.case.applied', 'e2e-correlation-1', @caseId,
                 '{"action":"mute","source":"e2e"}'::jsonb, @now);
            """,
            new { chatId, caseId, warningId, effectId, now });
    }

    private async Task<ChatAdministrationAdminPageE2EApplication> StartApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ChatAdministrationAdminPageE2EFixture).Assembly.GetName().Name,
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http1));
        builder.Services.AddSingleton<INpgsqlConnectionFactory>(
            new ChatAdministrationTestConnectionFactory(database.GetConnectionString()));
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.Cookie.Name = "chat-admin-page-e2e";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.HttpOnly = true;
        });
        builder.Services
            .AddRazorPages()
            .AddApplicationPart(typeof(CasinoShiz.Host.Pages.Admin.ChatAdministrationModel).Assembly);

        var app = builder.Build();
        app.UseSession();
        app.MapGet("/e2e-login", (HttpContext context) =>
        {
            context.Session.SetAdminSession(new AdminSession(770003, "e2e-admin", AdminRole.SuperAdmin));
            return Results.Ok();
        });
        app.MapRazorPages();
        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()
            ?.Addresses;
        var address = addresses?.SingleOrDefault()
            ?? throw new InvalidOperationException("ChatAdministration admin page E2E app did not publish an address.");
        return new ChatAdministrationAdminPageE2EApplication(app, new Uri(address, UriKind.Absolute));
    }
}
