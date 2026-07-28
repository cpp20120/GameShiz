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
