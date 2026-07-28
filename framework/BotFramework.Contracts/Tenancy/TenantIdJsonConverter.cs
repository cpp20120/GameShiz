namespace BotFramework.Contracts.Tenancy;

internal sealed class TenantIdJsonConverter : OpaqueIdJsonConverter<TenantId>
{
    protected override string GetValue(TenantId value) => value.Value;
}
