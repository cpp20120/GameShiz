namespace BotFramework.Host.Composition.ServiceDatabases;

public sealed record ServiceOwnershipReport(
    bool IsValid,
    string Database,
    string User,
    string Schema,
    IReadOnlyList<string> Violations);

public interface IServiceOwnershipValidator
{
    Task<ServiceOwnershipReport> ValidateAsync(CancellationToken ct = default);
}
