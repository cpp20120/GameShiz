namespace BotFramework.Rest;

public sealed class RestFrameworkOptions
{
    public const string SectionName = "Rest";

    public string ApiVersion { get; init; } = "v1";

    public bool RequireIdempotencyKeyForCommands { get; init; } = true;

    public bool RequireScopeClaim { get; init; }

    public bool OpenApiEnabled { get; init; } = true;

    public bool ExposeOpenApiOutsideDevelopment { get; init; }

    public int PermitLimit { get; init; } = 120;

    public int WindowSeconds { get; init; } = 60;

    public JwtOptions Jwt { get; init; } = new();

    public sealed class JwtOptions
    {
        public string? Authority { get; init; }

        public string? MetadataAddress { get; init; }

        public string? Audience { get; init; }

        public string? Issuer { get; init; }

        public bool RequireHttpsMetadata { get; init; } = true;
    }
}
