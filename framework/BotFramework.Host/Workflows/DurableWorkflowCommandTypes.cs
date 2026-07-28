namespace BotFramework.Host.Workflows;

internal static class DurableWorkflowCommandTypes
{
    public static string Stable(Type type) =>
        $"{type.Assembly.GetName().Name}:{type.FullName}";
}
