namespace Games.Admin.Domain.Results;

public sealed record RenameResult(RenameOp Op, string OldName, string NewName);
