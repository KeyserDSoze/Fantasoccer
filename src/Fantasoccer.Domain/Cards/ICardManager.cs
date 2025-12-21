using System.Data;
using System.Text.Json.Serialization;
using Fantasoccer.Domain;

namespace Fantasoccer
{
    public interface ICardManager
    {
        Task<CardResults> CheckAsync(IEnumerable<EnrichedPlayer> mine, IEnumerable<EnrichedPlayer> enemies, IEnumerable<Card> cards);
    }
    public sealed class CardManager : ICardManager
    {
        public Task<CardResults> CheckAsync(IEnumerable<EnrichedPlayer> mine, IEnumerable<EnrichedPlayer> enemies, IEnumerable<Card> cards)
        {
            var cardResult = new CardResults() { Cards = [] };
            foreach (var card in cards)
            {
                var myOccurences = 0;
                var myPlayersInvolved = 0;
                var myTotalVotes = new StatPlayer();
                var enemyOccurences = 0;
                var enemyPlayersInvolved = 0;
                var totalEnemyVotes = new StatPlayer();
                if (card.MyCondition != null)
                    (myOccurences, myPlayersInvolved, myTotalVotes) = Check(card.MyCondition, mine);
                if (card.EnemyCondition != null)
                    (enemyOccurences, enemyPlayersInvolved, totalEnemyVotes) = Check(card.EnemyCondition, enemies);
                static (int Occurences, int Total, StatPlayer TotalVote) Check(CardCondition condition, IEnumerable<EnrichedPlayer> players)
                {
                    var occurrences = 0;
                    var total = 0;
                    var totalVote = new StatPlayer();
                    foreach (var role in condition.RolesInvolved)
                    {
                        var currentPlayersToEvaluate = players;
                        if (role != FantaSoccerRole.All)
                            currentPlayersToEvaluate = players.Where(p => p.CurrentPosition == role);
                        total += currentPlayersToEvaluate.Count();
                        foreach (var player in currentPlayersToEvaluate)
                        {
                            if (player.Vote == null)
                                continue;
                            if (!player.Vote.HasVote)
                                continue;
                            totalVote.WithVote++;
                            totalVote.Penalty += player.Vote.Penalty;
                            totalVote.SufferedGoal += player.Vote.SufferedGoal;
                            totalVote.StoppedPenalty += player.Vote.StoppedPenalty;
                            totalVote.Assist += player.Vote.Assist;
                            totalVote.OwnGoal += player.Vote.OwnGoal;
                            totalVote.Goal += player.Vote.Goal;
                            totalVote.WrongedPenalty += player.Vote.WrongedPenalty;
                            if (player.Vote.Status == Behaviour.RedCard)
                            {
                                totalVote.RedCard++;
                            }
                            if (player.Vote.Status == Behaviour.YellowCard)
                            {
                                totalVote.YellowCard++;
                            }
                            if (player.Vote.Value >= 6)
                            {
                                totalVote.IsEnough++;
                            }
                            if (player.Vote.ManOfTheMatch)
                            {
                                totalVote.ManOfTheMatch++;
                            }
                            if (player.Vote.Injured)
                            {
                                totalVote.Injured++;
                            }
                            if (condition.ConditionForPlayer?.Minimum?.Vote.HasValue == true && player.Vote.Value < condition.ConditionForPlayer.Minimum.Vote.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.Vote.HasValue == true && player.Vote.Value > condition.ConditionForPlayer.Maximum.Vote.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.HasYellow.HasValue == true && ((condition.ConditionForPlayer.Minimum.HasYellow.Value && player.Vote.Status != Behaviour.YellowCard) || (!condition.ConditionForPlayer.Minimum.HasYellow.Value && player.Vote.Status == Behaviour.YellowCard)))
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.HasRed.HasValue == true && ((condition.ConditionForPlayer.Maximum.HasRed.Value && player.Vote.Status != Behaviour.RedCard) || (!condition.ConditionForPlayer.Maximum.HasRed.Value && player.Vote.Status == Behaviour.RedCard)))
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.Goals.HasValue == true && player.Vote.Goal < condition.ConditionForPlayer.Minimum.Goals.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.Goals.HasValue == true && player.Vote.Goal > condition.ConditionForPlayer.Maximum.Goals.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.Assists.HasValue == true && player.Vote.Assist < condition.ConditionForPlayer.Minimum.Assists.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.Assists.HasValue == true && player.Vote.Assist > condition.ConditionForPlayer.Maximum.Assists.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.OwnGoals.HasValue == true && player.Vote.OwnGoal < condition.ConditionForPlayer.Minimum.OwnGoals.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.OwnGoals.HasValue == true && player.Vote.OwnGoal > condition.ConditionForPlayer.Maximum.OwnGoals.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.Penalties.HasValue == true && player.Vote.Penalty < condition.ConditionForPlayer.Minimum.Penalties.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.Penalties.HasValue == true && player.Vote.Penalty > condition.ConditionForPlayer.Maximum.Penalties.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.StoppedPenalties.HasValue == true && player.Vote.StoppedPenalty < condition.ConditionForPlayer.Minimum.StoppedPenalties.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.StoppedPenalties.HasValue == true && player.Vote.StoppedPenalty > condition.ConditionForPlayer.Maximum.StoppedPenalties.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.SufferedGoals.HasValue == true && player.Vote.SufferedGoal < condition.ConditionForPlayer.Minimum.SufferedGoals.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.SufferedGoals.HasValue == true && player.Vote.SufferedGoal > condition.ConditionForPlayer.Maximum.SufferedGoals.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.WrongedPenalties.HasValue == true && player.Vote.WrongedPenalty < condition.ConditionForPlayer.Minimum.WrongedPenalties.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.WrongedPenalties.HasValue == true && player.Vote.WrongedPenalty > condition.ConditionForPlayer.Maximum.WrongedPenalties.Value)
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.IsManOfTheMatch.HasValue == true && ((condition.ConditionForPlayer.Minimum.IsManOfTheMatch.Value && !player.Vote.ManOfTheMatch) || (!condition.ConditionForPlayer.Minimum.IsManOfTheMatch.Value && player.Vote.ManOfTheMatch)))
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.IsOut.HasValue == true && ((condition.ConditionForPlayer.Maximum.IsOut.Value && !player.Vote.IsOut) || (!condition.ConditionForPlayer.Maximum.IsOut.Value && player.Vote.IsOut)))
                                continue;
                            if (condition.ConditionForPlayer?.Minimum?.IsIn.HasValue == true && ((condition.ConditionForPlayer.Minimum.IsIn.Value && !player.Vote.IsIn) || (!condition.ConditionForPlayer.Minimum.IsIn.Value && player.Vote.IsIn)))
                                continue;
                            if (condition.ConditionForPlayer?.Maximum?.IsInjured.HasValue == true && ((condition.ConditionForPlayer.Maximum.IsInjured.Value && !player.Vote.Injured) || (!condition.ConditionForPlayer.Maximum.IsInjured.Value && player.Vote.Injured)))
                                continue;
                            occurrences++;
                        }
                    }
                    return (occurrences, total, totalVote);
                }

                var checkMyTeam = CheckTeam(card.MyCondition?.Condition, myTotalVotes);
                var checkEnemyTeam = CheckTeam(card.EnemyCondition?.Condition, totalEnemyVotes);

                static bool CheckTeam(CardConditionSummatory? cardConditionSummatory, StatPlayer totalFromCard)
                {
                    if (cardConditionSummatory != null)
                    {
                        if (cardConditionSummatory.Minimum != null)
                        {
                            if (cardConditionSummatory.Minimum.WithVote.HasValue && totalFromCard.WithVote < cardConditionSummatory.Minimum.WithVote.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.WithoutVote.HasValue && totalFromCard.WithVote == 0)
                                return false;
                            if (cardConditionSummatory.Minimum.WithSpecial.HasValue && (totalFromCard.RedCard + totalFromCard.YellowCard) < cardConditionSummatory.Minimum.WithSpecial.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.Goal.HasValue && totalFromCard.Goal < cardConditionSummatory.Minimum.Goal.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.Penalty.HasValue && totalFromCard.Penalty < cardConditionSummatory.Minimum.Penalty.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.Assist.HasValue && totalFromCard.Assist < cardConditionSummatory.Minimum.Assist.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.StoppedPenalty.HasValue && totalFromCard.StoppedPenalty < cardConditionSummatory.Minimum.StoppedPenalty.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.SufferedGoal.HasValue && totalFromCard.SufferedGoal < cardConditionSummatory.Minimum.SufferedGoal.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.WrongedPenalty.HasValue && totalFromCard.WrongedPenalty < cardConditionSummatory.Minimum.WrongedPenalty.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.OwnGoal.HasValue && totalFromCard.OwnGoal < cardConditionSummatory.Minimum.OwnGoal.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.YellowCard.HasValue && totalFromCard.YellowCard < cardConditionSummatory.Minimum.YellowCard.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.RedCard.HasValue && totalFromCard.RedCard < cardConditionSummatory.Minimum.RedCard.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.IsEnough.HasValue && totalFromCard.IsEnough < cardConditionSummatory.Minimum.IsEnough.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.ManOfTheMatch.HasValue && totalFromCard.ManOfTheMatch < cardConditionSummatory.Minimum.ManOfTheMatch.Value)
                                return false;
                            if (cardConditionSummatory.Minimum.Injured.HasValue && totalFromCard.Injured < cardConditionSummatory.Minimum.Injured.Value)
                                return false;
                        }
                        if (cardConditionSummatory.Maximum != null)
                        {
                            if (cardConditionSummatory.Maximum.WithVote.HasValue && totalFromCard.WithVote > cardConditionSummatory.Maximum.WithVote.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.WithoutVote.HasValue && totalFromCard.WithVote > 0)
                                return false;
                            if (cardConditionSummatory.Maximum.WithSpecial.HasValue && (totalFromCard.RedCard + totalFromCard.YellowCard) > cardConditionSummatory.Maximum.WithSpecial.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.Goal.HasValue && totalFromCard.Goal > cardConditionSummatory.Maximum.Goal.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.Penalty.HasValue && totalFromCard.Penalty > cardConditionSummatory.Maximum.Penalty.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.Assist.HasValue && totalFromCard.Assist > cardConditionSummatory.Maximum.Assist.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.StoppedPenalty.HasValue && totalFromCard.StoppedPenalty > cardConditionSummatory.Maximum.StoppedPenalty.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.SufferedGoal.HasValue && totalFromCard.SufferedGoal > cardConditionSummatory.Maximum.SufferedGoal.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.WrongedPenalty.HasValue && totalFromCard.WrongedPenalty > cardConditionSummatory.Maximum.WrongedPenalty.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.OwnGoal.HasValue && totalFromCard.OwnGoal > cardConditionSummatory.Maximum.OwnGoal.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.YellowCard.HasValue && totalFromCard.YellowCard > cardConditionSummatory.Maximum.YellowCard.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.RedCard.HasValue && totalFromCard.RedCard > cardConditionSummatory.Maximum.RedCard.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.IsEnough.HasValue && totalFromCard.IsEnough > cardConditionSummatory.Maximum.IsEnough.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.ManOfTheMatch.HasValue && totalFromCard.ManOfTheMatch > cardConditionSummatory.Maximum.ManOfTheMatch.Value)
                                return false;
                            if (cardConditionSummatory.Maximum.Injured.HasValue && totalFromCard.Injured > cardConditionSummatory.Maximum.Injured.Value)
                                return false;
                        }
                    }
                    return true;
                }

                cardResult.Cards.Add(new CardResult
                {
                    CardId = card.Id,
                    Points = card.Points,
                    NegativePoints = card.NegativePoints,
                    EnemyPoints = card.EnemyPoints,
                    Reached = checkMyTeam && checkEnemyTeam &&
                              (card.MyCondition == null || (myOccurences >= (card.MyCondition.ConditionForPlayer?.MinimumNumberOfOccurencies ?? myPlayersInvolved)))
                              &&
                              (card.EnemyCondition == null || (enemyOccurences >= (card.EnemyCondition.ConditionForPlayer?.MinimumNumberOfOccurencies ?? enemyPlayersInvolved)))
                });
            }
            return Task.FromResult(cardResult);
        }
    }
    public sealed class CardResults
    {
        [JsonPropertyName("c")]
        public List<CardResult> Cards { get; set; }
    }
    public sealed class CardResult
    {
        [JsonPropertyName("i")]
        public string CardId { get; set; }
        [JsonPropertyName("p")]
        public float Points { get; set; }
        [JsonPropertyName("np")]
        public float NegativePoints { get; set; }
        [JsonPropertyName("ep")]
        public float EnemyPoints { get; set; }
        [JsonPropertyName("r")]
        public bool Reached { get; set; }
    }
    public enum CardType
    {
        Strategy,
        Goalkeeper,
        Defensor,
        Midfielder,
        Forward,
        Special
    }
    public sealed class CardWrapper
    {
        [JsonPropertyName("c")]
        public List<Card> Cards { get; set; } = [];
    }
    public sealed class Card
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("n")]
        public string Name { get; set; }
        [JsonPropertyName("d")]
        public string Description { get; set; }
        [JsonPropertyName("t")]
        public CardType Type { get; set; }
        [JsonPropertyName("p")]
        public float Points { get; set; }
        [JsonPropertyName("np")]
        public float NegativePoints { get; set; }
        [JsonPropertyName("ep")]
        public float EnemyPoints { get; set; }
        [JsonPropertyName("mc")]
        public CardCondition? MyCondition { get; set; }
        [JsonPropertyName("ec")]
        public CardCondition? EnemyCondition { get; set; }
    }
    public sealed class CardConditionForPlayersValues
    {
        [JsonPropertyName("v")]
        public float? Vote { get; set; }
        [JsonPropertyName("yc")]
        public bool? HasYellow { get; set; }
        [JsonPropertyName("rc")]
        public bool? HasRed { get; set; }
        [JsonPropertyName("g")]
        public int? Goals { get; set; }
        [JsonPropertyName("a")]
        public int? Assists { get; set; }
        [JsonPropertyName("o")]
        public int? OwnGoals { get; set; }
        [JsonPropertyName("p")]
        public int? Penalties { get; set; }
        [JsonPropertyName("s")]
        public int? StoppedPenalties { get; set; }
        [JsonPropertyName("d")]
        public int? SufferedGoals { get; set; }
        [JsonPropertyName("w")]
        public int? WrongedPenalties { get; set; }
        [JsonPropertyName("mm")]
        public bool? IsManOfTheMatch { get; set; }
        [JsonPropertyName("io")]
        public bool? IsOut { get; set; }
        [JsonPropertyName("ii")]
        public bool? IsIn { get; set; }
        [JsonPropertyName("ij")]
        public bool? IsInjured { get; set; }
    }
    public sealed class CardConditionForPlayers
    {
        [JsonPropertyName("m")]
        public int? MinimumNumberOfOccurencies { get; set; }
        [JsonPropertyName("mn")]
        public CardConditionForPlayersValues? Minimum { get; set; }
        [JsonPropertyName("mx")]
        public CardConditionForPlayersValues? Maximum { get; set; }
    }
    public sealed class CardConditionSummatoryPlayers
    {
        [JsonPropertyName("v")]
        public int? WithVote { get; set; }
        [JsonPropertyName("q")]
        public int? WithoutVote { get; set; }
        [JsonPropertyName("s")]
        public int? WithSpecial { get; set; }
        [JsonPropertyName("g")]
        public int? Goal { get; set; }
        [JsonPropertyName("p")]
        public int? Penalty { get; set; }
        [JsonPropertyName("u")]
        public int? Assist { get; set; }
        [JsonPropertyName("j")]
        public int? StoppedPenalty { get; set; }
        [JsonPropertyName("m")]
        public int? SufferedGoal { get; set; }
        [JsonPropertyName("w")]
        public int? WrongedPenalty { get; set; }
        [JsonPropertyName("o")]
        public int? OwnGoal { get; set; }
        [JsonPropertyName("y")]
        public int? YellowCard { get; set; }
        [JsonPropertyName("d")]
        public int? RedCard { get; set; }
        [JsonPropertyName("e")]
        public int? IsEnough { get; set; }
        [JsonPropertyName("c")]
        public int? ManOfTheMatch { get; set; }
        [JsonPropertyName("ij")]
        public int? Injured { get; set; }
    }
    public sealed class CardConditionSummatory
    {
        [JsonPropertyName("mx")]
        public CardConditionSummatoryPlayers? Maximum { get; set; }
        [JsonPropertyName("mn")]
        public CardConditionSummatoryPlayers? Minimum { get; set; }
    }
    public sealed class CardCondition
    {
        [JsonPropertyName("r")]
        public List<FantaSoccerRole> RolesInvolved { get; set; } = [FantaSoccerRole.All];
        [JsonPropertyName("cp")]
        public CardConditionForPlayers? ConditionForPlayer { get; set; }
        [JsonPropertyName("cs")]
        public CardConditionSummatory? Condition { get; set; }
    }
}
