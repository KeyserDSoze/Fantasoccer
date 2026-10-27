using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Club_HTTP_uses_one_official_global_name_and_only_attested_year_memberships_without_inventing_roster_presence()
    {
        using var reader = await factory.AccountAsync();
        await using (var database = factory.Database.CreateContext())
        {
            var club = await database.Clubs.SingleOrDefaultAsync(x => x.Id == "juv");
            if (club is null) database.Clubs.Add(club = new() { Id = "juv", Name = "JUVE", Abbreviation = "JUV" });
            var historical = await database.Clubs.SingleOrDefaultAsync(x => x.Id == "cat");
            if (historical is null) database.Clubs.Add(historical = new() { Id = "cat", Name = "CATANIA", Abbreviation = "CAT" });
            database.Seasons.AddRange(new() { SeasonIndex = 61, StartYear = 2072 }, new() { SeasonIndex = 62, StartYear = 2073 });
            database.ClubSeasons.AddRange(new() { Club = club, SeasonIndex = 61, Sources = ClubMembershipSource.Catalogue },
                new() { Club = club, SeasonIndex = 62, Sources = ClubMembershipSource.Calendar });
            database.FootballDatasets.Add(new() { Kind = FootballClubSeason.CatalogueDatasetKind, SeasonIndex = 61 });
            database.PlayerSeasons.Add(new() { Player = new() { Name = "Canonical club history player" }, Club = historical, SeasonIndex = 62,
                Position = PlayerPosition.Forward, IsActive = false });
            await database.SaveChangesAsync();
        }
        var all = (await reader.GetFromJsonAsync<ClubDto[]>("/api/football/clubs"))!;
        var global = Assert.Single(all.Where(x => x.ClubCode == "juv"));
        Assert.Equal("Juventus", global.Name); Assert.Equal("juv", global.Abbreviation); Assert.Equal(global.ClubCode, global.ClubId);
        foreach (var key in new[] { "juv", "JUV", "Juve", "JUVENTUS" })
        {
            var history = (await reader.GetFromJsonAsync<ClubHistoryDto>("/api/football/clubs/by-key/" + key))!;
            Assert.Equal(global, history.Club); Assert.Contains(61, history.Years); Assert.Contains(62, history.Years);
        }
        var first = (await reader.GetFromJsonAsync<ClubSeasonCatalogueDto>("/api/football/61/clubs"))!;
        Assert.True(first.IsComplete); Assert.Equal(61, first.SeasonIndex); Assert.Equal(global, Assert.Single(first.Teams));
        var second = (await reader.GetFromJsonAsync<ClubSeasonCatalogueDto>("/api/football/62/clubs"))!;
        Assert.False(second.IsComplete); Assert.Equal(global, Assert.Single(second.Teams));
        var catania = (await reader.GetFromJsonAsync<ClubHistoryDto>("/api/football/clubs/by-key/cat"))!;
        Assert.Equal("Catania", catania.Club.Name); Assert.DoesNotContain(62, catania.Years);
        var player = Assert.Single((await reader.GetFromJsonAsync<PlayersDto<FootballPlayerDto>>("/api/football/62/players"))!.Players);
        Assert.Equal("cat", player.RealTeam!.ClubCode); Assert.Equal("Catania", player.RealTeam.Name);
        Assert.Equal(HttpStatusCode.NoContent, (await reader.GetAsync("/api/football/63/clubs")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.GetAsync("/api/football/0/clubs")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.GetAsync("/api/football/clubs/by-key/not-a-club")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/football/clubs")).StatusCode);
    }
}
