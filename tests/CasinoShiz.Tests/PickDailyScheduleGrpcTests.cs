using System.Net;
using Games.Pick.Application.Analytics;
using Games.Pick.Application.Results;
using Games.Pick.Application.Services;
using Games.Pick.Domain.Results;
using Games.Pick.Infrastructure.Persistence;
using Games.Pick.Transport.Grpc;
using Games.Pick.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickDailyScheduleGrpcTests
{
    [Fact]
    public async Task DailySchedule_UsesTypedProtobufContract()
    {
        var expected = new PickDailySchedule(7, 22);
        await using var backend = await StartBackendAsync(expected);
        using var channel = GrpcChannel.ForAddress(backend.Address);

        var actual = await new GrpcPickClient(new PickApi.PickApiClient(channel))
            .GetDailyScheduleAsync(CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    private static async Task<GrpcBackend> StartBackendAsync(PickDailySchedule schedule)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddSingleton<IPickClient>(new FakePickClient(schedule));
        builder.Services.AddGrpc();

        var app = builder.Build();
        app.MapPickGrpcTransport();
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses.SingleOrDefault()
            ?? throw new InvalidOperationException("The test gRPC backend did not publish an address.");
        return new GrpcBackend(app, new Uri(address, UriKind.Absolute));
    }

    private sealed class FakePickClient(PickDailySchedule schedule) : IPickClient
    {
        public Task<PickResult> PickAsync(long userId, string displayName, long chatId, int amount,
            IReadOnlyList<string> variants, IReadOnlyList<int> backedIndices, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PickResult> PickAsync(long userId, string displayName, long chatId, int amount,
            IReadOnlyList<string> variants, IReadOnlyList<int> backedIndices, int sourceMessageId,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<PickResult> ContinueChainAsync(PickChainState chain, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PickChainState?> ClaimChainAsync(Guid chainId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task RestoreChainAsync(PickChainState chain, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<LotteryOpenResult> OpenLotteryAsync(long userId, string displayName, long chatId, int stake,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<LotteryOpenResult> OpenLotteryAsync(long userId, string displayName, long chatId, int stake,
            int sourceMessageId, CancellationToken ct) => throw new NotSupportedException();

        public Task<LotteryJoinResult> JoinLotteryAsync(long userId, string displayName, long chatId,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<LotteryJoinResult> JoinLotteryAsync(long userId, string displayName, long chatId,
            int sourceMessageId, CancellationToken ct) => throw new NotSupportedException();

        public Task<LotteryInfoSnapshot?> LotteryInfoAsync(long chatId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<LotterySettleResult?> CancelLotteryAsync(long openerId, long chatId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<DailyBuyResult> BuyDailyAsync(long userId, string displayName, long chatId, int count,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<DailyBuyResult> BuyDailyAsync(long userId, string displayName, long chatId, int count,
            int sourceMessageId, CancellationToken ct) => throw new NotSupportedException();

        public Task<DailyInfoSnapshot?> DailyInfoAsync(long chatId, long viewerId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PickDailyLotteryRow>> DailyHistoryAsync(long chatId, int limit,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<PickDailySchedule> GetDailyScheduleAsync(CancellationToken ct) =>
            Task.FromResult(schedule);
    }

    private sealed class GrpcBackend(WebApplication application, Uri address) : IAsyncDisposable
    {
        public Uri Address { get; } = address;

        public async ValueTask DisposeAsync()
        {
            await application.StopAsync();
            await application.DisposeAsync();
        }
    }
}
