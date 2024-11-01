using Fantasoccer.Domain;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Application.Shared
{
    public class ApiService
    {
        public ApiService(
            IRepository<RealCalendar, int> realCalendar,
            IRepository<RealRank, int> realRank,
            IRepository<RealPlayersWrapper, int> realPlayers,
            IRepository<RealPlayersWrapper, LeagueKey> randomizedRealPlayers,
            IRepository<StatPlayersWrapper, int> statsPlayers,
            IRepository<ChancedRealPlayerWrapper, YearDayKey> chances,
            IRepository<Group, string> groups,
            IRepository<Rank, LeagueKey> ranks,
            IRepository<Calendar, LeagueKey> calendar,
            IRepository<PlayedGame, GameKey> games,
            IRepository<RealTeamWrapper, int> serieA,
            IRepository<Team, TeamKey> team,
            IRepository<Team, TeamDayKey> dailyTeam,
            IRepository<AppIdentity, string> appIdentity,
            IRepository<Rank, YearDayLeague> dailyRank,
            IRepository<MarketWrapper, LeagueKey> market,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> voteFactory,
            IRepository<LiveGroup, string> live,
            IRepository<NewsWrapper, YearDayGroup> news,
            IRepository<HallOfFame, GeneralLeagueKey> hallOfFame,
            IRepository<PushNotificationSettings, string> pushNotification,
            IRepository<TeamWithAi, TeamDayKey> teamWithAi,
            IRepository<Settings, string> settings)
        {
            RealCalendar = realCalendar;
            RealRank = realRank;
            RealPlayers = realPlayers;
            StatsPlayers = statsPlayers;
            Chances = chances;
            Groups = groups;
            Ranks = ranks;
            Calendar = calendar;
            Games = games;
            SerieA = serieA;
            Team = team;
            DailyTeam = dailyTeam;
            AppIdentity = appIdentity;
            DailyRank = dailyRank;
            Market = market;
            RandomizedRealPlayers = randomizedRealPlayers;
            Live = live;
            News = news;
            HallOfFame = hallOfFame;
            PushNotification = pushNotification;
            LiveVote = voteFactory.Create("live");
            OfficialVote = voteFactory.Create("official");
            TeamWithAi = teamWithAi;
            Settings = settings;
        }

        public IRepository<RealCalendar, int> RealCalendar { get; }
        public IRepository<RealRank, int> RealRank { get; }
        public IRepository<RealPlayersWrapper, int> RealPlayers { get; }
        public IRepository<RealPlayersWrapper, LeagueKey> RandomizedRealPlayers { get; }
        public IRepository<StatPlayersWrapper, int> StatsPlayers { get; }
        public IRepository<ChancedRealPlayerWrapper, YearDayKey> Chances { get; }
        public IRepository<Group, string> Groups { get; }
        public IRepository<Rank, LeagueKey> Ranks { get; }
        public IRepository<Calendar, LeagueKey> Calendar { get; }
        public IRepository<PlayedGame, GameKey> Games { get; }
        public IRepository<RealTeamWrapper, int> SerieA { get; }
        public IRepository<Team, TeamKey> Team { get; }
        public IRepository<TeamWithAi, TeamDayKey> TeamWithAi { get; }
        public IRepository<Settings, string> Settings { get; }
        public IRepository<Team, TeamDayKey> DailyTeam { get; }
        public IRepository<AppIdentity, string> AppIdentity { get; }
        public IRepository<Rank, YearDayLeague> DailyRank { get; }
        public IRepository<MarketWrapper, LeagueKey> Market { get; }
        public IRepository<LiveGroup, string> Live { get; }
        public IRepository<NewsWrapper, YearDayGroup> News { get; }
        public IRepository<HallOfFame, GeneralLeagueKey> HallOfFame { get; }
        public IRepository<PushNotificationSettings, string> PushNotification { get; }
        public IRepository<VotedRealPlayerWrapper, YearDayKey> LiveVote { get; }
        public IRepository<VotedRealPlayerWrapper, YearDayKey> OfficialVote { get; }
    }
}
