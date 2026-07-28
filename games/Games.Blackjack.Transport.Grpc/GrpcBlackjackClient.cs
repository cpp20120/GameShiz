using System.Text.Json;
using Games.Blackjack.Contracts;
using Games.Blackjack.Domain.Results;
using Games.Blackjack.Transport.Grpc.Wire;
using Grpc.Core;
namespace Games.Blackjack.Transport.Grpc;
public sealed class GrpcBlackjackClient(BlackjackApi.BlackjackApiClient client):IBlackjackClient
{
 public async Task<BlackjackResult> StartAsync(long userId,string displayName,long chatId,int bet,string operationId,CancellationToken ct)=>(await client.StartAsync(BjWire.Call(new StartCall(userId,displayName,chatId,bet,operationId)),cancellationToken:ct)).Read<BlackjackResult>();
 public async Task<BlackjackResult> HitAsync(long userId,CancellationToken ct)=>(await client.HitAsync(BjWire.Call(new UserCall(userId)),cancellationToken:ct)).Read<BlackjackResult>();
 public async Task<BlackjackResult> StandAsync(long userId,CancellationToken ct)=>(await client.StandAsync(BjWire.Call(new UserCall(userId)),cancellationToken:ct)).Read<BlackjackResult>();
 public async Task<BlackjackResult> DoubleAsync(long userId,CancellationToken ct)=>(await client.DoubleAsync(BjWire.Call(new UserCall(userId)),cancellationToken:ct)).Read<BlackjackResult>();
 public async Task<BlackjackState> GetStateAsync(long userId,CancellationToken ct)=>(await client.GetStateAsync(BjWire.Call(new UserCall(userId)),cancellationToken:ct)).Read<BlackjackState>();
 public async Task SetStateMessageIdAsync(long userId,int messageId,CancellationToken ct)=>_ = await client.SetMessageAsync(BjWire.Call(new UserCall(userId,messageId)),cancellationToken:ct);
}
