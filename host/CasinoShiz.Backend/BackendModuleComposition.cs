using BotFramework.Host.Composition.Builder;
using Games.Admin.Infrastructure.Modules;
using Games.Admin.Transport.Grpc;
using Games.Basketball.Infrastructure.Modules;
using Games.Bowling.Infrastructure.Modules;
using Games.Blackjack.Infrastructure.Modules;
using Games.Blackjack.Transport.Grpc;
using Games.Challenges.Infrastructure.Modules;
using Games.Challenges.Transport.Grpc;
using Games.Dice.Infrastructure.Modules;
using Games.Dice.Transport.Grpc;
using Games.DiceCube.Infrastructure.Modules;
using Games.Darts.Infrastructure.Modules;
using Games.Football.Infrastructure.Modules;
using Games.Horse.Infrastructure.Modules;
using Games.Horse.Transport.Grpc;
using Games.Leaderboard.Infrastructure.Modules;
using Games.Leaderboard.Transport.Grpc;
using Games.Meta.Infrastructure.Modules;
using Games.Meta.Transport.Grpc;
using Games.NativeDice.Transport.Grpc;
using Games.Pick.Infrastructure.Modules;
using Games.Pick.Transport.Grpc;
using Games.PixelBattle.Infrastructure.Modules;
using Games.PixelBattle.Infrastructure.Integrations;
using Games.PixelBattle.Transport.Grpc;
using Games.Poker.Infrastructure.Modules;
using Games.Poker.Transport.Grpc;
using Games.Redeem.Infrastructure.Modules;
using Games.Redeem.Transport.Grpc;
using Games.SecretHitler.Infrastructure.Modules;
using Games.SecretHitler.Transport.Grpc;
using Games.Transfer.Infrastructure.Modules;
using Games.Transfer.Transport.Grpc;
using CasinoShiz.Operations.Transport.Grpc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace CasinoShiz.Backend;

/// <summary>
/// Keeps one backend image deployable as either the legacy all-games backend or
/// a game-owned worker. Kubernetes/Compose select the composition with
/// Backend:Modules; no source change is required for a new replica count.
/// </summary>
internal static class BackendModuleComposition
{
    private static readonly string[] AllModules =
    [
        "dice", "dicecube", "darts", "football", "basketball", "bowling",
        "transfer", "redeem", "leaderboard", "pixelbattle", "pick", "blackjack",
        "horse", "challenges", "poker", "secrethitler", "meta", "admin",
    ];

    public static IReadOnlySet<string> Resolve(IConfiguration configuration)
    {
        var raw = configuration["Backend:Modules"];
        var values = string.IsNullOrWhiteSpace(raw)
            ? configuration.GetSection("Backend:Modules").GetChildren().Select(x => x.Value).OfType<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var requested = values
            .Select(Normalize)
            .Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requested.Count == 0 || requested.Contains("all"))
            return AllModules.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = requested.Except(AllModules, StringComparer.OrdinalIgnoreCase).ToArray();
        if (unknown.Length > 0)
            throw new InvalidOperationException(
                $"Backend:Modules contains unknown module(s): {string.Join(", ", unknown)}. "
                + $"Known modules: {string.Join(", ", AllModules)}.");

        // The admin Razor pages are hosted by the admin backend but use the
        // meta and horse application services directly. Keep the deployment
        // config concise while ensuring those page dependencies are present.
        if (requested.Contains("admin"))
        {
            requested.Add("meta");
            requested.Add("horse");
        }

        return requested;
    }

    public static IBotFrameworkBuilder AddSelectedModules(
        this IBotFrameworkBuilder framework,
        IReadOnlySet<string> modules)
    {
        if (modules.Contains("dice")) framework.AddModule<DiceModule>();
        if (modules.Contains("dicecube")) framework.AddModule<DiceCubeModule>();
        if (modules.Contains("darts")) framework.AddModule<DartsRemoteModule>();
        if (modules.Contains("football")) framework.AddModule<FootballModule>();
        if (modules.Contains("basketball")) framework.AddModule<BasketballModule>();
        if (modules.Contains("bowling")) framework.AddModule<BowlingModule>();
        if (modules.Contains("transfer")) framework.AddModule<TransferModule>();
        if (modules.Contains("redeem")) framework.AddModule<RedeemModule>();
        if (modules.Contains("leaderboard")) framework.AddModule<LeaderboardModule>();
        if (modules.Contains("pixelbattle")) framework.AddModule<PixelBattleModule>();
        if (modules.Contains("pick")) framework.AddModule<PickModule>();
        if (modules.Contains("blackjack")) framework.AddModule<BlackjackModule>();
        if (modules.Contains("horse")) framework.AddModule<HorseModule>();
        if (modules.Contains("challenges")) framework.AddModule<ChallengeModule>();
        if (modules.Contains("poker")) framework.AddModule<PokerModule>();
        if (modules.Contains("secrethitler")) framework.AddModule<SecretHitlerModule>();
        if (modules.Contains("meta")) framework.AddModule<MetaModule>();
        if (modules.Contains("admin")) framework.AddModule<AdminModule>();
        return framework;
    }

    public static void MapSelectedTransports(
        this WebApplication app,
        IReadOnlySet<string> modules)
    {
        if (modules.Contains("dice")) app.MapDiceGrpcTransport();
        if (modules.Any(x => x is "dicecube" or "darts" or "football" or "basketball" or "bowling"))
            app.MapNativeDiceGrpcTransport();
        if (modules.Contains("transfer")) app.MapTransferGrpcTransport();
        if (modules.Contains("redeem")) app.MapRedeemGrpcTransport();
        if (modules.Contains("leaderboard")) app.MapLeaderboardGrpcTransport();
        if (modules.Contains("pixelbattle"))
        {
            app.MapPixelBattle();
            app.MapPixelBattleGrpcTransport();
        }
        if (modules.Contains("pick")) app.MapPickGrpcTransport();
        if (modules.Contains("blackjack")) app.MapBlackjackGrpcTransport();
        if (modules.Contains("horse")) app.MapHorseGrpcTransport();
        if (modules.Contains("challenges")) app.MapChallengeGrpcTransport();
        if (modules.Contains("poker")) app.MapPokerGrpcTransport();
        if (modules.Contains("secrethitler")) app.MapSecretHitlerGrpcTransport();
        if (modules.Contains("meta")) app.MapMetaGrpcTransport();
        if (modules.Contains("admin"))
        {
            app.MapAdminGrpcTransport();
            app.MapOperationsGrpcTransport();
        }
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant() switch
    {
        "sh" or "secret-hitler" or "secret_hitler" => "secrethitler",
        "challenge" => "challenges",
        "pixel-battle" or "pixel_battle" => "pixelbattle",
        _ => value.Trim().ToLowerInvariant(),
    };
}
