
namespace Games.Redeem.Domain.Results;

public enum RedeemError
{
    None = 0,
    InvalidCode,
    AlreadyRedeemed,
    SelfRedeem,
    NoUser,
}
