using ChatAdministration.Domain.Models;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class ModerationPresetCatalogTests
{
    [Fact]
    public void DefaultPresetResetsModerationButPreservesLifecycleAndOperationalSettings()
    {
        var original = new ChatSettings
        {
            Language = "en",
            TimeZone = "Europe/Helsinki",
            WelcomeEnabled = true,
            WelcomeTemplate = "hello",
            RulesText = "rules",
            ModerationLogChatId = new ChatId(-200),
            AutoModerationEnabled = false,
            WarningLimit = 99,
            FloodPolicy = new FloodPolicy { MaximumMessages = 100 },
        };

        var applied = ModerationPresetCatalog.TryApply(ModerationPresetCatalog.Default, original, out var updated);

        Assert.True(applied);
        Assert.Equal("en", updated.Language);
        Assert.Equal("Europe/Helsinki", updated.TimeZone);
        Assert.True(updated.WelcomeEnabled);
        Assert.Equal("hello", updated.WelcomeTemplate);
        Assert.Equal("rules", updated.RulesText);
        Assert.Equal(new ChatId(-200), updated.ModerationLogChatId);
        Assert.True(updated.AutoModerationEnabled);
        Assert.Equal(3, updated.WarningLimit);
        Assert.Equal(6, updated.FloodPolicy.MaximumMessages);
    }

    [Fact]
    public void StrictPresetEnablesProtectivePolicies()
    {
        Assert.True(ModerationPresetCatalog.TryApply(
            ModerationPresetCatalog.Strict,
            new ChatSettings(),
            out var updated));

        Assert.True(updated.ManualModerationEnabled);
        Assert.True(updated.AutoModerationEnabled);
        Assert.True(updated.CaptchaEnabled);
        Assert.True(updated.CaptchaPolicy.Enabled);
        Assert.Equal(LinkPolicyMode.DenyAll, updated.LinkPolicy.Mode);
        Assert.True(updated.MentionSpamPolicy.Enabled);
        Assert.True(updated.ForwardedMessagePolicy.Enabled);
        Assert.True(updated.NewMemberPolicy.Enabled);
        Assert.True(updated.CommandSpamPolicy.Enabled);
    }

    [Fact]
    public void DisabledPresetTurnsOffManualAndAutomaticModeration()
    {
        Assert.True(ModerationPresetCatalog.TryApply(
            ModerationPresetCatalog.Disabled,
            new ChatSettings { CaptchaEnabled = true },
            out var updated));

        Assert.False(updated.ManualModerationEnabled);
        Assert.False(updated.AutoModerationEnabled);
        Assert.False(updated.CaptchaEnabled);
        Assert.False(updated.CaptchaPolicy.Enabled);
    }
}
