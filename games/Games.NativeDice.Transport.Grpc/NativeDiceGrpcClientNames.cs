using CasinoShiz.ServiceDefaults;
using Games.Basketball.Application.Services;
using Games.Bowling.Application.Services;
using Games.Darts.Application.Services;
using Games.DiceCube.Application.Services;
using Games.Football.Application.Services;
using Games.NativeDice.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.NativeDice.Transport.Grpc;

public static class NativeDiceGrpcClientNames
{
    public const string DiceCube = "native-dice-dicecube";
    public const string Darts = "native-dice-darts";
    public const string Football = "native-dice-football";
    public const string Basketball = "native-dice-basketball";
    public const string Bowling = "native-dice-bowling";
}
