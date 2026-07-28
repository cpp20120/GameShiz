using System.Reflection;
using System.Text.Json;
using BotFramework.Host.Analytics.Reports;
using Games.Admin.Application.Services;
using Games.Admin.Infrastructure.Persistence;
using Games.Admin.Transport.Grpc.Wire;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Admin.Transport.Grpc;

internal static class AdminWire
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
