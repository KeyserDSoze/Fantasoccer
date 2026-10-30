using System.Text.Json;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Fantasoccer.LegacyModels;

namespace Fantasoccer.DataMigration;

// Operator-approved exclusion of legacy rosters saved where their owner had no
// team that year. Original bytes stay in the verified dump; the report carries
// structural coordinates only (for copies, also the roster that was kept).
public sealed record SkippedRosterDocument(string DocumentSha256, string Container, string ArchiveReference, int SeasonIndex, int Day,
    string Reason, string? KeptDocumentSha256 = null, string? KeptArchiveReference = null);

public sealed partial class CoreDatasetImporter
{
    private readonly record struct LegacyRoster(LegacyJsonDocument Document, string GroupKey, string BasketKey, string Email, int Year, int Day,
        string? LeagueKey, Team Team, RosterKind Kind)
    {
        // Opening rosters belong to the previous season's teams.
        public int OriginYear => Kind is RosterKind.OpeningSeason or RosterKind.OpeningMatchday ? Year - 1 : Year;
    }

    private static LegacyRoster ReadRoster(LegacyJsonDocument document)
    {
        switch (document.Container)
        {
            case "team":
                var season = document.Envelope.Deserialize<LegacyEntity<Team, TeamKey>>() ?? throw new CoreImportException("invalid_roster_envelope");
                var key = season.Key ?? throw new CoreImportException("missing_roster_key");
                return new(document, key.Group, key.BasketId, key.Email, key.Year, 0, null,
                    season.Value ?? throw new CoreImportException("missing_roster"), RosterKind.Season);
            case "dailyteams":
                var daily = document.Envelope.Deserialize<LegacyEntity<Team, TeamDayKey>>() ?? throw new CoreImportException("invalid_roster_envelope");
                var dayKey = daily.Key ?? throw new CoreImportException("missing_roster_key");
                return new(document, dayKey.Group, dayKey.BasketId, dayKey.Email, dayKey.Year, dayKey.Day, null,
                    daily.Value ?? throw new CoreImportException("missing_roster"), RosterKind.Matchday);
            case "openingteams":
                var opening = document.Envelope.Deserialize<LegacyEntity<Team, OpeningTeamKey>>() ?? throw new CoreImportException("invalid_roster_envelope");
                var openingKey = opening.Key ?? throw new CoreImportException("missing_roster_key");
                return new(document, openingKey.Group, openingKey.BasketId, openingKey.Email, openingKey.Year, 0, openingKey.League,
                    opening.Value ?? throw new CoreImportException("missing_roster"), RosterKind.OpeningSeason);
            default:
                var openingDay = document.Envelope.Deserialize<LegacyEntity<Team, OpeningTeamDayKey>>() ?? throw new CoreImportException("invalid_roster_envelope");
                var openingDayKey = openingDay.Key ?? throw new CoreImportException("missing_roster_key");
                return new(document, openingDayKey.Group, openingDayKey.BasketId, openingDayKey.Email, openingDayKey.Year, openingDayKey.Day, openingDayKey.League,
                    openingDay.Value ?? throw new CoreImportException("missing_roster"), RosterKind.OpeningMatchday);
        }
    }

    // A roster whose key basket does not hold its owner's team that year is
    // excluded only when (1) it is identical, in every field, to the single
    // roster of the same kind, day and league saved for that owner in the basket
    // of their only primary team that year, or (2) the owner has no team at all
    // in the group that year (not even as co-owner) and the roster carries
    // nothing: no players, money, prize or formation changes. Anything else
    // still stops the import: no team is chosen by name, co-ownership or year.
    private static bool SkipUnplacedRoster(CorePlan plan, IReadOnlyList<LegacyRoster> rosters, LegacyRoster roster, Guid groupId, string ownerEmail)
    {
        var document = roster.Document;
        var user = plan.Users.GetValueOrDefault(ownerEmail);
        var owned = user is null ? [] : plan.Owners.Where(x => x.GroupId == groupId && x.SeasonIndex == roster.OriginYear && x.UserId == user.Id).ToArray();
        if (owned.Length == 0)
        {
            var team = roster.Team;
            if (team.Players is { Count: > 0 } || team.MoneyFromRank != 0 || team.OpeningCompetitionPrize != 0 || team.FormationChanges != 0 ||
                team.FormationChangeHistory is { Count: > 0 })
                return false;
            plan.SkippedRosterDocuments.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document), roster.Year, roster.Day,
                "empty_roster_without_team"));
            return true;
        }
        var primary = owned.Where(x => x.IsPrimary).ToArray();
        if (primary.Length != 1) return false;
        var kept = rosters.Where(x => x.Document.Container == document.Container && x.GroupKey == roster.GroupKey && x.Year == roster.Year &&
                x.Day == roster.Day && x.LeagueKey == roster.LeagueKey && x.BasketKey != roster.BasketKey &&
                plan.Maps.Any(map => map.Kind == "basket" && map.LegacyKey == Composite(x.GroupKey, x.BasketKey) && map.TargetId == primary[0].BasketId) &&
                OwnerKey(plan, groupId, x.Email) == ownerEmail).ToArray();
        if (kept.Length != 1 || !JsonElement.DeepEquals(Value(document.Envelope), Value(kept[0].Document.Envelope))) return false;
        plan.SkippedRosterDocuments.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document), roster.Year, roster.Day,
            "identical_copy_in_team_basket", kept[0].Document.Sha256, DocumentArchiveReference(kept[0].Document)));
        return true;
    }
}
