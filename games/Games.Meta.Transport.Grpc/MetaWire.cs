using System.Reflection;
using System.Text.Json;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Quests;
using Games.Meta.Application.Risk;
using Games.Meta.Application.Tournaments;
using Games.Meta.Transport.Grpc.Wire;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Meta.Transport.Grpc;

internal static class MetaWire
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
