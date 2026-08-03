namespace BotFramework.Host.Composition.ServiceDatabases;

public sealed class ServiceOwnershipOptions
{
    public const string SectionName = "ServiceOwnership";

    /// <summary>Disabled by default for backwards-compatible local upgrades.</summary>
    public bool Enforce { get; set; }

    public string Schema { get; set; } = "public";

    public string? ExpectedDatabase { get; set; }

    public bool RequireNonSuperuser { get; set; } = true;

    public bool RequireSchemaCreate { get; set; } = true;
}
