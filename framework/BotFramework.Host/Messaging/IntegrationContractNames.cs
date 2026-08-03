namespace BotFramework.Host.Messaging;

internal static class IntegrationContractNames
{
    public static string Stable(Type type) =>
        $"{type.Assembly.GetName().Name}:{type.FullName}";
}
