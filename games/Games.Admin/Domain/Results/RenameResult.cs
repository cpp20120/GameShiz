namespace Games.Admin;

public sealed record RenameResult(RenameOp Op, string OldName, string NewName);
