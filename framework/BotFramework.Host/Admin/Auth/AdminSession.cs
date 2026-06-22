namespace BotFramework.Host.Admin;

public sealed record AdminSession(long UserId, string Name, AdminRole Role);
