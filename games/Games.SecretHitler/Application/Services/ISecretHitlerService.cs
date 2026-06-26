using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using static Games.SecretHitler.Domain.Rules.ShResultHelpers;

namespace Games.SecretHitler.Application.Services;

public interface ISecretHitlerService
{
    Task<(ShGameSnapshot? Snapshot, SecretHitlerPlayer? Me)> FindMyGameAsync(long userId, CancellationToken ct);
    Task<ShCreateResult> CreateGameAsync(
        long userId, string displayName, long publicChatId, long playerChatId, CancellationToken ct);
    Task<ShJoinResult> JoinGameAsync(
        long userId, string displayName, long playerChatId, string code, CancellationToken ct);
    Task<ShStartResult> StartGameAsync(long userId, CancellationToken ct);
    Task<ShNominateResult> NominateAsync(long userId, int chancellorPosition, CancellationToken ct);
    Task<ShVoteResult> VoteAsync(long userId, ShVote vote, CancellationToken ct);
    Task<ShDiscardResult> PresidentDiscardAsync(long userId, int discardIndex, CancellationToken ct);
    Task<ShEnactResult> ChancellorEnactAsync(long userId, int enactIndex, CancellationToken ct);
    Task<ShLeaveResult> LeaveAsync(long userId, CancellationToken ct);
    Task SetStateMessageIdAsync(long userId, int messageId, CancellationToken ct);
    Task SetPublicStateMessageIdAsync(string inviteCode, int messageId, CancellationToken ct);
}
