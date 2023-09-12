using FantacalcioV4.Database;
using Rystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.ApiManager
{
    public class GroupApi
    {
        public async Task<List<string>> ListAsync()
            => (await new StoredGroup().GetAsync()).Select(x => x.PrimaryKey).ToList();
        public async Task<List<League>> GetLeaguesAsync(string groupName)
            => (await new GroupKey { Name = groupName }.InstanceAsync()).Leagues;
        public Task<Team> GetTeamAsync(string groupName, string username, int year)
            => new TeamContext(FantacalcioId.GetTeam(groupName, year, username)).GetAsync();
        public Task SaveTeamAsync(Team team)
            => new TeamContext(team.Id).SaveAsync(team);
        public async Task<bool> AreWeInNewYear(string groupName, int nextYear)
        {
            var group = await new GroupContext(groupName).GetAsync();
            foreach (var league in group.Leagues)
                if (league.Years.Any(x => x.Year == nextYear))
                    return true;
            return false;
        }
        public async Task RecalculateWinningsAsync(string groupName)
        {
            var group = await new GroupContext(groupName).GetAsync();
            foreach (var league in group.Leagues)
            {
                for (int year = 1; year < FantacalcioTime.ActualYear - 1; year++)
                {
                    var annualLeague = league.Years.FirstOrDefault(x => x.Year == year);
                    if (annualLeague != default)
                    {
                        string winnerUsername = string.Empty;
                        if (annualLeague.Type == LeagueType.League)
                        {
                            var rank = await new RankKey() { GroupName = groupName, LeagueName = league.Name, Year = year }.InstanceAsync();
                            winnerUsername = rank.OrderedRankedTeams.FirstOrDefault().FirstOrDefault().Username;
                        }
                        else
                        {
                            var calendar = await new CalendarKey() { GroupName = groupName, LeagueName = league.Name, Year = year }.InstanceAsync();
                            var finalGame = calendar.Days.OrderByDescending(x => x.SerieADay).FirstOrDefault().Games.FirstOrDefault().Value.FirstOrDefault();
                            if (finalGame.Result.Value == GameResultType.HomeWon)
                                winnerUsername = finalGame.HomeUsername;
                            else if (finalGame.Result.Value == GameResultType.AwayWon)
                                winnerUsername = finalGame.AwayUsername;
                            else
                            {
                                if (finalGame.Result.Home.Value > finalGame.Result.Away.Value)
                                    winnerUsername = finalGame.HomeUsername;
                                else if (finalGame.Result.Away.Value > finalGame.Result.Home.Value)
                                    winnerUsername = finalGame.AwayUsername;
                                else
                                {
                                    var rank = await new RankKey { GroupName = groupName, LeagueName = league.Name, Year = year }.InstanceAsync();
                                    double valueA = rank.RankedTeams.FirstOrDefault(x => x.TeamId == finalGame.MiniHome.Id).ValuePoint;
                                    double valueB = rank.RankedTeams.FirstOrDefault(x => x.TeamId == finalGame.MiniAway.Id).ValuePoint;
                                    if (valueA > valueB)
                                        winnerUsername = finalGame.HomeUsername;
                                    else if (valueB > valueA)
                                        winnerUsername = finalGame.AwayUsername;
                                    else
                                        winnerUsername = string.Empty;
                                }
                            }
                        }
                        var identity = await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == winnerUsername);
                        var groupFromIdentity = identity.Groups.FirstOrDefault(x => x.Name == groupName);
                        var teamInYear = groupFromIdentity.Teams.FirstOrDefault(x => x.Year == year);
                        var participation = teamInYear.Participations.FirstOrDefault(x => x.League == league.Name);
                        teamInYear.Participations.Remove(participation);
                        teamInYear.Participations.Add(participation with { Won = true });
                        await identity.SaveAsync();
                    }
                }
            }
        }
        public async Task SubscribePeopleToNewYearAsync(string groupName, int nextYear, Dictionary<string, string> substitutedUsers, bool forceDeletion = false)
        {
            var group = await new GroupContext(groupName).GetAsync();
            if (forceDeletion)
            {
                foreach (var league in group.Leagues)
                    if (league.IsMain)
                        if (league.Years.FirstOrDefault(x => x.Year == nextYear) is not null and AnnualLeague annualLeague)
                        {
                            league.Years.Remove(annualLeague);
                            foreach (var team in annualLeague.Teams)
                            {
                                await RemoveTeamToIdentityAsync(team);
                            }
                        }
            }
            foreach (var league in group.Leagues)
            {
                var lastLeague = league.Years.FirstOrDefault(x => x.Year == nextYear - 1);
                if (league.IsMain)
                {
                    List<Team> newTeams = new();
                    foreach (var team in lastLeague.Teams)
                    {
                        var username = team.Username;
                        var teamName = team.Name;
                        if (substitutedUsers.ContainsKey(username))
                        {
                            username = substitutedUsers[username];
                            teamName = $"{username} Team";
                        }
                        var newTeam = new Team(FantacalcioId.GetTeam(groupName, nextYear, username), new());
                        newTeams.Add(newTeam);
                        await AddTeamToIdentityAsync(newTeam, league.Name, teamName);
                    }
                    league.Years.Add(lastLeague with
                    {
                        Id = FantacalcioId.GetLeague(lastLeague.GroupName, nextYear, lastLeague.Name),
                        Teams = newTeams
                    });
                }
            }
            await new GroupContext(groupName).SaveAsync(group);
            foreach (var league in group.Leagues)
                await new AnnualLeagueKey { GroupName = groupName, LeagueName = league.Name, Year = nextYear }.RestoreAsync();
        }
        private async Task AddTeamToIdentityAsync(Team newYearTeam, string leagueName, string teamName)
        {
            var identity = await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == newYearTeam.Username);
            var group = identity.Groups.FirstOrDefault(x => x.Name == newYearTeam.GroupName);
            if (group == default)
                identity.Groups.Add(group = new TeamByGroup(newYearTeam.GroupName, leagueName, new()));
            var teamInYear = group.Teams.FirstOrDefault(x => x.Year == newYearTeam.Year);
            if (teamInYear == default)
            {
                if (string.IsNullOrWhiteSpace(teamName))
                    teamName = $"{newYearTeam.Username} Team";
                group.Teams.Add(teamInYear = new TeamByYear(newYearTeam.Id, teamName, new()));
            }
            else if (teamInYear.Name != teamName && !string.IsNullOrWhiteSpace(teamName))
            {
                group.Teams.Remove(teamInYear);
                group.Teams.Add(teamInYear = teamInYear with { Name = teamName });
            }
            if (!teamInYear.Participations.Any(x => x.League == leagueName))
                teamInYear.Participations.Add(new ParticipationLeague(leagueName, false));
            await identity.SaveAsync();
            if (!await new TeamContext(newYearTeam.Id).ExistAsync())
                await SaveTeamAsync(newYearTeam);
        }
        private async Task RemoveTeamToIdentityAsync(Team removedTeam)
        {
            var identity = await new StoredIdentity().FirstOrDefaultAsync(x => x.PrimaryKey == removedTeam.Username);
            var group = identity.Groups.FirstOrDefault(x => x.Name == removedTeam.GroupName);
            if (group == default)
                return;
            var teamInYear = group.Teams.FirstOrDefault(x => x.Year == removedTeam.Year);
            if (teamInYear == default)
                return;
            group.Teams.Remove(teamInYear);
            await identity.SaveAsync();
        }
        public async Task CreateNewYearAsync(string groupName, int nextYear, Dictionary<string, string> substitutedUsers)
        {
            var group = await new GroupContext(groupName).GetAsync();
            foreach (var league in group.Leagues)
            {
                var lastLeague = league.Years.OrderByDescending(x => x.Year).FirstOrDefault();
                if (lastLeague.Year < nextYear)
                {
                    if (!league.IsMain)
                    {
                        var newLeague = new AnnualLeague(FantacalcioId.GetLeague(groupName, nextYear, lastLeague.Name),
                        lastLeague.Type, lastLeague.Settings with { }, new());
                        if (lastLeague.Type == LeagueType.League)
                        {
                            foreach (var mainLeague in group.Leagues.Where(x => x.IsMain))
                                foreach (var team in mainLeague.Years.FirstOrDefault(x => x.Year == nextYear).Teams)
                                {
                                    var newTeam = new Team(FantacalcioId.GetTeam(groupName, nextYear, team.Username), new());
                                    newLeague.Teams.Add(newTeam);
                                    await AddTeamToIdentityAsync(newTeam, league.Name, team.Name);
                                }
                        }
                        else if (lastLeague.Type == LeagueType.Cup)
                        {
                            int maxFromTheSide = league.Participant / 2;
                            foreach (var mainLeague in group.Leagues.Where(x => x.IsMain))
                            {
                                var rank = await new RankKey() { GroupName = groupName, LeagueName = mainLeague.Name, Year = nextYear - 1 }.InstanceAsync();
                                foreach (var team in rank.OrderedRankedTeams.SelectMany(x => x)
                                    .Where(x => !substitutedUsers.ContainsKey(x.Username))
                                    .Take(maxFromTheSide))
                                {
                                    var newTeam = new Team(FantacalcioId.GetTeam(groupName, nextYear, team.Username), new());
                                    newLeague.Teams.Add(newTeam);
                                    await AddTeamToIdentityAsync(newTeam, league.Name, team.Name);
                                }
                            }
                        }
                        league.Years.Add(newLeague);
                    }
                }
            }
            await new GroupContext(groupName).SaveAsync(group);
            await new GroupKey { Name = groupName }.RestoreAsync();
            foreach (var league in group.Leagues)
                await new AnnualLeagueKey { GroupName = groupName, LeagueName = league.Name, Year = nextYear }.RestoreAsync();
        }
        public async Task RestoreCalendarAndRankAsync(string groupName, int year)
        {
            var group = await new GroupKey { Name = groupName }.InstanceAsync();
            foreach (var league in group.Leagues)
            {
                if (league.Years.Any(x => x.Year == year))
                {
                    await new CalendarKey() { GroupName = groupName, LeagueName = league.Name, Year = year }.RestoreAsync();
                    await new RankKey() { GroupName = groupName, LeagueName = league.Name, Year = year }.RestoreAsync();
                }
            }
        }
        public async Task AddTieToADayAsync(string groupName, int year, int serieADay, string leagueName = "")
        {
            var group = await new GroupContext(groupName).GetAsync();
            foreach (var league in group.Leagues)
            {
                if (!string.IsNullOrWhiteSpace(leagueName) && league.Name != leagueName)
                    continue;
                var calendar = await new CalendarContext(groupName, year, league.Name).GetAsync();
                var day = calendar.Days.FirstOrDefault(x => x.SerieADay == serieADay);
                if (day != default)
                {
                    foreach (var gameByRound in day.Games)
                    {
                        foreach (var game in gameByRound.Value)
                        {
                            game.Result = new GameResult(new Point(63, false, false), new Point(63, false, false));
                        }
                    }
                }
                await new CalendarContext(groupName, year, league.Name).SaveAsync(calendar);
                await new CalendarKey() { GroupName = group.Name, LeagueName = league.Name, Year = year }.RestoreAsync();
            }
        }
        public async Task RemoveTieToADayAsync(string groupName, int year, int serieADay, string leagueName = "")
        {
            var group = await new GroupContext(groupName).GetAsync();
            foreach (var league in group.Leagues)
            {
                if (!string.IsNullOrWhiteSpace(leagueName) && league.Name != leagueName)
                    continue;
                var calendar = await new CalendarContext(groupName, year, league.Name).GetAsync();
                var day = calendar.Days.FirstOrDefault(x => x.SerieADay == serieADay);
                if (day != default)
                {
                    foreach (var gameByRound in day.Games)
                    {
                        foreach (var game in gameByRound.Value)
                        {
                            game.Result = default;
                        }
                    }
                }
                await new CalendarContext(groupName, year, league.Name).SaveAsync(calendar);
                await new CalendarKey() { GroupName = group.Name, LeagueName = league.Name, Year = year }.RestoreAsync();
            }
        }
    }
}
