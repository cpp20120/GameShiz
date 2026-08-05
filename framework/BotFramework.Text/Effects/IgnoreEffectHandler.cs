namespace BotFramework.Text;

/// <summary>
/// Built-in no-op handler for explicit ignore decisions. Empty decisions remain the preferred form.
/// </summary>
public sealed class IgnoreEffectHandler : MessageEffectHandler<IgnoreEffect>
{
    protected override ValueTask ExecuteAsync(
        IgnoreEffect effect,
        TextProcessingContext context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
