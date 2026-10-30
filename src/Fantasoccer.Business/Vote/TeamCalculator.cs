using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Business
{
    internal sealed class TeamCalculator : ITeamCalculator
    {
        private readonly IFootballMatchdayReader _matchday;
        private readonly IFactory<ILeagueCalculator> _leagueCalculator;

        public TeamCalculator(
              IFootballMatchdayReader matchday,
              IFactory<ILeagueCalculator> leagueCalculator)
        {
            _matchday = matchday;
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
            IsLive = false;
            IsEditable = false;
            Live = null;
            Official = null;
            Chance = null;
            if (withVote || withChance || withRealGame || day == 0)
            {
                var input = await _matchday.ReadAsync(year, day, withVote, withChance);
                SerieADay = input.Day;
                RealDay = input.RealDay;
                IsEditable = input.IsEditable;
                IsLive = input.IsLive;
                Live = input.Live;
                Official = input.Official;
                Chance = input.Chances;
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
            var calculatorType = LeagueSetting.Formation == FormationType.Best
                ? global::Fantasoccer.LeagueType.SuperLeague
                : LeagueType;
            var leagueCalculator = _leagueCalculator.Create(calculatorType.ToString())!;
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
            var chancedPlayer = Chance?.Players?.Find(x => x.Key == player.Key);
            return chancedPlayer?.Chance;
        }
        private Vote? GetVote(Player player)
        {
            var game = GetRealGame(player);
            if (LeagueSetting?.Postponements is { WaitForRecovery: false } policy &&
                (game?.WasPostponed == true || game?.Delayed == true))
                return new() { Role = player.Role, Value = policy.ValueFor(player.Role), HasVote = true, IsFinal = true, IsPolitical = true };
            // Only actual provider votes; political grades are never stored.
            var vote = Official?.Players?.Find(x => x.Key == player.Key)?.Vote;
            return vote ?? Live?.Players?.Find(x => x.Key == player.Key)?.Vote;
        }
        private RealGame? GetRealGame(Player player)
        {
            // Prefer a club attested in this day's vote over today's annual
            // catalogue/formation fallback. Never rewrite a historical vote.
            var club = Official?.Players?.Find(x => x.Key == player.Key)?.RealTeam
                ?? Live?.Players?.Find(x => x.Key == player.Key)?.RealTeam
                ?? player.RealTeam;
            if (club != null)
            {
                var game = RealDay?.Games?.Find(x =>
                    x.Home?.Abbreviation == club.Abbreviation ||
                    x.Away?.Abbreviation == club.Abbreviation ||
                    string.Equals(x.Home?.Name, club.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Away?.Name, club.Name, StringComparison.OrdinalIgnoreCase));
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
            if (vote.IsPolitical) return new() { Value = vote.Value, Special = false };
            var voteLeagueSetting = LeagueSetting.Votes?.GetValueOrDefault(player.Current.Role)
                ?? LeagueSetting.Votes?.GetValueOrDefault(Role.Undefined)
                ?? LeagueSetting.Default.Votes[Role.Undefined];
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
    }
}
