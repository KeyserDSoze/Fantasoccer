using FantacalcioV4.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fantasoccer.Business
{
    public class LeagueApi
    {
        private readonly GroupApi Group;
        private readonly CoreApi Core;
        public LeagueApi(GroupApi group, CoreApi core)
        {
            Group = group;
            Core = core;
        }
        public Task<Calendar> GetCalendarAsync(string groupName, string leagueName, int year)
            => new CalendarKey
            {
                GroupName = groupName,
                LeagueName = leagueName,
                Year = year
            }.InstanceAsync();
        public Task<PlayedAnnualLeague> GetAsync(string groupName, string leagueName, int year)
            => new AnnualLeagueKey
            {
                GroupName = groupName,
                LeagueName = leagueName,
                Year = year
            }.InstanceAsync();
        public Task<Rank> GetRankAsync(string groupName, string leagueName, int year)
            => new RankKey()
            {
                GroupName = groupName,
                LeagueName = leagueName,
                Year = year
            }.InstanceAsync();
        public async Task<Day> GetDayAsync(string groupName, string leagueName, int year, int serieADay)
            => (await new CalendarKey { GroupName = groupName, Year = year, LeagueName = leagueName }.InstanceAsync().NoContext())?.Days.FirstOrDefault(x => x.SerieADay == serieADay);
        public async Task<(PlayedGame Game, RealDay RealDay, bool IsLive)> GetGameAsync(string gameId, bool isLive)
        {
            var playedGame = new PlayedGame(gameId);
            var day = await GetDayAsync(playedGame.GroupName, playedGame.League, playedGame.Year, playedGame.SerieADay).NoContext();
            var realDay = (await Core.GetSerieACalendarAsync(playedGame.Year).NoContext()).Days.FirstOrDefault(x => x.SerieADay == playedGame.SerieADay);
            var game = day.Games.SelectMany(x => x.Value)
                .FirstOrDefault(x => x.Id == gameId);
            var league = await GetAsync(playedGame.GroupName, playedGame.League, playedGame.Year).NoContext();
            if (game != default)
            {
                if (game.Result == default || !game.Result.HasValue || !game.Home.ActivePlayers.Any(x => x.Vote != default))
                {
                    var votes = await Core.GetVotesAsync(playedGame.Year, playedGame.SerieADay).NoContext();
                    if (votes == default)
                        votes = new List<Player>();
                    if (isLive)
                        votes = votes.MergeVote(await Core.GetLiveVotesAsync(playedGame.Year, playedGame.SerieADay).NoContext());
                    game.SetVote(votes, league.Settings);
                }
                if (isLive)
                {
                    var chances = await Core.GetChancesAsync().NoContext();
                    game.SetChances(chances);
                }
            }
            return (game, realDay, isLive);
        }
        public async Task<Rank> GetHallOfFameAsync(string groupName, string leagueName)
        {
            var group = await Group.GetLeaguesAsync(groupName).NoContext();
            League league = group.FirstOrDefault(x => x.Name == leagueName);
            Rank hallOfFame = default;
            foreach (var year in league.Years.OrderByDescending(x => x.Year))
            {
                var rank = await GetRankAsync(groupName, league.Name, year.Year).NoContext();
                if (hallOfFame == default)
                    hallOfFame = rank;
                else
                    hallOfFame.AddRank(rank);
            }
            return hallOfFame;
        }
    }
}