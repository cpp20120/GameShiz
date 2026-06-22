
namespace Games.Redeem;

public sealed record BeginRedeemResult(
    RedeemError Error,
    Guid CodeGuid = default,
    CaptchaResult? Captcha = null);
