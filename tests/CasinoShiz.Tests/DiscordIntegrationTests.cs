using BotFramework.Discord;
using BotFramework.Discord.Hosting;
using BotFramework.Discord.Interactions;
using Discord;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DiscordIntegrationTests
{
    [Fact]
    public void ResultEmbed_UsesCasinoStyleAndKeepsUsefulPayload()
    {
        var embed = DiscordEmbeds.Result(new MockResult("OK", 125), "Balance", "en");

        Assert.Equal("Balance", embed.Title);
        Assert.Contains("Error", embed.Description);
        Assert.Contains("CasinoShiz", embed.Footer?.Text);
        Assert.Equal(new Color(0x8B5CF6), embed.Color);
    }

    [Fact]
    public void Localizer_MapsDiscordLocalesToRuAndEn()
    {
        Assert.Equal("Выбери раздел", DiscordLocalization.Get("casino.menu.placeholder", "ru-RU"));
        Assert.Equal("Choose a section", DiscordLocalization.Get("casino.menu.placeholder", "en-US"));
        Assert.Equal("ru", DiscordLocalization.Normalize("de", "ru"));
        Assert.Equal("en", DiscordLocalization.Normalize("de", "en"));
    }

    [Fact]
    public void ComponentTokens_FromPreviousProcessAreRejected()
    {
        var previousProcess = new DiscordComponentTokenStore();
        var oldButton = previousProcess.Issue("blackjack:hit");
        var currentProcess = new DiscordComponentTokenStore();

        Assert.False(currentProcess.TryResolve(oldButton, out _));
        Assert.True(previousProcess.TryResolve(oldButton, out var token));
        Assert.Equal("blackjack:hit", token.Action);
    }

    [Fact]
    public void ModalBuilder_CreatesStableInputContractForMockInteraction()
    {
        var modal = DiscordInteraction.TextModal(
            "cs:test:modal",
            "New bet",
            "bet",
            "Bet",
            "For example, 100",
            maxLength: 9);

        Assert.Equal("cs:test:modal", modal.CustomId);
        Assert.Equal("New bet", modal.Title);
        Assert.Contains(
            modal.Component.Components
                .OfType<ActionRowComponent>()
                .SelectMany(row => row.Components)
                .OfType<TextInputComponent>(),
            component => component.CustomId == "bet");
    }

    [Fact]
    public void RateLimiter_ProtectsMockInteractionsAndLetsAutocompleteThrough()
    {
        var clock = new ManualTimeProvider();
        var options = Options.Create(new DiscordOptions
        {
            RateLimitWindowSeconds = 10,
            RateLimitMaxRequests = 2,
            InteractionCooldownMilliseconds = 500,
        });
        var limiter = new DiscordUxRateLimiter(options, clock);
        var interaction = new MockInteraction(42, "component:blackjack:hit");

        Assert.True(interaction.Dispatch(limiter).Allowed);
        Assert.False(interaction.Dispatch(limiter).Allowed);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.True(interaction.Dispatch(limiter).Allowed);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.False(interaction.Dispatch(limiter).Allowed);

        var autocomplete = new MockInteraction(42, "autocomplete:blackjack", IsAutocomplete: true);
        Assert.True(autocomplete.Dispatch(limiter).Allowed);
    }

    private sealed record MockResult(string Error, int Balance);

    private sealed record MockInteraction(ulong UserId, string Bucket, bool IsAutocomplete = false)
    {
        public DiscordUxDecision Dispatch(DiscordUxRateLimiter limiter) =>
            limiter.Check(UserId, Bucket, IsAutocomplete);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
