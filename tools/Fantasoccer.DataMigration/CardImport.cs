using System.Text.Json;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadCards(VerifiedJsonDataset dataset, CorePlan plan, Func<string, string, Guid> map)
    {
        var images = dataset.Manifest.Containers.Where(x => x.Name == "cardimages").SelectMany(x => x.Blobs).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var document in dataset.Documents.Where(x => x.Container == "cards"))
        {
            var entity = document.Envelope.Deserialize<Entity<CardWrapper, string>>() ?? throw new CoreImportException("invalid_cards_envelope");
            var key = entity.Key;
            if (string.IsNullOrWhiteSpace(key) || plan.CardLibraries.Any(x => x.Name == key)) throw new CoreImportException("card_library_collision");
            map("card-library", key); plan.CardLibraries.Add(new() { Name = key });
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in entity.Value?.Cards ?? throw new CoreImportException("missing_cards"))
            {
                if (string.IsNullOrWhiteSpace(source.Id) || !ids.Add(source.Id) || !new[] { source.Points, source.NegativePoints, source.EnemyPoints }.All(float.IsFinite))
                    throw new CoreImportException("invalid_card");
                plan.Cards.Add(new() { Library = key, Id = map("card", Composite(key, source.Id)), Ordinal = plan.Cards.Count(x => x.Library == key), Name = source.Name,
                    Description = source.Description, Type = (int)source.Type, Points = source.Points, NegativePoints = source.NegativePoints, EnemyPoints = source.EnemyPoints,
                    ImageBlobName = images.Contains(source.Id) ? source.Id : null, MyCondition = CardCondition(source.MyCondition), EnemyCondition = CardCondition(source.EnemyCondition) });
            }
        }
    }
    private static RuleCardCondition? CardCondition(global::Fantasoccer.CardCondition? source) => source is null ? null : new()
    {
        Roles = (source.RolesInvolved ?? []).Select((role, index) => new RuleCardRole { Role = (int)role, Ordinal = index }).ToList(),
        HasPlayerCondition = source.ConditionForPlayer != null, MinimumNumberOfOccurrences = source.ConditionForPlayer?.MinimumNumberOfOccurencies,
        MinimumPlayer = CardPlayerValues(source.ConditionForPlayer?.Minimum), MaximumPlayer = CardPlayerValues(source.ConditionForPlayer?.Maximum),
        HasTeamCondition = source.Condition != null, MinimumTeam = CardTeamValues(source.Condition?.Minimum), MaximumTeam = CardTeamValues(source.Condition?.Maximum)
    };
    private static RuleCardPlayerValues? CardPlayerValues(CardConditionForPlayersValues? source)
    {
        if (source is null) return null;
        if (source.Vote is { } vote && !float.IsFinite(vote)) throw new CoreImportException("invalid_card_vote");
        return new() { Vote = source.Vote, HasYellow = source.HasYellow, HasRed = source.HasRed, Goals = source.Goals, Assists = source.Assists, OwnGoals = source.OwnGoals,
            Penalties = source.Penalties, StoppedPenalties = source.StoppedPenalties, SufferedGoals = source.SufferedGoals, WrongedPenalties = source.WrongedPenalties,
            IsManOfTheMatch = source.IsManOfTheMatch, IsOut = source.IsOut, IsIn = source.IsIn, IsInjured = source.IsInjured };
    }
    private static RuleCardTeamValues? CardTeamValues(CardConditionSummatoryPlayers? source) => source is null ? null : new()
    {
        WithVote = source.WithVote, WithoutVote = source.WithoutVote, WithSpecial = source.WithSpecial, Goals = source.Goal, Penalties = source.Penalty,
        Assists = source.Assist, StoppedPenalties = source.StoppedPenalty, SufferedGoals = source.SufferedGoal, WrongedPenalties = source.WrongedPenalty,
        OwnGoals = source.OwnGoal, YellowCards = source.YellowCard, RedCards = source.RedCard, EnoughVotes = source.IsEnough, ManOfTheMatch = source.ManOfTheMatch, Injured = source.Injured
    };
}
