using System.Security.Claims;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fantasoccer.Application.Core.Groups;

public sealed record GroupAccessRequirement(bool Manage) : IAuthorizationRequirement;

public sealed class GroupAccessHandler(FantasoccerDbContext database)
    : AuthorizationHandler<GroupAccessRequirement, Guid>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        GroupAccessRequirement requirement, Guid groupId)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var email = context.User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        if (email is null) return;
        var roles = await database.GroupMemberships.Where(x => x.GroupId == groupId && x.User.Email == email && x.User.IsEnabled)
            .Select(x => x.Roles).SingleOrDefaultAsync();
        // Global administration does not implicitly reveal every group's private data.
        if (roles != GroupRole.None && (!requirement.Manage || (roles & (GroupRole.Admin | GroupRole.SuperAdmin)) != 0))
            context.Succeed(requirement);
    }
}

public static class GroupServiceRegistration
{
    public static IServiceCollection AddGroupApplication(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("workspace-member", policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new GroupAccessRequirement(false)));
            options.AddPolicy("workspace-manager", policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new GroupAccessRequirement(true)));
        });
        services.AddScoped<IAuthorizationHandler, GroupAccessHandler>();
        services.AddScoped<GroupApplicationService>();
        services.AddScoped<GroupCreationApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Accounts.PlatformApplicationService>();
        services.AddScoped<GameGroupApplicationService>();
        services.TryAddSingleton(TimeProvider.System);
        // Shared cache layers: L1 IMemoryCache per instance, L2 IDistributedCache
        // (+ coordinator) registered by the host (Azure Blob, or in-memory for tests).
        services.AddMemoryCache();
        services.AddOptions<Fantasoccer.Application.Core.Caching.SharedCacheOptions>();
        services.TryAddSingleton<Fantasoccer.Application.Core.Caching.SharedSnapshotCache>();
        services.TryAddSingleton<Fantasoccer.Application.Core.Football.IFootballLiveDataStore, Fantasoccer.Application.Core.Football.FootballLiveDataStore>();
        services.AddScoped<Fantasoccer.Application.Core.Teams.TeamRosterApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Teams.CompetitionTeamResolver>();
        services.AddScoped<Fantasoccer.Application.Core.Competitions.LeagueLineupApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Competitions.CompetitionParticipationApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Football.FootballApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Football.FootballIngestionApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Football.PlayerImagesApplicationService>();
        services.AddScoped<Fantasoccer.Business.IFootballMatchdayReader, Fantasoccer.Application.Core.Football.SqlFootballMatchdayReader>();
        services.AddScoped<Fantasoccer.Business.IFormationStatisticsReader, Fantasoccer.Application.Core.Football.SqlFormationStatisticsReader>();
        services.AddScoped<Fantasoccer.Business.IFootballIngestionReader, Fantasoccer.Application.Core.Football.SqlFootballIngestionReader>();
        services.AddScoped<Fantasoccer.Application.Core.Competitions.CompetitionApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Competitions.GameApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Competitions.CompetitionLifecycleApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Competitions.LiveCompetitionApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Competitions.OpeningCompetitionApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Competitions.PlayerOrderApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Market.MarketApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Auctions.AuctionApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Teams.TeamCreditsApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Teams.TeamRosterAvailabilityService>();
        services.AddScoped<Fantasoccer.Application.Core.Teams.FormationAutomationService>();
        services.AddScoped<Fantasoccer.Application.Core.HallOfFame.HallOfFameApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.HallOfFame.HallRefreshApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Editorial.EditorialApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Operations.OperationsApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Operations.SqlJobApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Operations.ISqlJobActions, Fantasoccer.Application.Core.Operations.SqlJobActions>();
        services.AddOptions<Fantasoccer.Application.Core.Operations.JobMaintenanceOptions>()
            .Validate(x => x.HallBatchSize is >= 1 and <= 1000, "Hall batches must contain between one and 1000 leagues.");
        services.AddScoped<Fantasoccer.Application.Core.Cards.CardApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Notifications.NotificationSettingsApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Notifications.PushOutboxApplicationService>();
        services.AddScoped<Fantasoccer.Application.Core.Notifications.PushDispatcher>();
        services.AddScoped<Fantasoccer.Application.Core.Notifications.MarketPushProducer>();
        services.AddScoped<Fantasoccer.Application.Core.Notifications.MatchdayPushProducer>();
        services.AddOptions<Fantasoccer.Application.Core.Notifications.PushRetentionOptions>();
        services.AddScoped<Fantasoccer.Application.Core.Notifications.PushRetentionService>();
        services.AddScoped<Fantasoccer.Application.Core.Accounts.AccountApplicationService>();
        return services;
    }
}
