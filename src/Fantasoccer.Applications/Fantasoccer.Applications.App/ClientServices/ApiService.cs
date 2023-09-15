using Fantasoccer.Domain;
using RepositoryFramework;

namespace Fantasoccer.Applications.App
{
    public class ApiService
    {
        public ApiService(
            IRepository<RealCalendar, int> realCalendar,
            IRepository<RealPlayersWrapper, int> realPlayers,
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
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> voteFactory,
            IRepository<LiveGroup, string> live)
        {
            RealCalendar = realCalendar;
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
            Live = live;
            LiveVote = voteFactory.Create("live");
            OfficialVote = voteFactory.Create("official");
        }

        public IRepository<RealCalendar, int> RealCalendar { get; }
        public IRepository<RealPlayersWrapper, int> RealPlayers { get; }
        public IRepository<StatPlayersWrapper, int> StatsPlayers { get; }
        public IRepository<ChancedRealPlayerWrapper, YearDayKey> Chances { get; }
        public IRepository<Group, string> Groups { get; }
        public IRepository<Rank, LeagueKey> Ranks { get; }
        public IRepository<Calendar, LeagueKey> Calendar { get; }
        public IRepository<PlayedGame, GameKey> Games { get; }
        public IRepository<RealTeamWrapper, int> SerieA { get; }
        public IRepository<Team, TeamKey> Team { get; }
        public IRepository<Team, TeamDayKey> DailyTeam { get; }
        public IRepository<AppIdentity, string> AppIdentity { get; }
        public IRepository<LiveGroup, string> Live { get; }
        public IRepository<VotedRealPlayerWrapper, YearDayKey> LiveVote { get; }
        public IRepository<VotedRealPlayerWrapper, YearDayKey> OfficialVote { get; }
    }
}
