using System.Text.Json;
using Games.Blackjack.Contracts;
using Games.Blackjack.Domain.Results;
using Games.Blackjack.Transport.Grpc.Wire;
using Grpc.Core;
namespace Games.Blackjack.Transport.Grpc;
internal static class BjWire
{
 static readonly JsonSerializerOptions O=new(JsonSerializerDefaults.Web);
 public static ContractCall Call<T>(T x)=>new(){PayloadJson=JsonSerializer.Serialize(x,O)};
 public static ContractReply Reply<T>(T x)=>new(){PayloadJson=JsonSerializer.Serialize(x,O)};
 public static T Read<T>(this ContractCall x)=>JsonSerializer.Deserialize<T>(x.PayloadJson,O)!;
 public static T Read<T>(this ContractReply x)=>JsonSerializer.Deserialize<T>(x.PayloadJson,O)!;
}
internal sealed record StartCall(long UserId,string Name,long ChatId,int Bet,string OperationId);
internal sealed record UserCall(long UserId,int MessageId=0);
internal sealed record EmptyReply;
public sealed class BlackjackGrpcEndpoint(IBlackjackClient client):BlackjackApi.BlackjackApiBase
{
 public override async Task<ContractReply> Start(ContractCall r,ServerCallContext c){var x=r.Read<StartCall>();return BjWire.Reply(await client.StartAsync(x.UserId,x.Name,x.ChatId,x.Bet,x.OperationId,c.CancellationToken));}
 public override async Task<ContractReply> Hit(ContractCall r,ServerCallContext c)=>BjWire.Reply(await client.HitAsync(r.Read<UserCall>().UserId,c.CancellationToken));
 public override async Task<ContractReply> Stand(ContractCall r,ServerCallContext c)=>BjWire.Reply(await client.StandAsync(r.Read<UserCall>().UserId,c.CancellationToken));
 public override async Task<ContractReply> Double(ContractCall r,ServerCallContext c)=>BjWire.Reply(await client.DoubleAsync(r.Read<UserCall>().UserId,c.CancellationToken));
 public override async Task<ContractReply> GetState(ContractCall r,ServerCallContext c)=>BjWire.Reply(await client.GetStateAsync(r.Read<UserCall>().UserId,c.CancellationToken));
 public override async Task<ContractReply> SetMessage(ContractCall r,ServerCallContext c){var x=r.Read<UserCall>();await client.SetStateMessageIdAsync(x.UserId,x.MessageId,c.CancellationToken);return BjWire.Reply(new EmptyReply());}
}
public sealed class GrpcBlackjackClient(BlackjackApi.BlackjackApiClient client):IBlackjackClient
{
 public async Task<BlackjackResult> StartAsync(long u,string n,long ch,int b,string op,CancellationToken ct)=>(await client.StartAsync(BjWire.Call(new StartCall(u,n,ch,b,op)),cancellationToken:ct)).Read<BlackjackResult>();
 public async Task<BlackjackResult> HitAsync(long u,CancellationToken ct)=>(await client.HitAsync(BjWire.Call(new UserCall(u)),cancellationToken:ct)).Read<BlackjackResult>();
 public async Task<BlackjackResult> StandAsync(long u,CancellationToken ct)=>(await client.StandAsync(BjWire.Call(new UserCall(u)),cancellationToken:ct)).Read<BlackjackResult>();
 public async Task<BlackjackResult> DoubleAsync(long u,CancellationToken ct)=>(await client.DoubleAsync(BjWire.Call(new UserCall(u)),cancellationToken:ct)).Read<BlackjackResult>();
 public async Task<BlackjackState> GetStateAsync(long u,CancellationToken ct)=>(await client.GetStateAsync(BjWire.Call(new UserCall(u)),cancellationToken:ct)).Read<BlackjackState>();
 public async Task SetStateMessageIdAsync(long u,int m,CancellationToken ct)=>_ = await client.SetMessageAsync(BjWire.Call(new UserCall(u,m)),cancellationToken:ct);
}
