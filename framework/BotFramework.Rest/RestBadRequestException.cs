namespace BotFramework.Rest;

public sealed class RestBadRequestException(string detail, string code = "validation_error")
    : RestHttpException(400, detail, code);
