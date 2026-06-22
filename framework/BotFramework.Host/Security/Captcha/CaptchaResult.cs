namespace BotFramework.Host.Security;

public sealed record CaptchaResult(string Pattern, int TargetId, CaptchaItem[] Items);
