using Telegram.Bot.Types;

namespace BotFramework.Sdk.UpdateHandling.Routes;

/// <summary>Matches Telegram chat-member lifecycle updates.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ChatMemberAttribute : RouteAttribute
{
    public override int Priority => 300;
    public override string Name => "chat_member";
    public override bool Matches(Update update) => update.ChatMember is not null;
}
