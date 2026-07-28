using System.Text.Json;

namespace CasinoShiz.Wallet.Transport.Grpc;

internal static class WalletWireCodec
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}