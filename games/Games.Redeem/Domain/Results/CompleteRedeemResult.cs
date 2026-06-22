
namespace Games.Redeem.Domain.Results;

public sealed record CompleteRedeemResult(
    RedeemError Error,
    string FreeSpinGameId = "");
