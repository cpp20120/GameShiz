namespace BotFramework.Sdk.Configuration;

public sealed record ConfigurationValidationIssue(
    string Path,
    string Code,
    string Message);