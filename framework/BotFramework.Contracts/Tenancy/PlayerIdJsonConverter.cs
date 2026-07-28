namespace BotFramework.Contracts.Tenancy;

internal sealed class PlayerIdJsonConverter : OpaqueIdJsonConverter<PlayerId>
{
    protected override string GetValue(PlayerId value) => value.Value;
}
