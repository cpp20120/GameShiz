namespace BotFramework.Rest;

public sealed class RestForbiddenException(string detail, string code = "access_denied")
    : RestHttpException(403, detail, code);
