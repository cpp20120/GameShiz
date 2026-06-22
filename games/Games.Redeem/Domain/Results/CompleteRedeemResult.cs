
namespace Games.Redeem;

public sealed record CompleteRedeemResult(
    RedeemError Error,
    string FreeSpinGameId = "");
