namespace BotFramework.Discord.Hosting;

public readonly record struct DiscordUxDecision(bool Allowed, TimeSpan RetryAfter);
