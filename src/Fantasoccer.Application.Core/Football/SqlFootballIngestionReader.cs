using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Football;

public sealed class SqlFootballIngestionReader(FantasoccerDbContext database) : IFootballIngestionReader
{
    public async Task<RealCalendar?> CalendarAsync(int year)
    {
        var rows = await database.FootballMatchdays.AsNoTracking().Include(x => x.Matches).ThenInclude(x => x.HomeClub).Include(x => x.Matches).ThenInclude(x => x.AwayClub)
            .Where(x => x.SeasonIndex == year).OrderBy(x => x.SerieADay).AsSplitQuery().ToArrayAsync();
        if (rows.Length == 0) return null;
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        return new() { Year = year, Days = rows.Select(day => new RealDay { Year = year, SerieADay = day.SerieADay, Games = day.Matches.OrderBy(x => x.Ordinal).Select(x => new RealGame
        { Home = FootballMapping.DomainClub(x.HomeClub), Away = FootballMapping.DomainClub(x.AwayClub), Date = x.KickoffUtc is { } kickoff ? TimeZoneInfo.ConvertTime(kickoff, zone).DateTime : null,
            HomeGoal = x.HomeGoals, AwayGoal = x.AwayGoals, Delayed = x.Delayed }).ToList() }).ToList() };
    }
    public async Task<RealPlayersWrapper?> PlayersAsync(int year)
    {
        var rows = await database.PlayerSeasons.AsNoTracking().Include(x => x.Player).Include(x => x.Club).Where(x => x.SeasonIndex == year).ToArrayAsync();
        return rows.Length == 0 ? null : new() { Players = rows.Select(x => new RealPlayer { PlayerId = x.PlayerId, Name = x.Player.Name, RealTeam = x.Club == null ? null! : FootballMapping.DomainClub(x.Club),
            Role = (Role)x.Position, IsActive = x.IsActive, Visible = x.IsVisible }).ToList() };
    }
    public async Task<RealTeamWrapper?> ClubsAsync(int year)
    {
        var rows = await database.ClubSeasons.AsNoTracking().Include(x => x.Club).Where(x => x.SeasonIndex == year).OrderBy(x => x.Club.Name).ToArrayAsync();
        return new() { Teams = rows.Select(x => FootballMapping.DomainClub(x.Club)).ToList() };
    }
}
