namespace BotFramework.Host.Composition.Builder;

public sealed class PostgresConnectionOptions
{
    public string ConnectionString { get; set; } = "";
}

public sealed class OperationsSecurityOptions
{
    public const string SectionName = "Services:Operations";
    public bool Required { get; set; }
    public string ApiKey { get; set; } = "";
}
