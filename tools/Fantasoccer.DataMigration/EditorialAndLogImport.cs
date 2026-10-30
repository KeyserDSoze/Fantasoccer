using System.Globalization;
using System.Text.Json;
using Fantasoccer.Domain;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Fantasoccer.LegacyModels;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadEditorialAndLogs(VerifiedJsonDataset dataset, CorePlan plan, Func<string, string, Guid> map, Action<int> season)
    {
        Guid Existing(string kind, string key) => plan.Maps.SingleOrDefault(x => x.Kind == kind && x.LegacyKey == key)?.TargetId ?? throw new CoreImportException("missing_editorial_reference");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var document in dataset.Documents.Where(x => x.Container == "news"))
        {
            var entity = document.Envelope.Deserialize<LegacyEntity<NewsWrapper, YearDayGroup>>(options) ?? throw new CoreImportException("invalid_news_envelope");
            var key = entity.Key ?? throw new CoreImportException("missing_news_key"); var value = entity.Value ?? throw new CoreImportException("missing_news");
            season(key.Year);
            if (key.Day is < 1 or > 38 || value.Year != key.Year || value.Day != key.Day || (!string.IsNullOrEmpty(value.Group) && value.Group != key.Group)) throw new CoreImportException("news_scope_mismatch");
            var group = Existing("group", key.Group);
            if (plan.News.Any(x => x.GroupId == group && x.SeasonIndex == key.Year && x.Day == key.Day)) throw new CoreImportException("news_collision");
            var edition = new NewsEdition { GroupId = group, SeasonIndex = key.Year, Day = key.Day };
            foreach (var item in value.News ?? [])
            {
                // The legacy stored a display label, not a league ID. Retain it
                // verbatim; resolve an optional FK only when it is unambiguous.
                var leagues = plan.Leagues.Where(x => x.GroupId == group && string.Equals(x.Name, item.League, StringComparison.Ordinal) &&
                    plan.LeagueSeasons.Any(year => year.GroupId == group && year.LeagueId == x.LeagueId && year.SeasonIndex == key.Year)).ToArray();
                edition.Articles.Add(new() { Ordinal = edition.Articles.Count, LeagueId = leagues.Length == 1 ? leagues[0].LeagueId : null, LeagueName = item.League,
                    Title = item.Title, Content = item.Content, Image = item.Image, LegacyDate = Local(item.Date), PublishedAtUtc = Utc(item.Date) });
            }
            plan.News.Add(edition);
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "teamwithai"))
        {
            var entity = document.Envelope.Deserialize<LegacyEntity<TeamWithAi, TeamDayKey>>(options) ?? throw new CoreImportException("invalid_advice_envelope");
            var key = entity.Key ?? throw new CoreImportException("missing_advice_key"); season(key.Year);
            if (key.Day is < 1 or > 38) throw new CoreImportException("invalid_advice_day");
            var group = Existing("group", key.Group); var basket = Existing("basket", Composite(key.Group, key.BasketId));
            var id = Existing("team", Composite(key.Group, key.BasketId, key.Year.ToString(CultureInfo.InvariantCulture), OwnerKey(plan, group, key.Email)));
            var owner = ExistingOwner(plan, group, key.Email, "missing_advice_owner").Id;
            if (plan.Advice.Any(x => x.GroupId == group && x.TeamId == id && x.SeasonIndex == key.Year && x.Day == key.Day && x.OwnerUserId == owner)) throw new CoreImportException("advice_collision");
            plan.Advice.Add(new() { GroupId = group, BasketId = basket, TeamId = id, OwnerUserId = owner, SeasonIndex = key.Year, Day = key.Day,
                Message = entity.Value?.Message ?? throw new CoreImportException("missing_advice") });
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "table:logs"))
        {
            var row = document.Envelope.Deserialize<TableRow>(options) ?? throw new CoreImportException("invalid_log_row");
            string? Text(string name) => row.Properties.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value is { Type: "Edm.String" } property ? property.Value.GetString() : null;
            plan.Logs.Add(new() { Id = map("operational-log", Composite(row.PartitionKey, row.RowKey)), PartitionKey = row.PartitionKey, RowKey = row.RowKey,
                TimestampUtc = row.Timestamp, LegacyTimestampUtcTicks = row.Timestamp?.UtcTicks, ETag = row.ETag, Message = Text("Message"), Error = Text("Error"),
                Properties = row.Properties.Select(x => new ArchivedLogProperty { Name = x.Key, Type = x.Value.Type, EncodedValue = x.Value.Value.GetRawText() }).ToArray() });
        }
        foreach (var archive in plan.HallArchives)
        {
            var baselines = plan.Standings.Where(x => x.GroupId == archive.GroupId && x.LeagueId == archive.LeagueId && x.SnapshotDay == 0)
                .SelectMany(snapshot => HallContributionMath.FromSnapshot(snapshot, owner => ExistingOwner(plan, archive.GroupId, owner, "missing_hall_owner").Id)).ToArray();
            archive.ContributionsInitialized = HallContributionMath.Matches(archive, baselines) &&
                archive.Ranks.GroupBy(x => x.OwnerUserId).All(x => x.Count() == 1) && archive.WinningTeams.GroupBy(x => x.OwnerUserId).All(x => x.Count() == 1) &&
                archive.WinningPlayers.GroupBy(x => x.Player.PlayerId).All(x => x.Count() == 1);
            if (archive.ContributionsInitialized) plan.HallContributions.AddRange(baselines);
        }
    }
}
