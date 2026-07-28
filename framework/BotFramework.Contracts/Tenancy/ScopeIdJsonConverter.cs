namespace BotFramework.Contracts.Tenancy;

internal sealed class ScopeIdJsonConverter : OpaqueIdJsonConverter<ScopeId>
{
    protected override string GetValue(ScopeId value) => value.Value;
}
