using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    internal sealed class TeamCalculator : ITeamCalculator
    {
        private IRepository<VotedRealPlayerWrapper, YearDayKey> _liveVote;
        private IRepository<VotedRealPlayerWrapper, YearDayKey> _officialVote;
        private readonly IRepository<RealCalendar, int> _realCalendar;
        private readonly IRepository<ChancedRealPlayerWrapper, YearDayKey> _chances;
        private readonly IVotePlayerGenerator _votePlayerGenerator;
        private readonly IFactory<ILeagueCalculator> _leagueCalculator;

        public TeamCalculator(
             IRepository<RealCalendar, int> realCalendar,
             IRepository<ChancedRealPlayerWrapper, YearDayKey> chances,
             IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> voteFactory,
             IVotePlayerGenerator votePlayerGenerator,
             IFactory<ILeagueCalculator> leagueCalculator)
        {
            _liveVote = voteFactory.Create("live");
            _officialVote = voteFactory.Create("official");
            _realCalendar = realCalendar;
            _chances = chances;
            _votePlayerGenerator = votePlayerGenerator;
            _leagueCalculator = leagueCalculator;
        }
        public bool IsLive { get; private set; }
        public VotedRealPlayerWrapper? Live { get; private set; }
        public VotedRealPlayerWrapper? Official { get; private set; }
        public ChancedRealPlayerWrapper? Chance { get; private set; }
        public RealDay? RealDay { get; private set; }
        public LeagueSetting? LeagueSetting { get; private set; }
        public bool IsEditable { get; private set; }
        public int SerieADay { get; private set; }
        public LeagueType LeagueType { get; private set; }
        public async ValueTask BuildRequestAsync(int year, int day, LeagueType leagueType, LeagueSetting leagueSettings, bool withRealGame, bool withVote, bool withChance)
        {
            if (year == 0)
                year = FantacalcioTime.ActualYear;
            LeagueSetting = leagueSettings;
            LeagueType = leagueType;
            if (withVote || withChance || withRealGame || day == 0)
            {
                var calendar = await _realCalendar.GetAsync(year);
                if (day == 0)
                    day = calendar?.NextDay?.SerieADay ?? calendar?.LiveDay?.SerieADay ?? 1;
                SerieADay = day;
                var key = new YearDayKey { Day = day, Year = year };
                RealDay = calendar.Days.FirstOrDefault(x => x.SerieADay == key.Day);
                IsEditable = day == calendar?.NextDay?.SerieADay;
                if (calendar != null)
                {
                    IsLive = calendar.LiveSerieADay == key.Day;
                    if (withVote)
                    {
                        if (IsLive)
                        {
                            Live = await _liveVote.GetAsync(key);
                        }
                        Official = await _officialVote.GetAsync(key);
                    }
                    else
                    {
                        Live = null;
                        Official = null;
                    }
                    if (withChance)
                    {
                        Chance = await _chances.GetAsync(key);
                    }
                    else
                    {
                        Chance = null;
                    }
                }
            }
            else
            {
                RealDay = null;
                SerieADay = day;
            }
        }
        public void SetLeagueSettings(LeagueType leagueType, LeagueSetting leagueSettings)
        {
            LeagueType = leagueType;
            LeagueSetting = leagueSettings;
        }
        public Point CalculatePoint(IEnumerable<Player> players)
            => CalculatePoint(Enrich(players));
        public Point CalculatePoint(IEnumerable<EnrichedPlayer> players)
        {
            if (!players.Any() || LeagueSetting == null)
                return new Point(0F, false, false, false);
            var point = 0F;
            var defensiveBonus = true;
            var defensorCount = 0;
            var leagueCalculator = _leagueCalculator.Create(LeagueType.ToString())!;
            var goodPeople = true;
            foreach (var player in leagueCalculator.GetRightFormation(players).Where(x => x.CurrentPosition <= FantaSoccerRole.Forward))
            {
                if (player.Vote != default && player.Vote.HasVote)
                {
                    point += FinalValue(player).Value;
                    if (player.Current.Role == Role.Defensor)
                        defensorCount++;
                }
                if (player.Current.Role == Role.Defensor && (player.Vote == default || !player.Vote.HasVote || player.Vote.Value < 6))
                    defensiveBonus = false;
                if (player.Vote == default || !player.Vote.HasVote || player.Vote.Status != Behaviour.Nothing)
                    goodPeople = false;
            }
            if (goodPeople)
                point += LeagueSetting.PointForGoodPeople;
            if (defensiveBonus)
            {
                if (defensorCount == 3)
                    point += LeagueSetting.PointForStrongDefense;
                else if (defensorCount == 4)
                    point += LeagueSetting.PointForStrongDefense4;
                else if (defensorCount == 5)
                    point += LeagueSetting.PointForStrongDefense5;
            }
            return new Point(point, defensiveBonus, goodPeople,
                LeagueSetting.PointForOwnGoal > 0 && point < LeagueSetting.PointForFirstGoal - LeagueSetting.PointForOwnGoal);
        }
        public EnrichedPlayer Enrich(Player player)
        {
            var enrichedPlayer = new EnrichedPlayer
            {
                Current = player,
                Chance = GetChance(player),
                RealGame = GetRealGame(player),
                Vote = GetVote(player),
                CurrentPosition = player.Position
            };
            if (enrichedPlayer.Vote != null)
                enrichedPlayer.FinalValue = FinalValue(enrichedPlayer);
            return enrichedPlayer;
        }
        public EnrichedPlayer ForceEnrich(Player player, Vote? vote, Chance? chance, RealGame? realGame)
        {
            var enrichedPlayer = new EnrichedPlayer
            {
                Current = player,
                Chance = chance,
                RealGame = realGame,
                Vote = vote,
                CurrentPosition = player.Position
            };
            if (enrichedPlayer.Vote != null)
                enrichedPlayer.FinalValue = FinalValue(enrichedPlayer);
            return enrichedPlayer;
        }
        public IEnumerable<EnrichedPlayer> Enrich(IEnumerable<Player> players)
        {
            foreach (var player in players)
            {
                yield return Enrich(player);
            }
        }
        private Chance? GetChance(Player player)
        {
            var chancedPlayer = Chance?.Players.Find(x => x.Key == player.Key);
            return chancedPlayer?.Chance;
        }
        private Vote? GetVote(Player player)
        {
            var votePlayer = Official?.Players.Find(x => x.Key == player.Key);
            if (votePlayer == null)
                votePlayer = Live?.Players.Find(x => x.Key == player.Key);
            return votePlayer?.Vote;
        }
        private RealGame? GetRealGame(Player player)
        {
            if (player.RealTeam != null)
            {
                var game = RealDay?.Games.Find(x => x.Home.Abbreviation == player.RealTeam.Abbreviation || x.Away.Abbreviation == player.RealTeam.Abbreviation);
                return game;
            }
            return null;
        }
        public VoteValue FinalValue(Player player) => FinalValue(Enrich(player));
        public VoteValue FinalValue(EnrichedPlayer player)
        {
            var vote = player.Vote;
            if (LeagueSetting == null || vote == null)
                return VoteValue.Empty;
            var voteLeagueSetting = LeagueSetting.Votes.ContainsKey(player.Current.Role) ?
                LeagueSetting.Votes[player.Current.Role] : LeagueSetting.Votes[Role.Undefined];
            var calculatedVote = vote.Value;
            calculatedVote += vote.Goal * voteLeagueSetting.Goal;
            calculatedVote += vote.Penalty * voteLeagueSetting.Penalty;
            calculatedVote += vote.SufferedGoal * voteLeagueSetting.SufferedGoal;
            calculatedVote += vote.StoppedPenalty * voteLeagueSetting.StoppedPenalty;
            calculatedVote += vote.WrongedPenalty * voteLeagueSetting.WrongedPenalty;
            calculatedVote += vote.OwnGoal * voteLeagueSetting.OwnGoal;
            calculatedVote += vote.Assist * voteLeagueSetting.Assist;
            calculatedVote += vote.Injured ? voteLeagueSetting.Injury : 0;
            calculatedVote += vote.ManOfTheMatch ? voteLeagueSetting.ManOfTheMatch : 0;
            if (vote.Status == Behaviour.YellowCard)
                calculatedVote += voteLeagueSetting.YellowCard;
            else if (vote.Status == Behaviour.RedCard)
                calculatedVote += voteLeagueSetting.RedCard;
            var special = vote.HasVote && player.Current.Role == Role.GoalKeeper
                && vote.SufferedGoal == 0 && LeagueSetting.PointForCleanSheet > 0;
            if (special)
                calculatedVote += LeagueSetting.PointForCleanSheet;
            return new VoteValue()
            {
                Value = calculatedVote,
                Special = special
            };
        }
        public ValueTask<bool> RecalculateDayAsync(int serieADay)
            => _votePlayerGenerator.GenerateAndSaveAsync(FantacalcioTime.ActualYear, serieADay, true);
    }
}
