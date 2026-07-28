namespace BotFramework.Contracts.Tenancy;

internal sealed class RequestIdJsonConverter : OpaqueIdJsonConverter<RequestId>
{
    protected override string GetValue(RequestId value) => value.Value;
}
