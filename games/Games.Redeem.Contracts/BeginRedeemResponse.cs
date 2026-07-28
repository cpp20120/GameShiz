namespace Games.Redeem.Contracts;

public sealed record BeginRedeemResponse(
    RedeemClientError Error,
    Guid CodeGuid = default,
    RedeemCaptchaChallenge? Captcha = null);
