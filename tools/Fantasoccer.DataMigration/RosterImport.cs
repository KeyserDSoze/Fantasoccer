using System.Globalization;
using System.Text.Json;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadRosters(VerifiedJsonDataset dataset, CorePlan plan, Func<string, string, Guid> map,
        Func<string, string, UserAccount> addUser, Func<Guid, string, string, UserAccount> addOwner, Action<int> addSeason, CoreImportOptions options)
    {
        var clubs = new Dictionary<string, FootballClub>(StringComparer.Ordinal);
        var players = new Dictionary<string, FootballPlayer>(StringComparer.Ordinal);
        var seasons = new Dictionary<(Guid Player, int Year), PlayerSeason>();
        var authoritative = new HashSet<(Guid Player, int Year)>();
        var roots = new HashSet<(string Kind, int Year, int Day)>();
        void Root(string kind, int year, int day)
        {
            if (!roots.Add((kind, year, day))) throw new CoreImportException("duplicate_football_dataset");
            map("football-dataset", Composite(kind, year.ToString(CultureInfo.InvariantCulture), day.ToString(CultureInfo.InvariantCulture)));
            plan.Datasets.Add(new() { Kind = kind, SeasonIndex = year, Day = day });
        }
        Guid Club(RealTeam? club)
        {
            if (club?.Name is not { Length: > 0 }) throw new CoreImportException("missing_real_club");
            var key = club.Name.Trim().ToLowerInvariant();
            if (clubs.TryGetValue(key, out var existing)) return existing.Id;
            var row = new FootballClub { Id = map("club", key), Name = club.Name.Trim(), Abbreviation = club.Abbreviation };
            clubs.Add(key, row); plan.Clubs.Add(row); return row.Id;
        }
        Guid Person(RealPlayer player, PlayerSource origin)
        {
            var legacyKey = CheckedPlayerKey(player, origin);
            if (!players.TryGetValue(legacyKey, out var person))
            {
                person = new() { Id = map("player", legacyKey), Name = Name(player.Name) };
                players.Add(legacyKey, person); plan.Players.Add(person);
            }
            return person.Id;
        }
        PlayerSeason Player(RealPlayer player, int year, bool root, PlayerSource origin)
        {
            var playerId = Person(player, origin);
            var identity = (playerId, year);
            if (root && !authoritative.Add(identity)) throw new CoreImportException("duplicate_player_season");
            if (!seasons.TryGetValue(identity, out var row))
            {
                row = new() { PlayerId = playerId, SeasonIndex = year, ClubId = Club(player.RealTeam),
                    Position = (PlayerPosition)(int)player.Role, IsActive = player.IsActive, IsVisible = player.Visible };
                seasons.Add(identity, row); plan.PlayerSeasons.Add(row);
            }
            return row;
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "realteamwrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<RealTeamWrapper, int>>() ?? throw new CoreImportException("invalid_club_envelope");
            addSeason(entity.Key);
            Root(document.Container, entity.Key, 0);
            foreach (var club in entity.Value?.Teams ?? throw new CoreImportException("missing_clubs")) Club(club);
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "realplayerswrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<RealPlayersWrapper, int>>() ?? throw new CoreImportException("invalid_players_envelope");
            addSeason(entity.Key);
            Root(document.Container, entity.Key, 0);
            foreach (var (player, index) in (entity.Value?.Players ?? throw new CoreImportException("missing_players")).Select((value, index) => (value, index)))
                Player(player, entity.Key, true, PlayerOrigin(document, $".p[{index}]"));
        }

        LoadFootballSamples(dataset, plan, Player, Club, addSeason, Root);

        var snapshots = new HashSet<(Guid Team, RosterKind Kind, int Day, Guid Competition)>();
        foreach (var document in dataset.Documents.Where(x => x.Container is "team" or "dailyteams" or "openingteams" or "openingdailyteams"))
        {
            string groupKey, basketKey, email; int year, day; string? leagueKey = null; Team team; RosterKind kind;
            switch (document.Container)
            {
                case "team":
                    var season = document.Envelope.Deserialize<Entity<Team, TeamKey>>() ?? throw new CoreImportException("invalid_roster_envelope");
                    var key = season.Key ?? throw new CoreImportException("missing_roster_key");
                    groupKey = key.Group; basketKey = key.BasketId; email = key.Email; year = key.Year; day = 0; kind = RosterKind.Season;
                    team = season.Value ?? throw new CoreImportException("missing_roster"); break;
                case "dailyteams":
                    var daily = document.Envelope.Deserialize<Entity<Team, TeamDayKey>>() ?? throw new CoreImportException("invalid_roster_envelope");
                    var dayKey = daily.Key ?? throw new CoreImportException("missing_roster_key");
                    groupKey = dayKey.Group; basketKey = dayKey.BasketId; email = dayKey.Email; year = dayKey.Year; day = dayKey.Day; kind = RosterKind.Matchday;
                    team = daily.Value ?? throw new CoreImportException("missing_roster"); break;
                case "openingteams":
                    var opening = document.Envelope.Deserialize<Entity<Team, OpeningTeamKey>>() ?? throw new CoreImportException("invalid_roster_envelope");
                    var openingKey = opening.Key ?? throw new CoreImportException("missing_roster_key");
                    groupKey = openingKey.Group; basketKey = openingKey.BasketId; email = openingKey.Email; year = openingKey.Year; day = 0;
                    leagueKey = openingKey.League; kind = RosterKind.OpeningSeason;
                    team = opening.Value ?? throw new CoreImportException("missing_roster"); break;
                default:
                    var openingDay = document.Envelope.Deserialize<Entity<Team, OpeningTeamDayKey>>() ?? throw new CoreImportException("invalid_roster_envelope");
                    var openingDayKey = openingDay.Key ?? throw new CoreImportException("missing_roster_key");
                    groupKey = openingDayKey.Group; basketKey = openingDayKey.BasketId; email = openingDayKey.Email; year = openingDayKey.Year; day = openingDayKey.Day;
                    leagueKey = openingDayKey.League; kind = RosterKind.OpeningMatchday;
                    team = openingDay.Value ?? throw new CoreImportException("missing_roster"); break;
            }
            addSeason(year);
            if ((kind is RosterKind.Matchday or RosterKind.OpeningMatchday) && day is < 1 or > 38) throw new CoreImportException("invalid_roster_day");
            var groupId = Existing("group", groupKey);
            var basketId = Existing("basket", Composite(groupKey, basketKey));
            var ownerEmail = OwnerKey(plan, groupId, email);
            if (OwnerKey(plan, groupId, team.Owner) != ownerEmail) throw new CoreImportException("roster_owner_mismatch");
            var originYear = kind is RosterKind.OpeningSeason or RosterKind.OpeningMatchday ? year - 1 : year;
            var teamMap = plan.Maps.SingleOrDefault(x => x.Kind == "team" && x.LegacyKey == Composite(groupKey, basketKey, originYear.ToString(CultureInfo.InvariantCulture), ownerEmail));
            // A historical owner cannot be guessed from a display name. Stop for an
            // explicit mapping instead of accidentally joining the wrong team's history.
            var metadata = teamMap is null ? null : plan.Teams.SingleOrDefault(x => x.Id == teamMap.TargetId);
            if (metadata is null) throw new CoreImportException("unresolved_historical_team_identity");
            Guid competition = leagueKey is null ? Guid.Empty : Existing("league", Composite(groupKey, leagueKey));
            if (!snapshots.Add((metadata.Id, kind, day, competition))) throw new CoreImportException("duplicate_roster_snapshot");
            var owner = ExistingOwner(plan, groupId, email, "unresolved_historical_team_identity");
            Member(groupId, owner.Id);
            var snapshot = new RosterSnapshot { GroupId = groupId, BasketId = basketId, SeasonIndex = year, TeamId = metadata.Id,
                Kind = kind, Day = day, TeamOriginYear = originYear, CompetitionId = competition, LeagueId = leagueKey is null ? null : competition,
                OwnerUserId = owner.Id, Name = Name(team.Name), MoneyFromRank = team.MoneyFromRank, OpeningCompetitionPrize = team.OpeningCompetitionPrize,
                FormationChanges = team.FormationChanges, LegacyLastUpdateLocal = team.LastUpdate == default ? null : Local(team.LastUpdate) };
            foreach (var coOwner in (team.AdditionalOwners ?? []).Distinct(StringComparer.Ordinal).Where(value => OwnerKey(plan, groupId, value) != ownerEmail))
            {
                var person = addOwner(groupId, coOwner, coOwner); Member(groupId, person.Id);
                if (!snapshot.AdditionalOwners.Any(x => x.UserId == person.Id)) snapshot.AdditionalOwners.Add(new() { UserId = person.Id });
            }
            var active = new HashSet<Guid>();
            foreach (var (player, ordinal) in (team.Players ?? []).Select((value, ordinal) => (value, ordinal)))
            {
                var playerSeason = Player(player, year, false, PlayerOrigin(document, $".p[{ordinal}]"));
                if ((int)player.Status is < 0 or > 4 || (int)player.Position is < -2 or > 11 ||
                    (player.Status == PlayerInTeamStatus.Active && !active.Add(playerSeason.PlayerId))) throw new CoreImportException("invalid_roster_player");
                snapshot.Players.Add(new() { Ordinal = ordinal, PlayerId = playerSeason.PlayerId, ClubId = Club(player.RealTeam),
                    PlayerRole = (int)player.Role, IsActive = player.IsActive, IsVisible = player.Visible, Price = player.Price, Revenue = player.Revenue,
                    Status = (int)player.Status, Position = (int)player.Position });
            }
            foreach (var (change, ordinal) in (team.FormationChangeHistory ?? []).Select((value, ordinal) => (value, ordinal)))
                snapshot.FormationHistory.Add(new() { Ordinal = ordinal, Author = change.Author, LegacyDateLocal = Local(change.Date),
                    Players = change.Players.Select((value, index) => new RosterPlayerChange { Ordinal = index, PlayerName = value.Name,
                        FromPosition = (int)value.FromPosition, ToPosition = (int)value.ToPosition }).ToArray() });
            plan.Rosters.Add(snapshot);
        }

        var matchdays = new HashSet<(int, int)>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "realcalendar"))
        {
            var calendar = document.Envelope.Deserialize<Entity<RealCalendar, int>>() ?? throw new CoreImportException("invalid_calendar_envelope");
            var value = calendar.Value ?? throw new CoreImportException("missing_calendar");
            if (value.Year != calendar.Key) throw new CoreImportException("calendar_season_mismatch");
            addSeason(calendar.Key);
            Root(document.Container, calendar.Key, 0);
            map("calendar", calendar.Key.ToString(CultureInfo.InvariantCulture));
            foreach (var day in value.Days ?? [])
            {
                if (day.SerieADay is < 1 or > 38 || !matchdays.Add((calendar.Key, day.SerieADay))) throw new CoreImportException("duplicate_real_matchday");
                var row = new FootballMatchday { SeasonIndex = calendar.Key, SerieADay = day.SerieADay };
                foreach (var (game, ordinal) in (day.Games ?? []).Select((value, index) => (value, index)))
                    row.Matches.Add(new() { SeasonIndex = calendar.Key, SerieADay = day.SerieADay, Ordinal = ordinal,
                        HomeClubId = Club(game.Home), AwayClubId = Club(game.Away), HomeGoals = game.HomeGoal, AwayGoals = game.AwayGoal,
                        Delayed = game.Delayed, LegacyKickoffLocal = game.Date is { } date ? Local(date) : null,
                        KickoffUtc = game.Date is { } kickoff ? ToUtc(kickoff) : null });
                plan.Matchdays.Add(row);
            }
        }

        LoadFantasyCompetitions(dataset, plan, map, Player, Club);
        LoadAuctions(dataset, plan, options, map, Player, Club, addUser);
        LoadHallArchives(dataset, plan, map, Person, Club, addOwner, addSeason);
        LoadPushOutbox(dataset, plan, map, Player, Club, addUser, addSeason);

        Guid Existing(string kind, string key) => plan.Maps.SingleOrDefault(x => x.Kind == kind && x.LegacyKey == key)?.TargetId
            ?? throw new CoreImportException("missing_roster_reference");
        void Member(Guid groupId, Guid userId)
        {
            if (!plan.Memberships.Any(x => x.GroupId == groupId && x.UserId == userId)) plan.Memberships.Add(new() { GroupId = groupId, UserId = userId, Roles = GroupRole.None });
        }
    }
    private static DateTime Local(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    private static DateTimeOffset ToUtc(DateTime value)
    {
        var wall = Local(value);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        if (zone.IsInvalidTime(wall) || zone.IsAmbiguousTime(wall)) throw new CoreImportException("ambiguous_legacy_kickoff");
        return new(TimeZoneInfo.ConvertTimeToUtc(wall, zone));
    }
}
