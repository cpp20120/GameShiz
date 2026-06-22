namespace BotFramework.Host.Admin.Auth;

public sealed record AdminSession(long UserId, string Name, AdminRole Role);
