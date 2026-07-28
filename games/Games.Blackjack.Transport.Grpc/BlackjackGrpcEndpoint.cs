using System.Text.Json;
using Games.Blackjack.Contracts;
using Games.Blackjack.Domain.Results;
using Games.Blackjack.Transport.Grpc.Wire;
using Grpc.Core;
namespace Games.Blackjack.Transport.Grpc;
public sealed class BlackjackGrpcEndpoint(IBlackjackClient client):BlackjackApi.BlackjackApiBase
{
 public override async Task<ContractReply> Start(ContractCall request,ServerCallContext context){var x=request.Read<StartCall>();return BjWire.Reply(await client.StartAsync(x.UserId,x.Name,x.ChatId,x.Bet,x.OperationId,context.CancellationToken));}
 public override async Task<ContractReply> Hit(ContractCall request,ServerCallContext context)=>BjWire.Reply(await client.HitAsync(request.Read<UserCall>().UserId,context.CancellationToken));
 public override async Task<ContractReply> Stand(ContractCall request,ServerCallContext context)=>BjWire.Reply(await client.StandAsync(request.Read<UserCall>().UserId,context.CancellationToken));
 public override async Task<ContractReply> Double(ContractCall request,ServerCallContext context)=>BjWire.Reply(await client.DoubleAsync(request.Read<UserCall>().UserId,context.CancellationToken));
 public override async Task<ContractReply> GetState(ContractCall request,ServerCallContext context)=>BjWire.Reply(await client.GetStateAsync(request.Read<UserCall>().UserId,context.CancellationToken));
 public override async Task<ContractReply> SetMessage(ContractCall request,ServerCallContext context){var x=request.Read<UserCall>();await client.SetStateMessageIdAsync(x.UserId,x.MessageId,context.CancellationToken);return BjWire.Reply(new EmptyReply());}
}
