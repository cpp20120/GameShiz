namespace BotFramework.Rest;

public sealed class RestUnauthorizedException(string detail, string code = "authentication_required")
    : RestHttpException(401, detail, code);
