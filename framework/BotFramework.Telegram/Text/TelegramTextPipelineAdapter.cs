using System.Globalization;
using BotFramework.Text;
using Telegram.Bot.Types;

namespace BotFramework.Telegram.Text;

/// <summary>
/// Converts a Telegram message into the platform-independent text pipeline input.
/// </summary>
public sealed class TelegramTextPipelineAdapter(TextPipeline pipeline)
{
    private readonly TextPipeline _pipeline =
        pipeline ?? throw new ArgumentNullException(nameof(pipeline));

    public ValueTask<TextPipelineResult> ProcessAsync(
        Message message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var text = message.Text ?? message.Caption ?? string.Empty;
        var contentType = "none";
        if (message.Text is not null)
            contentType = "text";
        else if (message.Caption is not null)
            contentType = "caption";

        var context = new TextProcessingContext
        {
            MessageId = message.MessageId.ToString(CultureInfo.InvariantCulture),
            Source = "telegram",
            Properties = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["chat_id"] = message.Chat.Id,
                ["user_id"] = message.From?.Id,
                ["content_type"] = contentType,
            },
        };

        return _pipeline.ProcessAsync(text, context, cancellationToken);
    }
}
