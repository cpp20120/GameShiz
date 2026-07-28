namespace Games.Redeem.Contracts;

public sealed record CompleteRedeemResponse(RedeemClientError Error, string FreeSpinGameId = "");
