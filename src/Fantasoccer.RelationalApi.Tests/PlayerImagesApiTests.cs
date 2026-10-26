using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Business.Services;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task SQL_player_image_refresh_resolves_UUIDs_on_server_checks_current_ownership_and_returns_partial_batch_failures()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        using var member = await factory.AccountAsync();
        using var outsider = await factory.AccountAsync();
        var group = await CreateGroupAsync(owner); var year = FantacalcioTime.ActualYear;
        await CreateLeagueAsync(owner, group.GroupId, year);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var reader = (await member.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await owner.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(reader.Email, "Image reader", 2))).EnsureSuccessStatusCode();
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var created = await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(graph.Baskets[0].Id, year, "Image team", account.Id, []));
        created.EnsureSuccessStatusCode(); var team = (await created.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        var ids = new List<Guid>();
        await using (var database = factory.Database.CreateContext())
        {
            var club = new FootballClub { Name = "Image HTTP club", Abbreviation = "ihc" }; database.Clubs.Add(club);
            var roster = await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season);
            for (var i = 0; i < 2; i++)
            {
                var player = new FootballPlayer { Name = "Image HTTP " + Guid.NewGuid().ToString("N") }; database.Players.Add(player);
                database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = year, Position = PlayerPosition.Forward });
                roster.Players.Add(new() { Ordinal = i, Player = player, Club = club, PlayerRole = 3 });
                await database.SaveChangesAsync(); ids.Add(player.Id);
            }
        }
        var source = new StubImages();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IPlayerImageRefresher>(source)));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Workspace-User", account.Email);
        var request = new RefreshPlayerImageRequest(group.GroupId, year, ids[0]);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync("/api/player-images/refresh", request)).StatusCode);
        var refreshed = await client.PostAsJsonAsync("/api/player-images/refresh", request);
        refreshed.EnsureSuccessStatusCode();
        Assert.Equal(ids[0], (await refreshed.Content.ReadFromJsonAsync<RefreshedPlayerImageDto>())!.PlayerId);
        Assert.Equal(1, source.SingleCalls);
        client.DefaultRequestHeaders.Remove("X-Workspace-User"); client.DefaultRequestHeaders.Add("X-Workspace-User", reader.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/player-images/refresh-team", new RefreshTeamImagesRequest(group.GroupId, year, team))).StatusCode);
        Assert.Equal(0, source.BatchCalls);
        client.DefaultRequestHeaders.Remove("X-Workspace-User"); client.DefaultRequestHeaders.Add("X-Workspace-User", account.Email);
        var batch = await client.PostAsJsonAsync("/api/player-images/refresh-team", new RefreshTeamImagesRequest(group.GroupId, year, team));
        batch.EnsureSuccessStatusCode(); var summary = (await batch.Content.ReadFromJsonAsync<TeamImagesRefreshedDto>())!;
        Assert.Equal(2, summary.Total); Assert.Single(summary.Succeeded); Assert.Single(summary.Failed);
        Assert.Equal(ids.Order(), summary.Succeeded.Select(x => x.PlayerId).Concat(summary.Failed.Select(x => x.PlayerId)).Order());
    }
    private sealed class StubImages : IPlayerImageRefresher
    {
        public int SingleCalls { get; private set; }
        public int BatchCalls { get; private set; }
        public Task<bool> ReloadAsync(RealPlayer player, CancellationToken cancellationToken) { SingleCalls++; return Task.FromResult(true); }
        public Task<IReadOnlyList<PlayerImageReloadResult>> ReloadManyAsync(IReadOnlyCollection<RealPlayer> players, CancellationToken cancellationToken)
        { BatchCalls++; return Task.FromResult<IReadOnlyList<PlayerImageReloadResult>>(players.Select((x, index) => new PlayerImageReloadResult(x.Key, x.Name, index == 0, index == 0 ? null : "Synthetic provider unavailable")).ToArray()); }
        public Task DownloadMissingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
