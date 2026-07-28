namespace BotFramework.Rest;

public sealed class RestNotFoundException(string detail, string code = "not_found")
    : RestHttpException(404, detail, code);
