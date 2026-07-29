using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed class MemberLifecycleService(
    IChatAdministrationStore store,
    VerificationService verification)
{
    public async Task<VerificationPersistenceResult> JoinedAsync(MemberJoinedCommand command, CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            command.ChatId,
            command.UserId,
            command.UserId,
            ChatMemberRole.Member,
            ChatMemberRole.Member,
            command.DisplayName,
            command.DisplayName,
            ct);
        var verificationRequired = context.Chat.Settings.CaptchaPolicy.Enabled;
        var decision = MemberLifecyclePolicy.Join(
            context.Chat,
            context.Target with { DisplayName = command.DisplayName, Username = command.Username },
            verificationRequired,
            command.CreatedAt);
        if (!decision.Accepted)
            return new VerificationPersistenceResult(false, false);

        var result = await store.PersistMemberJoinedAsync(command, decision, ct);
        if (!verificationRequired || result.Duplicate)
            return new VerificationPersistenceResult(!result.Duplicate, result.Duplicate);

        var verificationResult = await verification.StartAsync(command.ChatId, command.UserId, command.DisplayName, command.CreatedAt, ct);
        return new VerificationPersistenceResult(verificationResult.Applied, false);
    }

    public async Task<ModerationCommandResult> LeftAsync(MemberLeftCommand command, CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            command.ChatId,
            command.UserId,
            command.UserId,
            ChatMemberRole.Member,
            ChatMemberRole.Member,
            command.DisplayName,
            command.DisplayName,
            ct);
        var decision = MemberLifecyclePolicy.Leave(context.Chat, command.UserId, command.DisplayName, command.Username, command.CreatedAt);
        if (!decision.Accepted)
            return new ModerationCommandResult(false, false, decision.ErrorCode, null, string.Empty);

        var result = await store.PersistMemberLeftAsync(command, decision, ct);
        return new ModerationCommandResult(true, result.Duplicate, null, null, result.Duplicate ? "Команда уже обработана." : string.Empty);
    }

    public async Task<string> RulesAsync(ChatId chatId, UserId actorUserId, ChatMemberRole actorRole, string displayName, CancellationToken ct)
    {
        var context = await store.LoadContextAsync(chatId, actorUserId, actorUserId, actorRole, actorRole, displayName, displayName, ct);
        return MemberLifecyclePolicy.RenderRules(context.Chat);
    }
}
