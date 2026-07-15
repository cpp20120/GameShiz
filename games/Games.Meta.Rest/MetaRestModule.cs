using BotFramework.Rest;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Quests;
using Games.Meta.Application.Risk;
using Games.Meta.Application.Tournaments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Games.Meta.Rest;

public sealed record MetaClanCreateRequest(string Tag, string Name);
public sealed record MetaClanJoinRequest(string Tag);
public sealed record MetaTournamentCreateRequest(string GameKey, int EntryFee, int MaxPlayers);
public sealed record MetaTournamentReportRequest(long VictorUserId);
public sealed record MetaTournamentFinishRequest(long VictorUserId);
public sealed record MetaRiskStatusRequest(string Status);

public sealed class MetaRestModule : IRestRouteModule
{
    public string ModuleId => "meta";

    public void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapRestGroup(ModuleId);
        group.MapGet("/season", SeasonAsync).WithName("MetaActiveSeason");
        group.MapGet("/profile", ProfileAsync).WithName("MetaProfile");
        group.MapGet("/top", TopAsync).WithName("MetaTop");
        group.MapGet("/achievements", AchievementsAsync).WithName("MetaAchievements");
        group.MapGet("/streaks", StreaksAsync).WithName("MetaStreaks");
        group.MapGet("/quests", QuestsAsync).WithName("MetaQuests");
        group.MapPost("/quests/{questId}/claim", ClaimQuestAsync).WithName("MetaClaimQuest");
        group.MapGet("/clan", UserClanAsync).WithName("MetaUserClan");
        group.MapGet("/clan/by-tag/{tag}", ClanByTagAsync).WithName("MetaClanByTag");
        group.MapGet("/clan/members", ClanMembersAsync).WithName("MetaClanMembers");
        group.MapGet("/clan/top", ClanTopAsync).WithName("MetaClanTop");
        group.MapPost("/clan", CreateClanAsync).WithName("MetaCreateClan");
        group.MapPost("/clan/join", JoinClanAsync).WithName("MetaJoinClan");
        group.MapGet("/tournaments/open", OpenTournamentsAsync).WithName("MetaOpenTournaments");
        group.MapPost("/tournaments", CreateTournamentAsync).WithName("MetaCreateTournament");
        group.MapGet("/tournaments/{tournamentId:long}", TournamentAsync).WithName("MetaTournament");
        group.MapGet("/tournaments/{tournamentId:long}/players", TournamentPlayersAsync).WithName("MetaTournamentPlayers");
        group.MapGet("/tournaments/{tournamentId:long}/matches", TournamentMatchesAsync).WithName("MetaTournamentMatches");
        group.MapPost("/tournaments/{tournamentId:long}/join", JoinTournamentAsync).WithName("MetaJoinTournament");
        group.MapPost("/tournaments/{tournamentId:long}/start", StartTournamentAsync).WithName("MetaStartTournament");
        group.MapPost("/tournaments/matches/{matchId:long}/report", ReportMatchAsync).WithName("MetaReportMatch");
        group.MapPost("/tournaments/{tournamentId:long}/finish", FinishTournamentAsync).WithName("MetaFinishTournament");
        group.MapDelete("/tournaments/{tournamentId:long}", CancelTournamentAsync).WithName("MetaCancelTournament");
        group.MapGet("/risk", OpenRiskAsync).WithName("MetaOpenRisk");
        group.MapPost("/risk/{flagId:long}/status", UpdateRiskAsync).WithName("MetaUpdateRisk");
    }

    private static async Task<IResult> SeasonAsync(IMetaService service, CancellationToken ct) => Results.Ok(await service.GetActiveSeasonAsync(ct).ConfigureAwait(false));

    private static async Task<IResult> ProfileAsync(IMetaService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.GetProfileAsync(RestCommandSupport.ScopeId(context), context.UserId, context.DisplayName, ct).ConfigureAwait(false));

    private static async Task<IResult> TopAsync(int? limit, IMetaService service, RestRequestContext context, CancellationToken ct)
    {
        var actualLimit = limit ?? 15;
        if (actualLimit is < 1 or > 100) throw new RestBadRequestException("limit must be between 1 and 100.");
        return Results.Ok(await service.GetTopAsync(RestCommandSupport.ScopeId(context), actualLimit, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> AchievementsAsync(IMetaService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.GetAchievementsAsync(RestCommandSupport.ScopeId(context), context.UserId, ct).ConfigureAwait(false));

    private static async Task<IResult> StreaksAsync(IMetaService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.GetGameStreaksAsync(RestCommandSupport.ScopeId(context), context.UserId, ct).ConfigureAwait(false));

    private static async Task<IResult> QuestsAsync(IQuestService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.GetQuestsAsync(RestCommandSupport.ScopeId(context), context.UserId, ct).ConfigureAwait(false));

    private static async Task<IResult> ClaimQuestAsync(string questId, IQuestService service, RestRequestContext context, CancellationToken ct)
    {
        RestCommandSupport.RequireText(questId, nameof(questId), 128);
        var result = await service.ClaimAsync(RestCommandSupport.ScopeId(context), context.UserId, context.DisplayName, questId, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> UserClanAsync(IClanService service, RestRequestContext context, CancellationToken ct)
    {
        var result = await service.GetUserClanAsync(RestCommandSupport.ScopeId(context), context.UserId, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ClanByTagAsync(string tag, IClanService service, RestRequestContext context, CancellationToken ct)
    {
        RestCommandSupport.RequireText(tag, nameof(tag), 32);
        var result = await service.GetClanByTagAsync(RestCommandSupport.ScopeId(context), tag, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ClanMembersAsync(long clanId, IClanService service, CancellationToken ct) =>
        Results.Ok(await service.GetMembersAsync(clanId, ct).ConfigureAwait(false));

    private static async Task<IResult> ClanTopAsync(int? limit, IClanService service, RestRequestContext context, CancellationToken ct)
    {
        var actualLimit = limit ?? 15;
        if (actualLimit is < 1 or > 100) throw new RestBadRequestException("limit must be between 1 and 100.");
        return Results.Ok(await service.GetTopAsync(RestCommandSupport.ScopeId(context), actualLimit, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CreateClanAsync(MetaClanCreateRequest request, IClanService service, RestRequestContext context, CancellationToken ct)
    {
        RestCommandSupport.RequireText(request.Tag, nameof(request.Tag), 16);
        RestCommandSupport.RequireText(request.Name, nameof(request.Name), 128);
        return Results.Ok(await service.CreateAsync(RestCommandSupport.ScopeId(context), context.UserId, context.DisplayName, request.Tag, request.Name, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> JoinClanAsync(MetaClanJoinRequest request, IClanService service, RestRequestContext context, CancellationToken ct)
    {
        RestCommandSupport.RequireText(request.Tag, nameof(request.Tag), 16);
        return Results.Ok(await service.JoinAsync(RestCommandSupport.ScopeId(context), context.UserId, context.DisplayName, request.Tag, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> OpenTournamentsAsync(int? limit, ITournamentService service, RestRequestContext context, CancellationToken ct)
    {
        var actualLimit = limit ?? 25;
        if (actualLimit is < 1 or > 100) throw new RestBadRequestException("limit must be between 1 and 100.");
        return Results.Ok(await service.GetOpenAsync(RestCommandSupport.ScopeId(context), actualLimit, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> CreateTournamentAsync(MetaTournamentCreateRequest request, ITournamentService service, RestRequestContext context, CancellationToken ct)
    {
        RestCommandSupport.RequireText(request.GameKey, nameof(request.GameKey), 64);
        if (request.EntryFee < 0 || request.MaxPlayers is < 2 or > 256) throw new RestBadRequestException("Tournament limits are invalid.");
        return Results.Ok(await service.CreateAsync(RestCommandSupport.ScopeId(context), context.UserId, request.GameKey, request.EntryFee, request.MaxPlayers, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> TournamentAsync(long tournamentId, ITournamentService service, CancellationToken ct)
    {
        var result = await service.GetAsync(tournamentId, ct).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> TournamentPlayersAsync(long tournamentId, ITournamentService service, CancellationToken ct) =>
        Results.Ok(await service.GetPlayersAsync(tournamentId, ct).ConfigureAwait(false));

    private static async Task<IResult> TournamentMatchesAsync(long tournamentId, ITournamentService service, CancellationToken ct) =>
        Results.Ok(await service.GetMatchesAsync(tournamentId, ct).ConfigureAwait(false));

    private static async Task<IResult> JoinTournamentAsync(long tournamentId, ITournamentService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.JoinAsync(tournamentId, context.UserId, RestCommandSupport.ScopeId(context), context.DisplayName, ct).ConfigureAwait(false));

    private static async Task<IResult> StartTournamentAsync(long tournamentId, ITournamentService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.StartAsync(tournamentId, context.UserId, ct).ConfigureAwait(false));

    private static async Task<IResult> ReportMatchAsync(long matchId, MetaTournamentReportRequest request, ITournamentService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.ReportMatchAsync(matchId, context.UserId, request.VictorUserId, ct).ConfigureAwait(false));

    private static async Task<IResult> FinishTournamentAsync(long tournamentId, MetaTournamentFinishRequest request, ITournamentService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.FinishAsync(tournamentId, context.UserId, request.VictorUserId, ct).ConfigureAwait(false));

    private static async Task<IResult> CancelTournamentAsync(long tournamentId, ITournamentService service, RestRequestContext context, CancellationToken ct) =>
        Results.Ok(await service.CancelAsync(tournamentId, context.UserId, ct).ConfigureAwait(false));

    private static async Task<IResult> OpenRiskAsync(int? limit, IRiskService service, RestRequestContext context, CancellationToken ct)
    {
        var actualLimit = limit ?? 50;
        if (actualLimit is < 1 or > 200) throw new RestBadRequestException("limit must be between 1 and 200.");
        return Results.Ok(await service.GetOpenAsync(RestCommandSupport.ScopeId(context), actualLimit, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> UpdateRiskAsync(long flagId, MetaRiskStatusRequest request, IRiskService service, CancellationToken ct)
    {
        RestCommandSupport.RequireText(request.Status, nameof(request.Status), 32);
        return Results.Ok(await service.UpdateStatusAsync(flagId, request.Status, ct).ConfigureAwait(false));
    }
}

public static class MetaRestServiceCollectionExtensions
{
    public static IServiceCollection AddMetaRest(this IServiceCollection services) => services.AddRestRouteModule<MetaRestModule>();
}
