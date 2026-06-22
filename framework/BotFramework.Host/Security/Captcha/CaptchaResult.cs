namespace BotFramework.Host.Security.Captcha;

public sealed record CaptchaResult(string Pattern, int TargetId, CaptchaItem[] Items);
