using System.Globalization;
using System.Text.Json;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Fantasoccer.LegacyModels;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadHallArchives(VerifiedJsonDataset dataset, CorePlan plan, Func<string, string, Guid> map,
        Func<RealPlayer, PlayerSource, string> player, Func<RealTeam?, ClubSource, string?> club, Func<Guid, string, string, UserAccount> user, Action<int> season)
    {
        Guid Existing(string kind, string key) => plan.Maps.SingleOrDefault(x => x.Kind == kind && x.LegacyKey == key)?.TargetId ?? throw new CoreImportException("missing_hall_reference");
        var missingClubs = 0; string? firstMissingClub = null;
        foreach (var document in dataset.Documents.Where(x => x.Container == "halloffame"))
        {
            missingClubs = 0; firstMissingClub = null;
            var entity = document.Envelope.Deserialize<LegacyEntity<HallOfFame, GeneralLeagueKey>>() ?? throw new CoreImportException("invalid_hall_envelope");
            var key = entity.Key ?? throw new CoreImportException("missing_hall_key");
            var value = entity.Value ?? throw new CoreImportException("missing_hall_archive");
            var group = Existing("group", key.Group); var league = Existing("league", Composite(key.Group, key.League));
            if (plan.HallArchives.Any(x => x.GroupId == group && x.LeagueId == league)) throw new CoreImportException("hall_archive_collision");
            var archive = new HallArchive { GroupId = group, LeagueId = league };
            if (value.RecordGame is { } record)
            {
                season(record.Year);
                var game = record.Game ?? throw new CoreImportException("missing_hall_record_game");
                var legacyKey = Composite(key.Group, key.League, record.Year.ToString(CultureInfo.InvariantCulture), game.Id);
                var linked = plan.Maps.SingleOrDefault(x => x.Kind == "match" && x.LegacyKey == legacyKey)?.TargetId;
                archive.RecordGame = new() { Year = record.Year, Id = linked ?? map("hall-match", legacyKey), LinkedMatchId = linked,
                    LegacyGameId = game.Id, Number = game.Number, Home = game.Home, HomeOwner = game.HomeOwner, Away = game.Away, AwayOwner = game.AwayOwner, Result = ImportResult(game.Result) };
            }
            if (value.RecordPlayer is { } single)
            {
                if (!float.IsFinite(single.Points)) throw new CoreImportException("invalid_hall_points");
                archive.PlayerRecords.Add(new() { Kind = 0, Points = single.Points, Player = Snapshot(single.Player, PlayerOrigin(document, ".rp.p")) });
            }
            if (value.PlayerWithMostPointsInYear is { } yearly)
            {
                season(yearly.Year);
                if (!float.IsFinite(yearly.Points)) throw new CoreImportException("invalid_hall_points");
                archive.PlayerRecords.Add(new() { Kind = 1, Year = yearly.Year, Points = yearly.Points, Player = Snapshot(yearly.Player, PlayerOrigin(document, ".ppmy.p")) });
            }
            foreach (var (winning, index) in (value.WinningPlayers ?? []).Select((value, index) => (value, index))) archive.WinningPlayers.Add(new() { Ordinal = archive.WinningPlayers.Count,
                Player = Snapshot(winning.Player, PlayerOrigin(document, $".wp[{index}].p")), Wins = Wins(winning.Wins) });
            foreach (var winning in value.WinningTeams ?? []) archive.WinningTeams.Add(new() { Ordinal = archive.WinningTeams.Count,
                OwnerUserId = Member(winning.Owner), OwnerEmail = winning.Owner, Name = winning.Team, Wins = Wins(winning.Wins) });
            foreach (var rank in value.Ranks ?? [])
            {
                if (!new[] { rank.Point, rank.ValuePoint, rank.SufferedValuePoint }.All(float.IsFinite)) throw new CoreImportException("invalid_hall_rank");
                archive.Ranks.Add(new() { Ordinal = archive.Ranks.Count, OwnerUserId = Member(rank.Owner), OwnerEmail = rank.Owner,
                    Name = rank.Name, Points = rank.Point, Victories = rank.Victories, Draws = rank.Draws, Defeats = rank.Defeats, Goals = rank.Goal,
                    SufferedGoals = rank.SufferedGoal, ValuePoints = rank.ValuePoint, SufferedValuePoints = rank.SufferedValuePoint, PlusMoney = rank.PlusMoney, Money = rank.Money });
            }
            // Hall snapshots span several seasons: no single season/day applies.
            if (missingClubs > 0) plan.MissingRosterClubSnapshots.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document),
                0, 0, missingClubs, firstMissingClub!));
            plan.HallArchives.Add(archive);
            Guid Member(string owner)
            {
                var person = user(group, owner, owner);
                if (!plan.Memberships.Any(x => x.GroupId == group && x.UserId == person.Id))
                    plan.Memberships.Add(new() { GroupId = group, UserId = person.Id, Roles = GroupRole.None });
                return person.Id;
            }
        }
        HallPlayerSnapshot Snapshot(Player source, PlayerSource origin)
        {
            if (source is null) throw new CoreImportException("missing_hall_player");
            var clubId = club(source.RealTeam, ClubOrigin(origin));
            if (clubId is null) { missingClubs++; firstMissingClub ??= ClubOrigin(origin).JsonPath; }
            return new() { PlayerId = player(source, origin), ClubId = clubId, Name = source.Name, PlayerRole = (int)source.Role,
                IsActive = source.IsActive, IsVisible = source.Visible, Price = source.Price, Revenue = source.Revenue, Status = (int)source.Status, Position = (int)source.Position };
        }
        ICollection<HallWinStage> Wins(Dictionary<string, List<int>> source)
        {
            var stages = new List<HallWinStage>();
            foreach (var (name, years) in source ?? [])
            {
                var stage = new HallWinStage { Stage = name, Ordinal = stages.Count };
                foreach (var (year, ordinal) in years.Select((year, ordinal) => (year, ordinal))) { season(year); stage.Years.Add(new() { Year = year, Ordinal = ordinal }); }
                stages.Add(stage);
            }
            return stages;
        }
    }
}
