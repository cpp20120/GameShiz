using Discord.WebSocket;
using BotFramework.Discord.Routing;

namespace BotFramework.Discord.Commands;

public sealed record DiscordCommand(string Name, IReadOnlyList<string> Arguments)
{
    public static bool TryParse(DiscordMessageContext context, out DiscordCommand command)
    {
        command = null!;
        var parts = context.CommandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;
        command = new DiscordCommand(parts[0].ToLowerInvariant(), parts.Skip(1).ToArray());
        return true;
    }

    public bool Is(params string[] names) => names.Any(name => string.Equals(Name, name, StringComparison.OrdinalIgnoreCase));

    public bool TryGetPositiveInt(int index, out int value)
    {
        value = 0;
        return index >= 0 && index < Arguments.Count
            && int.TryParse(Arguments[index], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out value)
            && value > 0;
    }
}

public static class DiscordMessageContextExtensions
{
    public static long UserId(this DiscordMessageContext context) => checked((long)context.Message.Author.Id);
    public static long ScopeId(this DiscordMessageContext context) => checked((long)context.Message.Channel.Id);
    public static int SourceMessageId(this DiscordMessageContext context) => unchecked((int)context.Message.Id);
    public static string DisplayName(this DiscordMessageContext context) => context.Message.Author.GlobalName
        ?? context.Message.Author.Username
        ?? context.Message.Author.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static Task ReplyAsync(this DiscordMessageContext context, string text) =>
        context.Message.Channel.SendMessageAsync(text);
}
