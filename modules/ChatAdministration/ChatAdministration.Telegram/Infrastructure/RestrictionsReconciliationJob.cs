using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using BotFramework.Sdk.Modules;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace ChatAdministration.Telegram.Infrastructure;

public sealed class RestrictionsReconciliationJob(
    IChatAdministrationStore store,
    ITelegramBotClient bot) : IBackgroundJob
{
    public string Name => "chat_administration.restrictions_reconciliation";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ReconcileOnceAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken ct)
    {
        foreach (var chat in await store.ListEnabledChatsAsync(ct))
        {
            IReadOnlyList<DesiredMemberRestriction> restrictions;
            try
            {
                restrictions = await store.ListDesiredRestrictionsAsync(chat.Id, 200, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                continue;
            }

            foreach (var restriction in restrictions)
            {
                try
                {
                    var member = await bot.GetChatMember(restriction.ChatId.Value, restriction.UserId.Value, ct);
                    var observed = ToRestrictionState(member);
                    if (!Equivalent(restriction.ObservedState, observed))
                    {
                        await store.UpdateObservedRestrictionAsync(
                            restriction.ChatId,
                            restriction.UserId,
                            observed,
                            $"reconcile-restriction:{restriction.ChatId}:{restriction.UserId}",
                            ct);
                    }

                    if (Equivalent(restriction.State, observed))
                        continue;

                    var correlationId = $"reconcile-restriction:{restriction.ChatId}:{restriction.UserId}";
                    IModerationEffect effect = restriction.State.CanSendMessages
                        ? new UnrestrictMemberEffect(
                            restriction.ChatId,
                            restriction.UserId,
                            null,
                            restriction.State.Until,
                            correlationId,
                            correlationId)
                        : new RestrictMemberEffect(
                            restriction.ChatId,
                            restriction.UserId,
                            restriction.State.Until ?? DateTimeOffset.UtcNow.AddMinutes(1),
                            null,
                            correlationId,
                            correlationId);
                    await store.EnqueueEffectAsync(
                        effect,
                        $"reconcile-restriction:{restriction.ChatId}:{restriction.UserId}:{StateKey(restriction.State)}",
                        EffectImportance.Required,
                        ct);
                }
                catch (ApiRequestException) when (!ct.IsCancellationRequested)
                {
                    // A chat/member may disappear while a reconciliation pass is running.
                }
            }
        }
    }

    private static RestrictionState ToRestrictionState(ChatMember member) => member switch
    {
        ChatMemberRestricted restricted => new RestrictionState
        {
            CanSendMessages = restricted.CanSendMessages,
            Until = restricted.UntilDate is { } until ? new DateTimeOffset(until, TimeSpan.Zero) : null,
        },
        _ => new RestrictionState { CanSendMessages = true },
    };

    private static bool Equivalent(RestrictionState? left, RestrictionState? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        return left.CanSendMessages == right.CanSendMessages
            && (left.Until is null || right.Until is null || Math.Abs((left.Until.Value - right.Until.Value).TotalMinutes) <= 1);
    }

    private static string StateKey(RestrictionState state) =>
        $"{state.CanSendMessages}:{state.Until?.UtcTicks.ToString() ?? "none"}";
}
