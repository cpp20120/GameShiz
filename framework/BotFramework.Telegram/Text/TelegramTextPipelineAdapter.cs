using System.Globalization;
using BotFramework.Text;
using Telegram.Bot.Types;

namespace BotFramework.Telegram.Text;

/// <summary>
/// Converts a Telegram message into the platform-independent text pipeline input.
/// It contributes trusted Telegram metadata only; analyzers and policies remain module-owned.
/// </summary>
public sealed class TelegramTextPipelineAdapter(ITextProcessingPipeline pipeline)
{
    private readonly ITextProcessingPipeline _pipeline =
        pipeline ?? throw new ArgumentNullException(nameof(pipeline));

    public ValueTask<TextPipelineResult> ProcessAsync(
        Message message,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(message, context: null, cancellationToken);

    /// <summary>
    /// Processes a message while preserving caller-provided tenant, scope, request, and custom metadata.
    /// Trusted Telegram fields replace conflicting property-bag values.
    /// </summary>
    public ValueTask<TextPipelineResult> ProcessAsync(
        Message message,
        TextProcessingContext? context,
        CancellationToken cancellationToken = default)
    {
        var input = CreateInput(message, context);
        return _pipeline.ProcessAsync(input.Text, input.Context, cancellationToken);
    }

    public ValueTask<TextPipelineResult> AnalyzeAsync(
        Message message,
        CancellationToken cancellationToken = default) =>
        AnalyzeAsync(message, context: null, cancellationToken);

    /// <summary>Runs the Telegram-adapted pipeline without executing effects.</summary>
    public ValueTask<TextPipelineResult> AnalyzeAsync(
        Message message,
        TextProcessingContext? context,
        CancellationToken cancellationToken = default)
    {
        var input = CreateInput(message, context);
        return _pipeline.AnalyzeAsync(input.Text, input.Context, cancellationToken);
    }

    private static TelegramTextInput CreateInput(
        Message message,
        TextProcessingContext? inheritedContext)
    {
        ArgumentNullException.ThrowIfNull(message);

        var text = message.Text ?? message.Caption ?? string.Empty;
        var contentType = message.Text is not null
            ? "text"
            : message.Caption is not null
                ? "caption"
                : "none";
        var messageId = message.MessageId.ToString(CultureInfo.InvariantCulture);
        var properties = new Dictionary<string, object?>(
            inheritedContext?.Properties ?? new Dictionary<string, object?>(),
            StringComparer.Ordinal)
        {
            [TextProcessingKeys.ChatId] = message.Chat.Id,
            [TextProcessingKeys.UserId] = message.From?.Id,
            [TextProcessingKeys.ContentType] = contentType,
            [TextProcessingKeys.ThreadId] = message.MessageThreadId,
            [TextProcessingKeys.SentAt] = message.Date.ToUniversalTime(),
            ["has_entities"] = (message.Entities?.Length ?? message.CaptionEntities?.Length ?? 0) > 0,
            ["is_forwarded"] = message.ForwardOrigin is not null,
        };

        return new TelegramTextInput(
            text,
            new TextProcessingContext
            {
                MessageId = messageId,
                Source = "telegram",
                RequestId = inheritedContext?.RequestId,
                CorrelationId = inheritedContext?.CorrelationId
                    ?? $"telegram:{message.Chat.Id}:{messageId}",
                Properties = properties,
            });
    }

    private sealed record TelegramTextInput(string Text, TextProcessingContext Context);
}
