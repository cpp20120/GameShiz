using Telegram.Bot.Types;

namespace BotFramework.Sdk.UpdateHandling.Routes;

/// <summary>Matches updates describing the bot's membership in a Telegram chat.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class MyChatMemberAttribute : RouteAttribute
{
    public override int Priority => 305;
    public override string Name => "my_chat_member";
    public override bool Matches(Update update) => update.MyChatMember is not null;
}
