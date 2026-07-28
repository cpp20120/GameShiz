namespace Games.Redeem.Contracts;

public sealed record RedeemCaptchaChallenge(string Pattern, IReadOnlyList<RedeemCaptchaItem> Items);
