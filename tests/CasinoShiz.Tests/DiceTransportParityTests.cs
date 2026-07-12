using System.Net;
using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Messaging;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.Dice.Application.Execution;
using Games.Dice.Application.Requests;
using Games.Dice.Application.Services;
using Games.Dice.Contracts.Play;
using Games.Dice.Domain.Results;
using Games.Dice.Infrastructure.Messaging;
using Games.Dice.Infrastructure.Persistence;
using Games.Dice.Transport.Grpc;
using Games.Dice.Transport.Grpc.Wire;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class DiceTransportParityTests(AtomicPostgresFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DicePlayRequest Request = new(42, "parity", 37, 84, "101", false);
    private static readonly RequestMetadata Metadata = RequestMetadata.Create("parity", "42", "84", "ru");

    [Fact]
    public async Task MonolithAndSplitGrpc_ReturnSameResponseAndCommittedEffects()
    {
        await database.ResetAsync();
        await using var monolith = BuildServices();
        await using var monolithScope = monolith.CreateAsyncScope();
        var monolithResponse = await new InProcessDiceClient(
                monolithScope.ServiceProvider.GetRequiredService<IRequestClient>())
            .PlayAsync(Request, Metadata, CancellationToken.None);
        var monolithEffects = await ReadEffectsAsync();

        await database.ResetAsync();
        await using var backend = await StartGrpcBackendAsync();
        using var channel = GrpcChannel.ForAddress(backend.Address);
        var splitResponse = await new GrpcDiceClient(new DiceApi.DiceApiClient(channel))
            .PlayAsync(Request, Metadata, CancellationToken.None);
        var splitEffects = await ReadEffectsAsync();

        Assert.Equal(monolithResponse, splitResponse);
        Assert.Equal(monolithEffects, splitEffects);
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateExecutor());
        services.AddSingleton<IRuntimeTuningAccessor>(new FakeRuntimeTuning
        {
            Dice = new DiceOptions { Cost = 5, RedeemDropChance = 0 },
        });
        services.AddScoped<IDiceService, DiceService>();
        services.AddScoped<IRequestHandler<DicePlayRequest, DicePlayResponse>, DicePlayRequestHandler>();
        services.AddScoped<IRequestClient, LocalRequestClient>();
        return services.BuildServiceProvider();
    }

    private async Task<GrpcBackend> StartGrpcBackendAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddSingleton(CreateExecutor());
        builder.Services.AddSingleton<IRuntimeTuningAccessor>(new FakeRuntimeTuning
        {
            Dice = new DiceOptions { Cost = 5, RedeemDropChance = 0 },
        });
        builder.Services.AddScoped<IDiceService, DiceService>();
        builder.Services.AddScoped<IRequestHandler<DicePlayRequest, DicePlayResponse>, DicePlayRequestHandler>();
        builder.Services.AddScoped<IRequestClient, LocalRequestClient>();
        builder.Services.AddGrpc();

        var app = builder.Build();
        app.MapDiceGrpcTransport();
        await app.StartAsync().ConfigureAwait(false);
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.SingleOrDefault()
            ?? throw new InvalidOperationException("The test gRPC backend did not publish an address.");
        return new GrpcBackend(app, new Uri(address, UriKind.Absolute));
    }

    private IAtomicGameExecutor<DiceCommand, NoGameState, DicePlayResult> CreateExecutor()
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<DiceCommand, NoGameState, DicePlayResult>(
            new PostgresGameExecutionSessionFactory(connections),
            new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(Options.Create(new BotFrameworkOptions { StartingCoins = 100 })),
            new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            new TransactionalEventCollector(new TestEventSerializer()),
            new TestDiceDescriptor(),
            new DiceAction(),
            new DiceStateStore(),
            [new DiceRollRecordWriter()],
            time,
            new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance));
    }

    private Task<string> ReadEffectsAsync() => database.ScalarAsync<string>("""
        SELECT jsonb_build_object(
            'wallet', (SELECT jsonb_build_object('coins', coins, 'version', version) FROM users),
            'ledger', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'delta', delta,
                    'balance_after', balance_after,
                    'reason', reason) ORDER BY id)
                FROM economics_ledger
            ), '[]'::jsonb),
            'quota', (SELECT jsonb_build_object(
                'game_id', game_id,
                'rolls_on', rolls_on,
                'roll_count', roll_count)
                FROM telegram_dice_daily_rolls),
            'history', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'user_id', user_id,
                    'dice_value', dice_value,
                    'prize', prize,
                    'loss', loss,
                    'rolled_at', rolled_at) ORDER BY rolled_at)
                FROM dice_rolls
            ), '[]'::jsonb),
            'inbox', (SELECT jsonb_build_object(
                'key', idempotency_key,
                'status', status,
                'game_id', game_id,
                'aggregate_id', aggregate_id,
                'result_type', result_type,
                'result', result_json,
                'entropy', entropy_json)
                FROM game_command_idempotency),
            'outbox', COALESCE((
                SELECT jsonb_agg(jsonb_build_object(
                    'command_id', command_id,
                    'event_index', event_index,
                    'event_type', event_type,
                    'payload', payload) ORDER BY event_index)
                FROM game_event_outbox
            ), '[]'::jsonb)
        )::text
        """);

    private sealed class TestDiceDescriptor : GameExecutionDescriptor<DiceCommand, NoGameState, DicePlayResult>
    {
        public override string GameId => "dice";
        public override string CommandId(DiceCommand command) => $"dice:roll:{command.ChatId}:{command.SourceMessageId}:{command.UserId}";
        public override string AggregateId(DiceCommand command) => $"{command.ChatId}:{command.UserId}";
        public override long ChatId(DiceCommand command) => command.ChatId;
        public override string DisplayName(DiceCommand command) => command.DisplayName;
        public override WalletIdentity Wallet(DiceCommand command) => new(command.UserId, command.ChatId);
        public override IReadOnlyList<QuotaIdentity> Quotas(DiceCommand command, DateTimeOffset utcNow) =>
            [new(DiceAction.DailyRollQuota, GameId, command.UserId, command.ChatId, DateOnly.FromDateTime(utcNow.UtcDateTime), 10)];
    }

    private sealed class TestConnectionFactory(string connectionString) : INpgsqlConnectionFactory
    {
        public NpgsqlConnection Create() => new(connectionString);

        public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
        {
            var connection = Create();
            await connection.OpenAsync(ct).ConfigureAwait(false);
            return connection;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class TestEventSerializer : IEventSerializer
    {
        public string Serialize(IDomainEvent ev) => JsonSerializer.Serialize(ev, ev.GetType());
        public IDomainEvent? Deserialize(string eventType, string payloadJson) => throw new NotSupportedException();
    }

    private sealed class GrpcBackend(WebApplication application, Uri address) : IAsyncDisposable
    {
        public Uri Address { get; } = address;

        public async ValueTask DisposeAsync()
        {
            await application.StopAsync().ConfigureAwait(false);
            await application.DisposeAsync().ConfigureAwait(false);
        }
    }
}
