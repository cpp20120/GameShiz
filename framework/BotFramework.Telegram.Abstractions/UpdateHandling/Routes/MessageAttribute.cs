using Telegram.Bot.Types;

namespace BotFramework.Sdk.UpdateHandling.Routes;

/// <summary>Matches ordinary Telegram message updates not claimed by a higher-priority route.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class MessageAttribute : RouteAttribute
{
    public override int Priority => 50;
    public override string Name => "message";
    public override bool Matches(Update update) => update.Message is not null;
}
