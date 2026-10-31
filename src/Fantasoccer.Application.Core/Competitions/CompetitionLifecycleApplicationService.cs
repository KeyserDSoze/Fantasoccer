using System.Security.Claims;
using System.Security.Cryptography;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Application.Core.Competitions;

public sealed record GenerateCompetitionRequest(int? ExpectedVersion = null, bool ReplaceExisting = false);
public sealed record RecalculateCompetitionRequest(int Day, bool OnlyLastDay = false, bool PreciseDay = false, int? ExpectedVersion = null, bool OnlyUnscored = false);
public sealed record RecalculationOutcome(bool Changed, bool AwaitingRecovery);

public sealed class CompetitionLifecycleApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions,
    IFactory<ILeagueCalculator> calculators, ITeamCalculator scoring, CompetitionTeamResolver resolver, LeagueLineupApplicationService lineups, TimeProvider clock)
{
    public async Task GenerateAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, GenerateCompetitionRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var season = await ManageAsync(caller, group, league, year, cancellationToken);
        var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        var settings = LeagueRulesMapping.ToDomain(rules);
        var type = Type(season);
        var calculator = Calculator(type);
        var existing = await CalendarAsync(group, league, year, cancellationToken);
        if (existing is not null && (!request.ReplaceExisting || request.ExpectedVersion != existing.Version))
            throw new ApplicationFault(409, "calendar_already_exists", "Il calendario esiste già: servono conferma esplicita e versione corrente per sostituirlo.");
        if (existing is not null && (existing.Stages.SelectMany(x => x.Days).SelectMany(x => x.Games).Any(x => x.Result is { IsCancelled: false }) ||
            await database.PlayedMatchSnapshots.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken)))
            throw new ApplicationFault(409, "calendar_has_results", "Un calendario con risultati storici non può essere sostituito da questa operazione.");
        // Explicit participants only (derived teams carry their origin's owners).
        var teams = (await TeamsAsync(group, league, year, cancellationToken)).Where(x => !x.IsArchived).ToArray();
        if (teams.Any(x => x.Primary is null)) throw new ApplicationFault(400, "invalid_competition_teams", "Ogni squadra partecipante deve avere un proprietario principale.");
        var members = teams.Select(team => new AnnualTeam { Name = team.Name, Owner = team.OwnerReference, AdditionalOwners = team.AdditionalOwners.ToList() }).ToList();
        if (members.Count < 2 || members.Select(x => x.Owner).Distinct(StringComparer.OrdinalIgnoreCase).Count() != members.Count || members.Count % 2 != 0)
            throw new ApplicationFault(400, "invalid_competition_teams", "Servono almeno due squadre, in numero pari e con proprietari principali distinti.");
        if (type == LeagueType.Cup)
        {
            if (members.Count < 16) throw new ApplicationFault(400, "cup_requires_sixteen_teams", "La coppa richiede almeno sedici squadre.");
            var previous = await database.FantasyStandingSnapshots.AsNoTracking().Where(x => x.GroupId == group && x.SeasonIndex == year - 1 && x.SnapshotDay == 0 &&
                database.Leagues.Any(leagueRow => leagueRow.GroupId == group && leagueRow.LeagueId == x.LeagueId && leagueRow.IsMain)).AsSplitQuery().ToArrayAsync(cancellationToken);
            var qualified = previous.SelectMany(x => x.Stages).SelectMany(x => x.Teams).OrderByDescending(x => x.Points).Take(8).Select(x => x.Owner).ToHashSet(StringComparer.OrdinalIgnoreCase);
            members = members.Where(x => qualified.Contains(x.Owner)).Concat(members.Where(x => !qualified.Contains(x.Owner)).OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))).Take(16).ToList();
        }
        var domain = calculator.GetRandomCalendar(year, new() { Year = year, Type = type, Settings = settings }, members);
        if (domain.Rounds.Count == 0) throw new ApplicationFault(400, "empty_generated_calendar", "Il regolamento non produce alcuna giornata.");
        if (existing is not null)
        {
            database.FantasyCalendars.Remove(existing);
            await database.SaveChangesAsync(cancellationToken);
        }
        var calendar = new FantasyCalendar { GroupId = group, LeagueId = league, SeasonIndex = year, Version = (existing?.Version ?? 0) + 1 };
        AppendCalendar(calendar, domain, teams);
        database.FantasyCalendars.Add(calendar);
        var rank = calculator.GetRank(settings, domain);
        await StoreRankAsync(group, league, year, rank, teams, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<RecalculationOutcome> RecalculateAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, RecalculateCompetitionRequest request, CancellationToken cancellationToken = default)
        => RecalculateCoreAsync(caller, group, league, year, request, cancellationToken);

    public Task<RecalculationOutcome> RecalculateScheduledAsync(Guid group, Guid league, int year, int day, CancellationToken cancellationToken = default)
        => RecalculateCoreAsync(null, group, league, year, new(day, PreciseDay: true, OnlyUnscored: true), cancellationToken);

    private async Task<RecalculationOutcome> RecalculateCoreAsync(ClaimsPrincipal? caller, Guid group, Guid league, int year, RecalculateCompetitionRequest request, CancellationToken cancellationToken)
    {
        if (request.Day is < 1 or > 38) throw new ApplicationFault(400, "invalid_recalculation_day", "Giornata non valida.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var season = caller != null ? await ManageAsync(caller, group, league, year, cancellationToken) :
            await database.LeagueSeasons.AsNoTracking().Include(x => x.League).SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && !x.IsArchived && !x.League.IsArchived, cancellationToken)
                ?? throw new ApplicationFault(404, "competition_not_found", "Competizione attiva non trovata.");
        var calendar = await CalendarAsync(group, league, year, cancellationToken) ?? throw new ApplicationFault(404, "calendar_not_found", "Calendario non disponibile.");
        if (request.ExpectedVersion is { } version && version != calendar.Version) throw new ApplicationFault(409, "calendar_changed", "Ricaricare il calendario prima di ricalcolare.");
        var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        var settings = LeagueRulesMapping.ToDomain(rules);
        var type = Type(season);
        var calculator = Calculator(type);
        var selectedDays = calendar.Stages.SelectMany(x => x.Days).Where(x => request.PreciseDay || request.OnlyLastDay ? x.SerieADay == request.Day : x.SerieADay <= request.Day).ToArray();
        if (request.OnlyUnscored && selectedDays.All(day => day.Games.All(game => game.Result != null))) return new(false, false);
        var changed = false; var metadataChanged = false; var awaiting = false;
        foreach (var day in selectedDays)
        {
            if (request.OnlyUnscored && day.Games.All(x => x.Result != null)) continue;
            if (day.FantasyTurn <= settings.CancelledDay)
            {
                foreach (var game in day.Games.Where(x => !request.OnlyUnscored || x.Result == null)) game.Result = new() { IsCancelled = true };
                day.AwaitingPostponedMatches = false; changed = true;
                continue;
            }
            var realGames = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.SerieADay == day.SerieADay).ToArrayAsync(cancellationToken);
            var votedClubs = await database.FootballVotes.Where(x => x.SeasonIndex == year && x.Day == day.SerieADay && x.Source == FootballVoteSource.Official && x.IsFinal && x.HasVote)
                .Select(x => x.ClubId).Distinct().ToArrayAsync(cancellationToken);
            var ready = MatchdayReady(realGames, settings.Postponements.WaitForRecovery, votedClubs.ToHashSet(), clock.GetUtcNow());
            if (!ready && settings.Postponements.WaitForRecovery && realGames.Any(x => x.WasPostponed || x.Delayed))
            {
                if (day.Games.All(x => x.Result is not null))
                    throw new ApplicationFault(409, "postponed_recovery_not_finished", "I recuperi non sono ancora completi: il risultato già salvato non viene cancellato o sostituito.");
                awaiting = true;
                if (!day.AwaitingPostponedMatches) { day.AwaitingPostponedMatches = true; metadataChanged = true; }
                continue;
            }
            if (!ready)
                throw new ApplicationFault(409, "official_matchday_incomplete", "La giornata non è conclusa o mancano voti ufficiali per una squadra reale. Nessun risultato è stato modificato.");
            if (day.AwaitingPostponedMatches) { day.AwaitingPostponedMatches = false; metadataChanged = true; }
            await scoring.BuildRequestAsync(year, day.SerieADay, type, settings, true, true, false);
            foreach (var game in day.Games)
            {
                if (request.OnlyUnscored && game.Result != null) continue;
                if (game.HomeTeamId is null || game.AwayTeamId is null) continue;
                // The competition's own lineup over the authoritative day roster.
                var home = await lineups.ScoringPlayersAsync(group, league, year, day.SerieADay, game.HomeTeamId.Value, game.Id, cancellationToken);
                var away = await lineups.ScoringPlayersAsync(group, league, year, day.SerieADay, game.AwayTeamId.Value, game.Id, cancellationToken);
                var homePoint = scoring.CalculatePoint(home.Where(x => x.Status == 0).Select(CompetitionMapping.Domain));
                var awayPoint = scoring.CalculatePoint(away.Where(x => x.Status == 0).Select(CompetitionMapping.Domain));
                homePoint = new(homePoint.Value + settings.PointInHome, homePoint.DefensiveBonus, homePoint.GoodPeople, homePoint.OwnGoal);
                var result = new GameResult { Home = homePoint, Away = awayPoint };
                result.CalculateGoals(settings);
                game.Result = CompetitionMapping.Saved(result);
                changed = true;
                var saved = await database.PlayedMatchSnapshots.SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.MatchId == game.Id && x.Day == day.SerieADay, cancellationToken);
                if (saved is null) database.PlayedMatchSnapshots.Add(saved = new() { GroupId = group, LeagueId = league, SeasonIndex = year, MatchId = game.Id, Day = day.SerieADay,
                    HomeName = game.HomeName, AwayName = game.AwayName });
                saved.Result = CompetitionMapping.Saved(result);
                saved.Players.Clear();
                foreach (var (isHome, roster) in new[] { (true, home), (false, away) })
                    foreach (var player in roster)
                        saved.Players.Add(new() { IsHome = isHome, Ordinal = player.Ordinal, PlayerId = player.PlayerId, ClubId = player.ClubId,
                            PlayerRole = player.PlayerRole, IsActive = player.IsActive, IsVisible = player.IsVisible, Price = player.Price, Revenue = player.Revenue,
                            Status = player.Status, Position = player.Position });
            }
        }
        if (!changed)
        {
            if (metadataChanged) { calendar.Version++; await database.SaveChangesAsync(cancellationToken); }
            await transaction.CommitAsync(cancellationToken);
            return new(metadataChanged, awaiting);
        }
        var domain = CompetitionMapping.Domain(calendar);
        var teams = await TeamsAsync(group, league, year, cancellationToken);
        var rank = calculator.GetRank(settings, new() { Year = year, Rounds = domain.Rounds.Where(x => x.Value.Count > 0).ToDictionary(x => x.Key, x => x.Value) });
        // Persist only the latest rank. Historical reads project the saved
        // calendar/results with the annual rules, without storing daily copies.
        await StoreRankAsync(group, league, year, rank, teams, cancellationToken);
        var annualTeams = teams.Where(x => x.Primary is not null).Select(x => new AnnualTeam { Name = x.Name, Owner = x.OwnerReference, AdditionalOwners = [] }).ToList();
        var expanded = calculator.AddDaysInLeague(year, new() { Year = year, Type = type, Settings = settings }, annualTeams, rank, domain);
        AppendCalendar(calendar, expanded, teams);
        calendar.Version++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, awaiting);
    }

    internal static bool MatchdayReady(FootballMatch[] matches, bool wait, HashSet<string?> actualClubs, DateTimeOffset now)
    {
        if (matches.Length == 0) return false;
        var required = matches.Where(x => wait || !x.WasPostponed && !x.Delayed).ToArray();
        if (required.Length == 0)
        {
            var original = matches.Select(x => x.OriginalKickoffUtc ?? x.KickoffUtc).ToArray();
            return original.All(x => x is not null) && now >= original.Max()!.Value.AddMinutes(135);
        }
        return required.All(game => !game.Delayed &&
            (game.IsFinished == true || game.IsFinished is null && game.KickoffUtc is { } kickoff && now >= kickoff.AddMinutes(135)) &&
            (game.ActualOfficialVotesReady ?? (actualClubs.Contains(game.HomeClubId) && actualClubs.Contains(game.AwayClubId))));
    }

    private async Task<LeagueSeason> ManageAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken)
        => await competitions.AuthorizeManagerAsync(caller, group, league, year, cancellationToken);
    private Task<FantasyCalendar?> CalendarAsync(Guid group, Guid league, int year, CancellationToken cancellationToken) => database.FantasyCalendars
        .Include(x => x.Stages).ThenInclude(x => x.Days).ThenInclude(x => x.Games).Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
    private Task<ResolvedTeam[]> TeamsAsync(Guid group, Guid league, int year, CancellationToken cancellationToken) =>
        resolver.ParticipantsAsync(group, league, year, includeArchived: true, cancellationToken);
    private ILeagueCalculator Calculator(LeagueType type) => calculators.Create(type.ToString()) ?? throw new ApplicationFault(409, "league_calculator_unavailable", "Calcolatore non disponibile.");
    private static LeagueType Type(LeagueSeason season) => (LeagueType)(season.CompetitionType != 0 ? season.CompetitionType : season.League.CompetitionType != 0 ? season.League.CompetitionType : 1);

    private async Task StoreRankAsync(Guid group, Guid league, int year, Rank rank, ResolvedTeam[] teams, CancellationToken cancellationToken)
    {
        await database.FantasyStandingSnapshots.Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.SnapshotDay == 0).ExecuteDeleteAsync(cancellationToken);
        var snapshot = new FantasyStandingSnapshot { GroupId = group, LeagueId = league, SeasonIndex = year, SnapshotDay = 0, SerieADay = rank.SerieADay };
        foreach (var (name, rows) in rank.RankedTeams)
        {
            var stage = new FantasyStandingStage { Stage = name, Ordinal = snapshot.Stages.Count };
            foreach (var row in rows)
            {
                var metadata = Resolve(teams, row.Owner);
                stage.Teams.Add(new() { Ordinal = stage.Teams.Count, BasketId = metadata.BasketId, TeamId = metadata.TeamId, Name = row.Name, Owner = row.Owner,
                    Points = row.Point, Victories = row.Victories, Draws = row.Draws, Defeats = row.Defeats, GoalsFor = row.Goal, GoalsAgainst = row.SufferedGoal,
                    ValuePoints = row.ValuePoint, SufferedValuePoints = row.SufferedValuePoint, Money = row.Money, PlusMoney = row.PlusMoney });
            }
            snapshot.Stages.Add(stage);
        }
        database.FantasyStandingSnapshots.Add(snapshot);
    }
    // Within one league a user controls at most one participant (database rule),
    // so the legacy owner-based calendar engine still resolves teams uniquely.
    private static ResolvedTeam Resolve(ResolvedTeam[] teams, string owner)
    {
        var matches = teams.Where(x => string.Equals(x.OwnerReference, owner, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new ApplicationFault(409, "ambiguous_competition_owner", "Il proprietario non identifica più una sola squadra annuale: riconciliare il calendario prima di ricalcolare.");
    }
    private static void AppendCalendar(FantasyCalendar calendar, Calendar domain, ResolvedTeam[] teams)
    {
        var known = calendar.Stages.SelectMany(x => x.Days).SelectMany(x => x.Games).Select(x => x.Id).ToHashSet();
        foreach (var (name, days) in domain.Rounds)
        {
            var stage = calendar.Stages.SingleOrDefault(x => x.Stage == name);
            if (stage is null) calendar.Stages.Add(stage = new() { GroupId = calendar.GroupId, LeagueId = calendar.LeagueId, SeasonIndex = calendar.SeasonIndex, Stage = name, Ordinal = calendar.Stages.Count });
            foreach (var day in days)
            {
                var incoming = day.Games.Where(game => !Guid.TryParse(game.Id, out var id) || !known.Contains(id)).ToArray();
                if (incoming.Length == 0) continue;
                if (day.SerieADay is < 1 or > 38) throw new ApplicationFault(400, "generated_matchday_out_of_season", "Il calendario supera le giornate disponibili.");
                var row = new FantasyMatchday { GroupId = calendar.GroupId, LeagueId = calendar.LeagueId, SeasonIndex = calendar.SeasonIndex,
                    Stage = name, Ordinal = stage.Days.Count, FantasyTurn = day.Number, SerieADay = day.SerieADay };
                foreach (var game in incoming)
                {
                    var home = Resolve(teams, game.HomeOwner); var away = Resolve(teams, game.AwayOwner);
                    var id = Guid.TryParse(game.Id, out var parsed) ? parsed : Guid.NewGuid();
                    row.Games.Add(new() { GroupId = calendar.GroupId, LeagueId = calendar.LeagueId, SeasonIndex = calendar.SeasonIndex, Id = id,
                        Stage = name, DayOrdinal = row.Ordinal, Ordinal = row.Games.Count, Number = game.Number, HomeName = game.Home, AwayName = game.Away,
                        HomeOwner = game.HomeOwner, AwayOwner = game.AwayOwner, HomeTeamId = home.TeamId, HomeBasketId = home.BasketId,
                        AwayTeamId = away.TeamId, AwayBasketId = away.BasketId, Result = CompetitionMapping.Saved(game.Result) });
                    known.Add(id);
                }
                stage.Days.Add(row);
            }
        }
    }
}
