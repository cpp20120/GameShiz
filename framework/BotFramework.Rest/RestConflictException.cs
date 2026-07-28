namespace BotFramework.Rest;

public sealed class RestConflictException(string detail, string code = "conflict")
    : RestHttpException(409, detail, code);
