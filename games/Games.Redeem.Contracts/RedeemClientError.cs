namespace Games.Redeem.Contracts;

public enum RedeemClientError
{
    None,
    InvalidCode,
    AlreadyRedeemed,
    SelfRedeem,
    NoUser,
}
