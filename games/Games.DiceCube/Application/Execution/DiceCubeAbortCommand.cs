namespace Games.DiceCube.Application.Execution;

public sealed record DiceCubeAbortCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    string CommandId);
