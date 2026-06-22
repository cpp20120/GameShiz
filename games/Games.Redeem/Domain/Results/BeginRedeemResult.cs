
namespace Games.Redeem.Domain.Results;

public sealed record BeginRedeemResult(
    RedeemError Error,
    Guid CodeGuid = default,
    CaptchaResult? Captcha = null);
