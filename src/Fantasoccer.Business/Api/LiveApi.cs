using FantacalcioV4.Database;
using Rystem.Background;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fantasoccer.Business
{
    public class LiveApi
    {
        private readonly GroupApi Group;
        private readonly LeagueApi League;
        private readonly CoreApi Core;
        public LiveApi(GroupApi group, LeagueApi league, CoreApi core)
        {
            Group = group;
            League = league;
            Core = core;
        }
        public async Task<int> GetCurrentSerieADayAsync(int year)
        {
            var serieA = await new RealCalendarKey { Year = year }.InstanceAsync().NoContext();
            int serieADay = serieA.LiveDay?.SerieADay ?? serieA.LastDay?.SerieADay ?? 0;
            return serieADay;
        }
        public async Task<int> GetNextSerieADayAsync(int year)
        {
            var serieA = await new RealCalendarKey { Year = year }.InstanceAsync().NoContext();
            int serieADay = serieA.LiveDay?.SerieADay ?? serieA.NextDay?.SerieADay ?? 0;
            return serieADay;
        }
        public async Task<DateTime> GetNextSerieADayDateStartAsync(int year)
        {
            var serieA = await new RealCalendarKey { Year = year }.InstanceAsync().NoContext();
            return serieA.NextDay?.Games.OrderBy(x => x.Date).First().Date ?? DateTime.UtcNow;
        }
        public Task<ActualLive> GetAllLiveForAGroupAsync(string groupName) 
            => new LiveKey { Group = groupName }.InstanceAsync();
        public async Task<Live> GetDayAsync(string groupName, string leagueName, int year)
        {
            var group = await new GroupKey { Name = groupName }.InstanceAsync().NoContext();
            var settings = group.Leagues.First(x => x.Name == leagueName).Years.FirstOrDefault(x => x.Year == year).Settings;
            var serieA = await new RealCalendarKey { Year = year }.InstanceAsync().NoContext();
            int serieADay = await GetNextSerieADayAsync(year).NoContext();
            if (serieADay == 0)
                return default;
            var daily = await League.GetDayAsync(groupName, leagueName, year, serieADay).NoContext();
            if (daily == default)
                return default;
            var votes = await new FinalVotesContext(year, serieADay).GetAsync().NoContext();
            if (votes == default)
                votes = new List<Player>();
            var liveVotes = await new LiveVotesContext(year, serieADay).GetAsync().NoContext();
            if (liveVotes != default)
                votes = votes.MergeVote(liveVotes);
            if (votes != default && votes.Count > 0)
                daily.SetVote(votes, settings);
            Rank rank = await new RankKey { GroupName = groupName, LeagueName = leagueName, Year = year }.InstanceAsync().NoContext();
            rank.AddDay(daily, settings);
            return new(leagueName, daily, rank, serieA.Days.FirstOrDefault(x => x.SerieADay == serieADay));
        }

        public async Task<PlayedGame> GetGameAsync(string group, string league, int year, string gameId)
        {
            var day = await GetDayAsync(group, league, year).NoContext();
            return day.Calendar.Games.SelectMany(x => x.Value).FirstOrDefault(x => x.Id == gameId);
        }
        public async Task<(PlayedGame Game, RealDay RealDay, bool IsLive)> GetMyGameAsync(string group, string league, int year, string username)
        {
            var day = await GetDayAsync(group, league, year).NoContext();
            var game = day.Calendar.Games.SelectMany(x => x.Value).FirstOrDefault(x => x.HomeUsername == username || x.AwayUsername == username);
            if (game != default)
                return await League.GetGameAsync(game.Id, true);
            else
                return default;
        }
        public async Task<(Team My, Team Enemy, RealDay SerieADay)> GetActualGameAsync(string group, string league, string username, int year)
        {
            var chance = await Core.GetChancesAsync().NoContext();
            var team = await Group.GetTeamAsync(group, username, year).NoContext();
            foreach (var player in team.ActivePlayers)
            {
                if (player.Position == default)
                    player.Position = new Position(-1, -1);
                if (player.Value == default)
                    player.Position = new Position(205, 1055);
                player.Chance = chance.FirstOrDefault(x => x.Key == player.Key)?.Chance ?? Chance.Default();
            }
            var myGame = await GetMyGameAsync(group, league, year, username).NoContext();
            var serieACalendar = await new RealCalendarKey() { Year = year }.InstanceAsync().NoContext();
            return (team, myGame == default ? default : (myGame.Game.HomeUsername == username ? myGame.Game.Away : myGame.Game.Home), serieACalendar.LiveDay ?? serieACalendar.NextDay);
        }
        public async Task<int> IsSerieAPlayingAsync()
            => (await new RealCalendarKey().InstanceAsync().NoContext()).LiveSerieADay;
        public async Task<SaveTeamResponseType> SaveTeamAsync(Team team, string group, string username, int year, bool bypassSaveAndGet, DateTime? forceTime = default)
        {
            if (team == default)
                return SaveTeamResponseType.Empty;
            var myTeam = bypassSaveAndGet ? team : await Group.GetTeamAsync(group, username, year).NoContext();
            if (myTeam.Id == team.Id)
            {
                if (team.ActivePlayers.ExistsRightPlayersInRightPosition())
                {
                    foreach (var player in myTeam.ActivePlayers)
                    {
                        var changedPlayer = team.ActivePlayers.FirstOrDefault(x => x.Key == player.Key);
                        if (changedPlayer != default)
                            player.Position = changedPlayer.Position;
                        else
                            return SaveTeamResponseType.NotExistingPlayer;
                    }
                    foreach (var player in myTeam.ActivePlayers)
                    {
                        player.Chance = default;
                        player.Vote = default;
                    }
                    if (!bypassSaveAndGet)
                        await Group.SaveTeamAsync(myTeam).NoContext();

                    if (year == FantacalcioTime.ActualYear)
                        new ChangedTeam(forceTime.HasValue ? forceTime.Value : FantacalcioTime.ItalyNow, team).Enqueue();
                    else
                        return SaveTeamResponseType.YearNotActual;
                }
                else
                    return SaveTeamResponseType.WrongPosition;
            }
            else
                return SaveTeamResponseType.NotYourTeam;
            return SaveTeamResponseType.Ok;
        }
    }
    public enum SaveTeamResponseType
    {
        Ok,
        Empty,
        WrongPosition,
        NotYourTeam,
        LiveActive,
        NotExistingPlayer,
        YearNotActual
    }
}