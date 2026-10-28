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
        var clubLabels = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var memberships = new Dictionary<(string Code, int Year), FootballClubSeason>();
        var players = new Dictionary<string, FootballPlayer>(StringComparer.Ordinal);
        var seasons = new Dictionary<(string Player, int Year), PlayerSeason>();
        var partialVoteSeasons = new HashSet<(string Player, int Year)>();
        var rosterClubCandidates = new Dictionary<(string Player, int Year), Dictionary<string, PlayerSource>>();
        var authoritative = new HashSet<(string Player, int Year)>();
        var roots = new HashSet<(string Kind, int Year, int Day)>();
        var resolvedClubs = new HashSet<(string Document, string Path)>();
        var catalogResolutions = PreparePlayerCatalogResolutions(dataset, options.PlayerCatalogResolutions);
        void Root(string kind, int year, int day)
        {
            if (!roots.Add((kind, year, day))) throw new CoreImportException("duplicate_football_dataset");
            map("football-dataset", Composite(kind, year.ToString(CultureInfo.InvariantCulture), day.ToString(CultureInfo.InvariantCulture)));
            plan.Datasets.Add(new() { Kind = kind, SeasonIndex = year, Day = day });
        }
        string Club(RealTeam? club, ClubSource origin)
        {
            var key = CheckedClubCode(club, origin);
            // Report catalogue resolutions: a missing code or a documented
            // provider code variant replaced by the verified club code.
            var explicitCode = club!.ExplicitAbbreviation?.Trim().ToLowerInvariant();
            if ((string.IsNullOrWhiteSpace(explicitCode) || explicitCode != key) && FantacalcioClubCodes.TryGetCode(club.Name, out _) &&
                resolvedClubs.Add((origin.Document.Sha256, origin.JsonPath)))
                plan.ResolvedClubCodes.Add(new(origin.Document.Sha256, origin.Document.Container, DocumentArchiveReference(origin.Document), origin.JsonPath,
                    FantacalcioClubCodes.Version));
            var name = FantacalcioClubCodes.CanonicalName(key, club.Name);
            Label(club.Name, key); Label(name, key);
            if (clubs.TryGetValue(key, out var existing)) return existing.Id;
            var row = new FootballClub { Id = key, LegacyId = map("club", club.Name.Trim().ToLowerInvariant()), Name = name, Abbreviation = key };
            clubs.Add(key, row); plan.Clubs.Add(row); return row.Id;
        }
        // Legacy rosters and hall snapshots of 2012/13-2021/22 store "t":{"n":""}:
        // the day's club was never recorded. Keep NULL instead of inventing one;
        // a club with a name or code keeps every existing validation.
        string? OptionalClub(RealTeam? club, ClubSource origin) => UnrecordedClub(club) ? null : Club(club, origin);
        void Label(string name, string code)
        {
            if (!clubLabels.TryGetValue(name.Trim(), out var values)) clubLabels.Add(name.Trim(), values = new(StringComparer.Ordinal));
            values.Add(code);
        }
        string RankClub(string? name, ClubSource origin)
        {
            if (name is not null && clubLabels.TryGetValue(name.Trim(), out var matches))
            {
                if (matches.Count != 1) throw new CoreImportException("ambiguous_real_rank_club");
                return matches.Single();
            }
            return Club(new() { Name = name! }, origin);
        }
        void Membership(string code, int year, ClubMembershipSource source)
        {
            addSeason(year);
            if (!memberships.TryGetValue((code, year), out var row))
            {
                row = new() { ClubId = code, SeasonIndex = year, Sources = source };
                memberships.Add((code, year), row); plan.ClubSeasons.Add(row);
            }
            else row.Sources |= source;
        }
        string Person(RealPlayer player, PlayerSource origin)
        {
            var legacyKey = CheckedPlayerKey(player, origin);
            if (!players.TryGetValue(legacyKey, out var person))
            {
                person = new() { Id = legacyKey, LegacyId = map("player", legacyKey), Name = Name(player.Name) };
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
                var missingVoteClub = player.RealTeam is null && AllowsMissingVoteClub(origin) ||
                    origin.Document.Container is "team" or "dailyteams" or "openingteams" or "openingdailyteams" && UnrecordedClub(player.RealTeam);
                row = new() { PlayerId = playerId, SeasonIndex = year, ClubId = missingVoteClub ? null : Club(player.RealTeam, ClubOrigin(origin)),
                    Position = (PlayerPosition)(int)player.Role, IsActive = player.IsActive, IsVisible = player.Visible };
                seasons.Add(identity, row); plan.PlayerSeasons.Add(row);
                if (missingVoteClub) partialVoteSeasons.Add(identity);
            }
            return row;
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "realteamwrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<RealTeamWrapper, int>>() ?? throw new CoreImportException("invalid_club_envelope");
            addSeason(entity.Key);
            Root(document.Container, entity.Key, 0);
            foreach (var (club, index) in (entity.Value?.Teams ?? throw new CoreImportException("missing_clubs")).Select((club, index) => (club, index)))
                Membership(Club(club, ClubOrigin(document, $".t[{index}]")), entity.Key, ClubMembershipSource.Catalogue);
            Root(FootballClubSeason.CatalogueDatasetKind, entity.Key, 0);
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "realplayerswrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<RealPlayersWrapper, int>>() ?? throw new CoreImportException("invalid_players_envelope");
            addSeason(entity.Key);
            Root(document.Container, entity.Key, 0);
            var catalog = entity.Value?.Players ?? throw new CoreImportException("missing_players");
            catalogResolutions.TryGetValue(document.Sha256, out var resolution);
            var unnamed = catalog.Count(x => string.IsNullOrWhiteSpace(x?.Name));
            if (resolution is not null && unnamed != resolution.ExpectedUnnamedPlayers)
                throw new CoreImportException("player_catalog_resolution_count_mismatch", documentSha256: document.Sha256);
            foreach (var (player, index) in catalog.Select((value, index) => (value, index)))
            {
                if (string.IsNullOrWhiteSpace(player?.Name))
                {
                    PreserveUnnamedCatalogEntry(dataset, plan, document, player, entity.Key, index, map);
                    continue;
                }
                Player(player, entity.Key, true, PlayerOrigin(document, $".p[{index}]"));
            }
            if (unnamed > 0 || resolution is not null) plan.ResolvedPlayerCatalogs.Add(new(document.Sha256, DocumentArchiveReference(document), entity.Key,
                catalog.Count, catalog.Count - unnamed, unnamed, resolution is not null));
        }

        LoadFootballSamples(dataset, plan, Player, Club, addSeason, Root, RankClub, Membership, map);

        var snapshots = new HashSet<(Guid Team, RosterKind Kind, int Day, Guid Competition)>();
        // All keys first: a roster saved under the wrong basket is compared with
        // the copy in its team's basket wherever that copy appears in the dump.
        var rosters = dataset.Documents.Where(x => x.Container is "team" or "dailyteams" or "openingteams" or "openingdailyteams").Select(ReadRoster).ToArray();
        foreach (var roster in rosters)
        {
            var (document, groupKey, basketKey, email, year, day, leagueKey, team, kind) = roster;
            addSeason(year);
            if ((kind is RosterKind.Matchday or RosterKind.OpeningMatchday) && day is < 1 or > 38) throw new CoreImportException("invalid_roster_day");
            var groupId = Existing("group", groupKey);
            var basketId = Existing("basket", Composite(groupKey, basketKey));
            var ownerEmail = OwnerKey(plan, groupId, email);
            if (OwnerKey(plan, groupId, team.Owner) != ownerEmail) throw new CoreImportException("roster_owner_mismatch");
            var originYear = roster.OriginYear;
            var teamMap = plan.Maps.SingleOrDefault(x => x.Kind == "team" && x.LegacyKey == Composite(groupKey, basketKey, originYear.ToString(CultureInfo.InvariantCulture), ownerEmail));
            // A historical owner cannot be guessed from a display name. Stop for an
            // explicit mapping instead of accidentally joining the wrong team's history.
            var metadata = teamMap is null ? null : plan.Teams.SingleOrDefault(x => x.Id == teamMap.TargetId);
            if (metadata is null)
            {
                if (SkipUnplacedRoster(plan, rosters, roster, groupId, ownerEmail)) continue;
                throw UnresolvedTeam(document, "$.Key.e");
            }
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
            var active = new HashSet<string>(StringComparer.Ordinal);
            var missingClubs = 0; string? firstMissingClub = null;
            foreach (var (player, ordinal) in (team.Players ?? []).Select((value, ordinal) => (value, ordinal)))
            {
                var origin = PlayerOrigin(document, $".p[{ordinal}]");
                var rawPlayer = Value(document.Envelope).GetProperty("p")[ordinal];
                if (ExcludedNamelessRosterPlayer(player, rawPlayer))
                {
                    // Operator decision: a sold/removed entry without any name or
                    // stored key is not imported; its money is reported, not lost.
                    plan.ExcludedNamelessRosterEntries.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document), origin.JsonPath,
                        year, day, player!.Price, player.Revenue, (int)player.Status, (int)player.Position));
                    continue;
                }
                ValidateRosterPlayerReference(player, rawPlayer, players, origin);
                var wasKnown = players.ContainsKey(player.Key);
                var wasSeasonKnown = wasKnown && seasons.ContainsKey((player.Key, year));
                var playerSeason = Player(player, year, false, origin);
                if (!wasSeasonKnown) plan.RecoveredRosterPlayers.Add(new(document.Sha256, DocumentArchiveReference(document), origin.JsonPath, year, !wasKnown));
                if ((int)player.Status is < 0 or > 4 || (int)player.Position is < -2 or > 11 ||
                    (player.Status == PlayerInTeamStatus.Active && !active.Add(playerSeason.PlayerId))) throw new CoreImportException("invalid_roster_player");
                var clubCode = OptionalClub(player.RealTeam, ClubOrigin(origin));
                if (clubCode is null) { missingClubs++; firstMissingClub ??= ClubOrigin(origin).JsonPath; }
                snapshot.Players.Add(new() { Ordinal = ordinal, PlayerId = playerSeason.PlayerId, ClubId = clubCode,
                    PlayerRole = (int)player.Role, IsActive = player.IsActive, IsVisible = player.Visible, Price = player.Price, Revenue = player.Revenue,
                    Status = (int)player.Status, Position = (int)player.Position });
                if (kind == RosterKind.Season && clubCode is not null && partialVoteSeasons.Contains((playerSeason.PlayerId, year)))
                {
                    var identity = (playerSeason.PlayerId, year);
                    if (!rosterClubCandidates.TryGetValue(identity, out var candidates)) rosterClubCandidates.Add(identity, candidates = new(StringComparer.Ordinal));
                    candidates.TryAdd(clubCode, origin);
                }
            }
            if (missingClubs > 0) plan.MissingRosterClubSnapshots.Add(new(document.Sha256, document.Container, DocumentArchiveReference(document),
                year, day, missingClubs, firstMissingClub!));
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
            if (OtherSeasonCalendar(document, calendar) is { } excluded)
            {
                // Not decoded for clubs, memberships or matches: another season.
                plan.ExcludedRealCalendars.Add(excluded);
                continue;
            }
            Root(document.Container, calendar.Key, 0);
            map("calendar", calendar.Key.ToString(CultureInfo.InvariantCulture));
            var futureScores = 0; string? firstFutureScore = null;
            foreach (var (day, dayIndex) in (value.Days ?? []).Select((day, index) => (day, index)))
            {
                if (day.SerieADay is < 1 or > 38 || !matchdays.Add((calendar.Key, day.SerieADay))) throw new CoreImportException("duplicate_real_matchday");
                var row = new FootballMatchday { SeasonIndex = calendar.Key, SerieADay = day.SerieADay };
                foreach (var (game, ordinal) in (day.Games ?? []).Select((value, index) => (value, index)))
                {
                    var home = Club(game.Home, ClubOrigin(document, $".d[{dayIndex}].g[{ordinal}].h"));
                    var away = Club(game.Away, ClubOrigin(document, $".d[{dayIndex}].g[{ordinal}].a"));
                    Membership(home, calendar.Key, ClubMembershipSource.Calendar);
                    Membership(away, calendar.Key, ClubMembershipSource.Calendar);
                    var future = FutureScore(game, dataset.Manifest.StartedAtUtc);
                    if (future) { futureScores++; firstFutureScore ??= ClubOrigin(document, $".d[{dayIndex}].g[{ordinal}]").JsonPath; }
                    row.Matches.Add(new() { SeasonIndex = calendar.Key, SerieADay = day.SerieADay, Ordinal = ordinal,
                        HomeClubId = home, AwayClubId = away, HomeGoals = future ? null : game.HomeGoal, AwayGoals = future ? null : game.AwayGoal,
                         Delayed = game.Delayed, WasPostponed = game.WasPostponed || game.Delayed, IsFinished = game.IsFinished,
                         OriginalKickoffUtc = game.OriginalDate is { } original ? ToUtc(original) : game.Date is { } scheduled ? ToUtc(scheduled) : null,
                         LegacyKickoffLocal = game.Date is { } date ? Local(date) : null,
                         KickoffUtc = game.Date is { } kickoff ? ToUtc(kickoff) : null });
                }
                plan.Matchdays.Add(row);
            }
            if (futureScores > 0) plan.DiscardedFutureScores.Add(new(document.Sha256, DocumentArchiveReference(document), calendar.Key,
                futureScores, dataset.Manifest.StartedAtUtc, firstFutureScore!));
        }

        AttestImportedPostponements(plan);
        LoadFantasyCompetitions(dataset, plan, map, Player, Club);
        LoadAuctions(dataset, plan, options, map, Player, Club, addUser);
        LoadHallArchives(dataset, plan, map, Person, OptionalClub, addOwner, addSeason);
        LoadPushOutbox(dataset, plan, map, Player, Club, addUser, addSeason);

        // The current annual record may be completed from a unique same-year
        // season-roster reference, but this never rewrites an unknown vote club.
        // Daily/opening snapshots do not establish the annual current club.
        foreach (var identity in partialVoteSeasons)
        {
            var row = seasons[identity];
            if (row.ClubId is not null || !rosterClubCandidates.TryGetValue(identity, out var candidates)) continue;
            if (candidates.Count != 1) { plan.AmbiguousRosterClubRecoveries++; continue; }
            var evidence = candidates.Single(); row.ClubId = evidence.Key;
            var origin = evidence.Value;
            plan.RecoveredRosterPlayers.Add(new(origin.Document.Sha256, DocumentArchiveReference(origin.Document), origin.JsonPath, identity.Year,
                CreatedGlobalPlayer: false, FilledAnnualClub: true));
        }

        Guid Existing(string kind, string key) => plan.Maps.SingleOrDefault(x => x.Kind == kind && x.LegacyKey == key)?.TargetId
            ?? throw new CoreImportException("missing_roster_reference");
        void Member(Guid groupId, Guid userId)
        {
            if (!plan.Memberships.Any(x => x.GroupId == groupId && x.UserId == userId)) plan.Memberships.Add(new() { GroupId = groupId, UserId = userId, Roles = GroupRole.None });
        }
    }
    private static DateTime Local(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    // No club at all ("t":null or "t":{"n":""} without a code), as opposed to
    // a recorded club that may still be invalid and keeps its diagnostics.
    private static bool UnrecordedClub(RealTeam? club)
        => club is null || string.IsNullOrWhiteSpace(club.Name) && string.IsNullOrWhiteSpace(club.ExplicitAbbreviation);
    private static DateTimeOffset ToUtc(DateTime value)
    {
        var wall = Local(value);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        if (zone.IsInvalidTime(wall) || zone.IsAmbiguousTime(wall)) throw new CoreImportException("ambiguous_legacy_kickoff");
        return new(TimeZoneInfo.ConvertTimeToUtc(wall, zone));
    }
}
