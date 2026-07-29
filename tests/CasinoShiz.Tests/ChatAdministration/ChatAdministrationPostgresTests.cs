using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using ChatAdministration.Telegram.Infrastructure;
using Dapper;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

[Collection(AtomicPostgresCollection.Name)]
public sealed class ChatAdministrationPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MuteIsAtomicIdempotentAndDurablyScheduled()
    {
        var store = new ChatAdministrationStore(new ChatAdministrationTestConnectionFactory(database.ConnectionString));
        var service = new ModerationCommandService(store);
        var command = new MuteMemberCommand(
            "postgres-mute-1",
            "telegram-update:integration:1",
            "correlation:integration:1",
            "telegram-update:integration:1",
            new ChatId(-100),
            new UserId(10),
            new UserId(20),
            "moderator",
            "member",
            TimeSpan.FromMinutes(10),
            "flood",
            Now,
            123,
            ChatMemberRole.Admin,
            ChatMemberRole.Member);

        var first = await service.ExecuteMuteAsync(command, CancellationToken.None);
        var duplicate = await service.ExecuteMuteAsync(command, CancellationToken.None);

        Assert.True(first.Accepted, first.ErrorCode);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_cases"));
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_commands"));
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_members WHERE chat_id = -100 AND user_id = 20"));
        Assert.True(await database.ScalarAsync<bool>("SELECT desired_restriction IS NOT NULL FROM chat_admin_members WHERE chat_id = -100 AND user_id = 20"));
        Assert.True(await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_effect_outbox WHERE effect_type = 'telegram.restrict_member'") == 1);
        Assert.True(await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_effect_outbox WHERE effect_type = 'telegram.unrestrict_member' AND not_before > @now", new { now = Now }) == 1);
        Assert.True(await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_audit_events WHERE action = 'moderation.mute.requested'") == 1);
    }

    [Fact]
    public async Task SettingsCallbackTokenIsOpaqueServerSideAndSingleUse()
    {
        var store = new ChatAdministrationStore(new ChatAdministrationTestConnectionFactory(database.ConnectionString));
        var token = await store.CreateSettingsCallbackAsync(
            new ChatId(-100),
            "captcha",
            "toggle",
            Now.AddMinutes(5),
            CancellationToken.None);

        Assert.DoesNotContain("captcha", token, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("toggle", token, StringComparison.OrdinalIgnoreCase);

        var first = await store.ConsumeSettingsCallbackAsync(token, new ChatId(-100), new UserId(10), CancellationToken.None);
        var second = await store.ConsumeSettingsCallbackAsync(token, new ChatId(-100), new UserId(10), CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal("captcha", first!.Key);
        Assert.Equal("toggle", first.Value);
        Assert.Null(second);
    }

    [Fact]
    public async Task ScheduledTypedEffectRoundTripsThroughOutbox()
    {
        var store = new ChatAdministrationStore(new ChatAdministrationTestConnectionFactory(database.ConnectionString));
        var inner = new SendMessageEffect(new ChatId(-100), "scheduled", ParseMode: MessageParseMode.Html);
        var nested = new EffectEnvelope
        {
            Id = EffectId.New(),
            EffectType = EffectTypeCatalog.SendMessage,
            Payload = inner,
            CorrelationId = "schedule-correlation",
            CausationId = "schedule-causation",
            IdempotencyKey = "schedule-inner",
            CreatedAt = Now,
        };
        var schedule = new ScheduleEffect(Now.AddMinutes(5), nested, "schedule-correlation", "schedule-causation");

        await store.EnqueueEffectAsync(schedule, "schedule-roundtrip", EffectImportance.Required, CancellationToken.None);
        var claimed = await store.ClaimDueEffectsAsync(10, TimeSpan.FromMinutes(1), CancellationToken.None);

        var restored = Assert.IsType<ScheduleEffect>(Assert.Single(claimed).Payload);
        var restoredMessage = Assert.IsType<SendMessageEffect>(restored.Effect.Payload);
        Assert.Equal(Now.AddMinutes(5), restored.ExecuteAt);
        Assert.Equal("scheduled", restoredMessage.Text);
        Assert.Equal(EffectTypeCatalog.SendMessage, restored.Effect.EffectType);
    }

    [Fact]
    public async Task ChatMetadataRegistersThenUpdatesWithoutCrossChatLeakage()
    {
        var store = new ChatAdministrationStore(new ChatAdministrationTestConnectionFactory(database.ConnectionString));
        await store.UpsertChatMetadataAsync(
            new ChatMetadataCommand(
                "metadata-1",
                "telegram-update:metadata:1",
                "metadata-correlation-1",
                new ChatId(-100),
                ChatType.Group,
                "First title",
                Now),
            CancellationToken.None);
        await store.UpsertChatMetadataAsync(
            new ChatMetadataCommand(
                "metadata-2",
                "telegram-update:metadata:2",
                "metadata-correlation-2",
                new ChatId(-200),
                ChatType.Supergroup,
                "Second title",
                Now.AddMinutes(1)),
            CancellationToken.None);
        await store.UpsertChatMetadataAsync(
            new ChatMetadataCommand(
                "metadata-3",
                "telegram-update:metadata:3",
                "metadata-correlation-3",
                new ChatId(-100),
                ChatType.Group,
                "Renamed title",
                Now.AddMinutes(2)),
            CancellationToken.None);

        Assert.Equal("Renamed title", await database.ScalarAsync<string>("SELECT title FROM chat_admin_chats WHERE chat_id = -100"));
        Assert.Equal("Second title", await database.ScalarAsync<string>("SELECT title FROM chat_admin_chats WHERE chat_id = -200"));
        Assert.Equal(2, await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_chats"));
        Assert.Equal(2, await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_domain_events WHERE event_type = 'ChatRegistered'"));
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT count(*) FROM chat_admin_domain_events WHERE event_type = 'ChatMetadataUpdated'"));
    }

    [Fact]
    public async Task CustomRolesAndModerationRulesSurviveSettingsAndMemberReload()
    {
        var store = new ChatAdministrationStore(new ChatAdministrationTestConnectionFactory(database.ConnectionString));
        var roleId = new RoleId("support");
        var settings = new ChatSettings
        {
            ModerationRules =
            [
                new ModerationRuleDefinition
                {
                    Id = new RuleId("links"),
                    Type = ModerationRuleType.Link,
                    IsEnabled = false,
                    Priority = 3,
                    ScoreOverride = 9,
                },
            ],
            CustomRoles =
            [
                new CustomRoleDefinition
                {
                    Id = roleId,
                    DisplayName = "Support",
                    Rank = 50,
                    Permissions = new HashSet<Permission> { Permission.MembersWarn },
                },
            ],
        };
        await store.UpdateChatSettingsAsync(new ChatId(-100), settings, new UserId(10), "settings-correlation", CancellationToken.None);

        var context = await store.LoadContextAsync(
            new ChatId(-100),
            new UserId(10),
            new UserId(20),
            ChatMemberRole.Admin,
            ChatMemberRole.Member,
            "admin",
            "member",
            CancellationToken.None);
        Assert.False(context.Chat.Settings.ModerationRules.Single().IsEnabled);
        Assert.Equal(9, context.Chat.Settings.ModerationRules.Single().ScoreOverride);

        var decision = RolePolicy.ChangeCustom(context.Chat, context.Actor, context.Target, roleId, assign: true);
        Assert.True(decision.Accepted, decision.ErrorCode);
        await store.PersistRoleMutationAsync(
            new RoleMutationCommand(
                "custom-role-assignment-1",
                "custom-role-assignment-1",
                "custom-role-correlation",
                "custom-role-causation",
                new ChatId(-100),
                new UserId(10),
                new UserId(20),
                ChatMemberRole.Member,
                true,
                decision.Member!,
                decision.Events.Single(),
                "assigned",
                Now,
                null)
            {
                CustomRoleId = roleId,
            },
            CancellationToken.None);

        var reloaded = await store.LoadContextAsync(
            new ChatId(-100),
            new UserId(10),
            new UserId(20),
            ChatMemberRole.Admin,
            ChatMemberRole.Member,
            "admin",
            "member",
            CancellationToken.None);
        Assert.Contains(roleId, reloaded.Chat.Settings.CustomRoles.Select(role => role.Id));
        Assert.Contains(roleId, reloaded.Target.CustomRoleIds);
    }
}
