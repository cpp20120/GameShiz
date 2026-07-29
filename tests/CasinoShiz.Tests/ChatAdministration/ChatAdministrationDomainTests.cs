using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Parsing;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class ChatAdministrationDomainTests
{
    [Fact]
    public void ModeratorCanMuteMemberAndPlanTypedRestriction()
    {
        var now = DateTimeOffset.UtcNow;
        var decision = MutePolicy.Decide(
            new MuteRequest(new ChatId(-100), new UserId(10), new UserId(20), TimeSpan.FromMinutes(10), "flood", "corr", "cause", now),
            Chat(-100),
            Member(-100, 10, ChatMemberRole.Moderator),
            Member(-100, 20, ChatMemberRole.Member));

        Assert.True(decision.Accepted);
        Assert.Equal(2, decision.Events.Count);
        var effect = Assert.Single(decision.EffectPlan.Effects, planned => planned.Effect is RestrictMemberEffect);
        var restriction = Assert.IsType<RestrictMemberEffect>(effect.Effect);
        Assert.Equal(TimeSpan.FromMinutes(10), restriction.Until - now);
        Assert.Equal(EffectImportance.Required, effect.Importance);

        var expiration = Assert.Single(decision.EffectPlan.Effects, planned => planned.Effect is UnrestrictMemberEffect);
        var unrestriction = Assert.IsType<UnrestrictMemberEffect>(expiration.Effect);
        Assert.Equal(restriction.Until, unrestriction.ExpectedUntil);
        Assert.Contains(effect.Id!.Value, expiration.DependsOn);
    }

    [Theory]
    [InlineData(7, "нед.")]
    [InlineData(1, "дн.")]
    [InlineData(1, "ч.")]
    [InlineData(1, "мин.")]
    [InlineData(30, "сек.")]
    public void MuteResponseUsesHumanDuration(int amount, string suffix)
    {
        var duration = suffix switch
        {
            "нед." => TimeSpan.FromDays(amount * 7),
            "дн." => TimeSpan.FromDays(amount),
            "ч." => TimeSpan.FromHours(amount),
            "мин." => TimeSpan.FromMinutes(amount),
            _ => TimeSpan.FromSeconds(amount),
        };
        var decision = MutePolicy.Decide(
            new MuteRequest(new ChatId(-100), new UserId(10), new UserId(20), duration, "reason", "corr", "cause", DateTimeOffset.UtcNow),
            Chat(-100),
            Member(-100, 10, ChatMemberRole.Moderator),
            Member(-100, 20, ChatMemberRole.Member));
        var response = Assert.IsType<SendMessageEffect>(decision.EffectPlan.Effects.Single(x => x.Effect is SendMessageEffect).Effect);

        Assert.Contains(suffix, response.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EqualOrHigherRoleCannotBeModerated()
    {
        var actor = Member(-100, 10, ChatMemberRole.Moderator);
        var admin = Member(-100, 20, ChatMemberRole.Admin);
        var owner = Member(-100, 30, ChatMemberRole.Owner);

        Assert.False(AuthorizationPolicy.Authorize(actor, admin, Permission.MembersMute).Allowed);
        Assert.False(AuthorizationPolicy.Authorize(actor, owner, Permission.MembersMute).Allowed);
        Assert.False(AuthorizationPolicy.Authorize(admin, owner, Permission.MembersMute).Allowed);
    }

    [Fact]
    public void OrdinaryMemberCannotUseMutePermission()
    {
        var result = AuthorizationPolicy.Authorize(
            Member(-100, 10, ChatMemberRole.Member),
            Member(-100, 20, ChatMemberRole.Member),
            Permission.MembersMute);

        Assert.False(result.Allowed);
        Assert.Equal("permission_denied", result.ErrorCode);
    }

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("10m", 600)]
    [InlineData("2h", 7200)]
    [InlineData("1d", 86400)]
    [InlineData("1w", 604800)]
    public void MuteDurationParserNormalizesUnits(string token, int expectedSeconds)
    {
        Assert.True(MuteCommandParser.TryParse($"/mute {token} flood", out var parsed, out var error), error);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), parsed!.Duration);
        Assert.Equal("flood", parsed.Reason);
    }

    [Fact]
    public async Task RepeatedCommandIdDoesNotCreateSecondCase()
    {
        var store = new RecordingStore();
        var service = new ModerationCommandService(store);
        var command = new MuteMemberCommand(
            "command-1", "telegram-update:1", "corr", "cause", new ChatId(-100),
            new UserId(10), new UserId(20), "moderator", "member", TimeSpan.FromMinutes(10), "flood",
            DateTimeOffset.UtcNow, 99, ChatMemberRole.Moderator, ChatMemberRole.Member);

        var first = await service.ExecuteMuteAsync(command, CancellationToken.None);
        var second = await service.ExecuteMuteAsync(command, CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.True(second.Duplicate);
        Assert.Equal(2, store.PersistCalls);
        Assert.Equal(1, store.CreatedCases);
    }

    private static ChatState Chat(long chatId) => new()
    {
        Id = new ChatId(chatId),
        Type = ChatType.Supergroup,
        Title = "test",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static MemberState Member(long chatId, long userId, ChatMemberRole role) => new()
    {
        ChatId = new ChatId(chatId),
        UserId = new UserId(userId),
        DisplayName = $"user-{userId}",
        Roles = new HashSet<ChatMemberRole> { role },
        FirstSeenAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow,
    };

}
