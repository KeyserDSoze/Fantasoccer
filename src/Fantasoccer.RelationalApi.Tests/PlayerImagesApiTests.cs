using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task SQL_player_image_refresh_preserves_natural_keys_and_legacy_image_aliases_checks_ownership_and_returns_partial_failures()
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
        var ids = new List<string>();
        var sharedName = "Image HTTP " + Guid.NewGuid().ToString("N");
        await using (var database = factory.Database.CreateContext())
        {
            var club = new FootballClub { Name = "Image HTTP club", Abbreviation = "ihc" }; database.Clubs.Add(club);
            var roster = await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season);
            for (var i = 0; i < 2; i++)
            {
                var player = new FootballPlayer { Id = i == 0 ? "recordedimagea" : "recordedimageb", Name = sharedName }; database.Players.Add(player);
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
        var single = (await refreshed.Content.ReadFromJsonAsync<RefreshedPlayerImageDto>())!;
        Assert.Equal(ids[0], single.PlayerId); Assert.Equal(single.PlayerId, single.PlayerKey);
        Assert.Equal(1, source.SingleCalls);
        client.DefaultRequestHeaders.Remove("X-Workspace-User"); client.DefaultRequestHeaders.Add("X-Workspace-User", reader.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/player-images/refresh-team", new RefreshTeamImagesRequest(group.GroupId, year, team))).StatusCode);
        Assert.Equal(0, source.BatchCalls);
        client.DefaultRequestHeaders.Remove("X-Workspace-User"); client.DefaultRequestHeaders.Add("X-Workspace-User", account.Email);
        var batch = await client.PostAsJsonAsync("/api/player-images/refresh-team", new RefreshTeamImagesRequest(group.GroupId, year, team));
        batch.EnsureSuccessStatusCode(); var summary = (await batch.Content.ReadFromJsonAsync<TeamImagesRefreshedDto>())!;
        Assert.Equal(2, summary.Total); Assert.Single(summary.Succeeded); Assert.Single(summary.Failed);
        Assert.Equal(ids.Order(), summary.Succeeded.Select(x => x.PlayerId).Concat(summary.Failed.Select(x => x.PlayerId)).Order());
        Assert.All(summary.Succeeded, row => Assert.Equal(row.PlayerId, row.PlayerKey));
        Assert.All(summary.Failed, row => Assert.Equal(row.PlayerId, row.PlayerKey));
    }
    private sealed class StubImages : IPlayerImageRefresher
    {
        public int SingleCalls { get; private set; }
        public int BatchCalls { get; private set; }
        public Task<bool> ReloadAsync(RealPlayer player, CancellationToken cancellationToken) { SingleCalls++; return Task.FromResult(true); }
        public Task<IReadOnlyList<PlayerImageReloadResult>> ReloadManyAsync(IReadOnlyCollection<RealPlayer> players, CancellationToken cancellationToken)
        { BatchCalls++; return Task.FromResult<IReadOnlyList<PlayerImageReloadResult>>(players.Select((x, index) => new PlayerImageReloadResult(x.PlayerId!, x.PlayerId!, x.Name, index == 0, index == 0 ? null : "Synthetic provider unavailable")).ToArray()); }
        public Task DownloadMissingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [PostgreSqlFact]
    public async Task Image_refresh_distinguishes_disabled_external_HTTP_from_provider_failure_and_runs_the_real_downloader_when_enabled()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        var group = await CreateGroupAsync(owner); var year = FantacalcioTime.ActualYear;
        await CreateLeagueAsync(owner, group.GroupId, year);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var created = await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(graph.Baskets[0].Id, year, "Refresh probe", account.Id, []));
        created.EnsureSuccessStatusCode();
        var teamId = (await created.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        string playerId, playerName;
        await using (var database = factory.Database.CreateContext())
        {
            var club = new FootballClub { Name = "Refresh test club", Abbreviation = "rfc" };
            var player = new FootballPlayer { Name = "Refresh Photo " + Guid.NewGuid().ToString("N") };
            database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = year, Position = PlayerPosition.Forward, IsActive = true });
            var roster = await database.RosterSnapshots.SingleAsync(x => x.TeamId == teamId && x.Kind == RosterKind.Season);
            roster.Players.Add(new() { Ordinal = 0, Player = player, Club = club, PlayerRole = 3, Status = 0, IsActive = true });
            await database.SaveChangesAsync(); playerId = player.Id; playerName = player.Name;
        }
        var request = new RefreshPlayerImageRequest(group.GroupId, year, playerId);
        var store = new RecordedImageStore();
        using (var disabled = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ExternalHttp:Enabled", "false");
            builder.ConfigureServices(services => services.AddSingleton<IPlayerImageStore>(store));
        }))
        {
            using var client = disabled.CreateClient(); client.DefaultRequestHeaders.Add("X-Workspace-User", account.Email);
            // This used to report the user's exact generic 502, even though no provider was contacted.
            foreach (var response in new[]
            {
                await client.PostAsJsonAsync("/api/player-images/refresh", request),
                await client.PostAsJsonAsync("/api/player-images/refresh-team", new RefreshTeamImagesRequest(group.GroupId, year, teamId))
            })
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                var error = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("external_http_disabled", error.GetProperty("code").GetString());
                Assert.Contains("EXTERNAL_HTTP_ENABLED=true", error.GetProperty("detail").GetString());
            }
            Assert.Empty(store.Writes);
        }
        var source = new ImageSourceHandler(playerName);
        using var enabled = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ExternalHttp:Enabled", "true");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IPlayerImageStore>(store);
                services.AddHttpClient("player-images").ConfigurePrimaryHttpMessageHandler(() => source);
            });
        });
        using var live = enabled.CreateClient(); live.DefaultRequestHeaders.Add("X-Workspace-User", account.Email);
        var success = await live.PostAsJsonAsync("/api/player-images/refresh", request);
        Assert.True(success.IsSuccessStatusCode, await success.Content.ReadAsStringAsync());
        Assert.Equal($"players/{playerId}.jpg", Assert.Single(store.Writes));
        Assert.False(string.IsNullOrEmpty((await success.Content.ReadFromJsonAsync<RefreshedPlayerImageDto>())!.Revision));
        // A genuine provider failure still has its own code, and never overwrites the existing image.
        source.Fail = true;
        var failure = await live.PostAsJsonAsync("/api/player-images/refresh", request);
        Assert.Equal(HttpStatusCode.BadGateway, failure.StatusCode);
        Assert.Equal("image_source_unavailable", (await failure.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Single(store.Writes);
    }

    private sealed class RecordedImageStore : IPlayerImageStore
    {
        public List<string> Writes { get; } = [];
        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Writes.Contains(name));
        public Task UploadAsync(string name, byte[] image, string contentType, string cacheControl, CancellationToken cancellationToken)
        { Writes.Add(name); return Task.CompletedTask; }
    }
    private sealed class ImageSourceHandler(string name) : HttpMessageHandler
    {
        public bool Fail { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            HttpContent content;
            if (request.RequestUri!.AbsolutePath.Contains("/competitions/")) content = JsonContent.Create(new
            {
                seasons = new[] { new { seasonId = "current", seasonName = FantacalcioTime.LabelYear(FantacalcioTime.ActualYear) } }
            });
            else if (request.RequestUri.Host == "media-sdp.legaseriea.it")
            {
                content = new ByteArrayContent([1, 2, 3]); content.Headers.ContentType = new("image/webp");
            }
            else content = JsonContent.Create(new
            {
                pagination = new { totalPages = 1, currentPage = 1, isLastPage = true },
                players = new[] { new { displayName = name, shortName = name, role = 4, team = new { officialName = "Refresh test club" },
                    imagery = new { playerImage_home_middle = "playerImages/photo.webp" } } }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
