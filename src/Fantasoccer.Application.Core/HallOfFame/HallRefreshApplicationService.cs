using System.Security.Claims;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Application.Core.HallOfFame;

public sealed record RefreshHallRequest(int ExpectedVersion);
public sealed class HallRefreshApplicationService(FantasoccerDbContext database, IAuthorizationService authorization, IFactory<ILeagueCalculator> calculators, TimeProvider clock)
{
    public async Task RefreshAsync(ClaimsPrincipal caller, Guid group, Guid league, RefreshHallRequest request, CancellationToken cancellationToken = default)
    {
        await ManagerAsync(caller, group);
        await RefreshCoreAsync(group, league, request.ExpectedVersion, cancellationToken, caller);
    }
    public Task RefreshScheduledAsync(Guid group, Guid league, CancellationToken cancellationToken = default) => RefreshCoreAsync(group, league, null, cancellationToken);

    public async Task ConfirmBaselineAsync(ClaimsPrincipal caller, Guid group, Guid league, RefreshHallRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await ManagerAsync(caller, group);
        var archive = await ArchiveAsync(group, league, cancellationToken) ?? throw Missing();
        if (archive.Version != request.ExpectedVersion) throw Changed();
        if (archive.ContributionsInitialized) throw new ApplicationFault(409, "hall_baseline_already_initialized", "Il checkpoint dell'albo è già inizializzato.");
        if (archive.Ranks.GroupBy(x => x.OwnerUserId).Any(x => x.Count() > 1) || archive.WinningTeams.GroupBy(x => x.OwnerUserId).Any(x => x.Count() > 1) || archive.WinningPlayers.GroupBy(x => x.Player.PlayerId).Any(x => x.Count() > 1)) throw BaselineRequired();
        var snapshots = await database.FantasyStandingSnapshots.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SnapshotDay == 0).AsSplitQuery().ToArrayAsync(cancellationToken);
        var owners = await OwnersAsync(group, snapshots, cancellationToken);
        var actor = await database.Users.Where(x => x.Email == caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant() && x.IsEnabled).Select(x => x.Id).SingleAsync(cancellationToken);
        await database.HallRankContributions.Where(x => x.GroupId == group && x.LeagueId == league).ExecuteDeleteAsync(cancellationToken);
        foreach (var snapshot in snapshots) database.HallRankContributions.AddRange(HallContributionMath.FromSnapshot(snapshot, email => Owner(owners, group, email)));
        database.HallBaselineConfirmations.Add(new() { GroupId = group, LeagueId = league, ActorUserId = actor, ArchiveVersion = archive.Version, RecordedAtUtc = clock.GetUtcNow() });
        archive.ContributionsInitialized = true; archive.Version++;
        // Confirmation seals the current source baseline, never replaces the
        // imported career totals with a recomputation of incomplete history.
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    private async Task RefreshCoreAsync(Guid group, Guid league, int? expectedVersion, CancellationToken cancellationToken, ClaimsPrincipal? caller = null)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        if (caller != null) await ManagerAsync(caller, group);
        if (!await database.Leagues.AnyAsync(x => x.GroupId == group && x.LeagueId == league, cancellationToken)) throw Missing();
        var archive = await ArchiveAsync(group, league, cancellationToken);
        if (expectedVersion is { } version && version != (archive?.Version ?? 0)) throw Changed();
        var changed = archive == null;
        if (archive == null) database.HallArchives.Add(archive = new() { GroupId = group, LeagueId = league, ContributionsInitialized = true, Version = 0 });
        var snapshots = await database.FantasyStandingSnapshots.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SnapshotDay == 0).OrderBy(x => x.SeasonIndex).AsSplitQuery().ToArrayAsync(cancellationToken);
        var users = await OwnersAsync(group, snapshots, cancellationToken);
        List<HallRankContribution> baselines;
        if (archive.ContributionsInitialized) baselines = await database.HallRankContributions.Where(x => x.GroupId == group && x.LeagueId == league).ToListAsync(cancellationToken);
        else
        {
            // Imported career totals that do not match the seasons we have: if they contain them (older years, leagues born later and seasons not
            // played are only in the totals), those seasons are already counted and the checkpoint is sealed here. Otherwise an administrator decides.
            baselines = Reconcile(archive, snapshots, users, group);
            await database.HallRankContributions.Where(x => x.GroupId == group && x.LeagueId == league).ExecuteDeleteAsync(cancellationToken);
            database.HallRankContributions.AddRange(baselines);
            database.HallBaselineConfirmations.Add(new() { GroupId = group, LeagueId = league, ActorUserId = null, ArchiveVersion = archive.Version, RecordedAtUtc = clock.GetUtcNow() });
            archive.ContributionsInitialized = true; changed = true;
        }
        foreach (var snapshot in snapshots)
        {
            var current = HallContributionMath.FromSnapshot(snapshot, email => Owner(users, group, email));
            foreach (var user in current.Select(x => x.OwnerUserId).Union(baselines.Where(x => x.SeasonIndex == snapshot.SeasonIndex).Select(x => x.OwnerUserId)).ToArray())
            {
                var next = current.SingleOrDefault(x => x.OwnerUserId == user) ?? new() { GroupId = group, LeagueId = league, SeasonIndex = snapshot.SeasonIndex, OwnerUserId = user };
                var old = baselines.SingleOrDefault(x => x.SeasonIndex == snapshot.SeasonIndex && x.OwnerUserId == user);
                if (old != null && Equal(old, next)) continue;
                var rows = archive.Ranks.Where(x => x.OwnerUserId == user).ToArray();
                if (rows.Length > 1) throw BaselineRequired("L'albo ha più righe per lo stesso proprietario: serve una verifica manuale.");
                var rank = rows.SingleOrDefault();
                if (rank == null)
                {
                    var name = snapshot.Stages.SelectMany(x => x.Teams).FirstOrDefault(x => Owner(users, group, x.Owner) == user);
                    archive.Ranks.Add(rank = new() { Ordinal = archive.Ranks.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1, OwnerUserId = user,
                        OwnerEmail = name?.Owner ?? "user:" + user.ToString("D"), Name = name?.Name ?? "" });
                }
                ApplyDelta(rank, next, old);
                if (old == null) { database.HallRankContributions.Add(next); baselines.Add(next); }
                else Copy(old, next);
                changed = true;
            }
        }
        var calendars = await database.FantasyCalendars.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Days).ThenInclude(x => x.Games)
            .Where(x => x.GroupId == group && x.LeagueId == league).OrderBy(x => x.SeasonIndex).AsSplitQuery().ToArrayAsync(cancellationToken);
        foreach (var calendar in calendars)
        {
            var games = calendar.Stages.SelectMany(x => x.Days).SelectMany(x => x.Games).ToArray();
            var record = games.Where(x => x.Result is { IsCancelled: false, Home: not null, Away: not null }).OrderByDescending(x => Math.Max(x.Result!.Home!.Value, x.Result.Away!.Value)).FirstOrDefault();
            var oldScore = archive.RecordGame?.Result is { } saved ? Math.Max(saved.Home?.Value ?? 0, saved.Away?.Value ?? 0) : float.NegativeInfinity;
            if (record?.Result is { } result && Math.Max(result.Home!.Value, result.Away!.Value) > oldScore)
            {
                archive.RecordGame = new() { Year = calendar.SeasonIndex, Id = record.Id, LinkedMatchId = record.Id, LegacyGameId = record.Id.ToString(), Number = record.Number,
                    Home = record.HomeName, HomeOwner = record.HomeOwner, Away = record.AwayName, AwayOwner = record.AwayOwner, Result = CompetitionMapping.Saved(CompetitionMapping.Domain(result)) };
                changed = true;
            }
            var snapshot = snapshots.SingleOrDefault(x => x.SeasonIndex == calendar.SeasonIndex);
            if (snapshot == null || games.Length == 0 || games.Any(x => x.Result == null)) continue;
            var season = await database.LeagueSeasons.AsNoTracking().Include(x => x.League).SingleAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == calendar.SeasonIndex, cancellationToken);
            var type = (LeagueType)(season.CompetitionType != 0 ? season.CompetitionType : season.League.CompetitionType != 0 ? season.League.CompetitionType : 1);
            var calculator = calculators.Create(type.ToString()) ?? throw new ApplicationFault(409, "league_calculator_unavailable", "Calcolatore dell'albo non disponibile.");
            var rank = new Rank { SerieADay = snapshot.SerieADay, RankedTeams = snapshot.Stages.ToDictionary(x => x.Stage, x => x.Teams.OrderBy(row => row.Ordinal).Select(row => new RankedTeam
            { Name = row.Name, Owner = row.Owner, Point = row.Points, ValuePoint = row.ValuePoints, SufferedValuePoint = row.SufferedValuePoints,
                Victories = row.Victories, Draws = row.Draws, Defeats = row.Defeats, Goal = row.GoalsFor, SufferedGoal = row.GoalsAgainst, Money = row.Money, PlusMoney = row.PlusMoney }).ToList()) };
            foreach (var winner in calculator.GetTheYearlyWinners(rank, CompetitionMapping.Domain(calendar)))
            {
                var ownerId = Owner(users, group, winner.Owner);
                var owner = archive.WinningTeams.SingleOrDefault(x => x.OwnerUserId == ownerId);
                var annual = snapshot.Stages.SelectMany(x => x.Teams).Where(x => Owner(users, group, x.Owner) == ownerId).DistinctBy(x => x.TeamId).ToArray();
                if (annual.Length != 1) throw new ApplicationFault(409, "ambiguous_hall_winner", "La vittoria non identifica una sola squadra annuale.");
                if (owner == null) archive.WinningTeams.Add(owner = new() { Ordinal = archive.WinningTeams.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1,
                    OwnerUserId = ownerId, OwnerEmail = winner.Owner, Name = annual[0].Name });
                changed |= AddWin(owner.Wins, winner.Round, calendar.SeasonIndex);
                var roster = await database.RosterSnapshots.AsNoTracking().Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
                    .Where(x => x.GroupId == group && x.SeasonIndex == calendar.SeasonIndex && x.TeamId == annual[0].TeamId && x.Kind == RosterKind.Season).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
                foreach (var player in roster?.Players.Where(x => x.Status == 0) ?? [])
                {
                    var won = archive.WinningPlayers.SingleOrDefault(x => x.Player.PlayerId == player.PlayerId);
                    if (won == null) archive.WinningPlayers.Add(won = new() { Ordinal = archive.WinningPlayers.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1,
                        Player = new() { PlayerId = player.PlayerId, ClubId = player.ClubId, Club = player.Club, Name = player.Player.Name, PlayerRole = player.PlayerRole,
                            IsActive = player.IsActive, IsVisible = player.IsVisible, Price = player.Price, Revenue = player.Revenue, Status = player.Status, Position = player.Position } });
                    changed |= AddWin(won.Wins, winner.Round, calendar.SeasonIndex);
                }
            }
        }
        if (!changed) return;
        archive.Version++;
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private Task<HallArchive?> ArchiveAsync(Guid group, Guid league, CancellationToken cancellationToken) => database.HallArchives
        .Include(x => x.PlayerRecords).ThenInclude(x => x.Player).ThenInclude(x => x.Club).Include(x => x.WinningPlayers).ThenInclude(x => x.Player).ThenInclude(x => x.Club)
        .Where(x => x.GroupId == group && x.LeagueId == league).AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
    private async Task ManagerAsync(ClaimsPrincipal caller, Guid group)
    { if (!(await authorization.AuthorizeAsync(caller, group, "workspace-manager")).Succeeded) throw new ApplicationFault(403, "hall_manager_required", "Serve un amministratore del gruppo per aggiornare l'albo."); }
    // Everyone who ever played the league, resolved from what the standings say: current accounts (even if no longer in the group),
    // "user:<id>" references and the historical participants of the group. Not only the present members.
    private async Task<Dictionary<string, Guid>> OwnersAsync(Guid group, IEnumerable<FantasyStandingSnapshot> snapshots, CancellationToken cancellationToken)
    {
        var wanted = snapshots.SelectMany(x => x.Stages).SelectMany(x => x.Teams).Select(x => x.Owner.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var emails = wanted.Select(x => x.ToLowerInvariant()).ToArray();
        var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var user in await database.Users.AsNoTracking().Where(x => x.Email != null && emails.Contains(x.Email)).Select(x => new { x.Email, x.Id }).ToArrayAsync(cancellationToken))
            result.TryAdd(user.Email!.ToLowerInvariant(), user.Id);
        foreach (var label in wanted) if (UserAccount.TryOwnerReference(label, out var id)) result.TryAdd(label.ToLowerInvariant(), id);
        var keys = wanted.Select(label => HistoricalParticipantIdentity.Key(group, label)).ToArray();
        foreach (var entry in await database.LegacyIdentityMaps.AsNoTracking().Where(x => x.Kind == "user" && keys.Contains(x.LegacyKey)).ToArrayAsync(cancellationToken))
            result.TryAdd(entry.LegacyKey, entry.TargetId);
        return result;
    }
    private static Guid Owner(Dictionary<string, Guid> users, Guid group, string value)
    {
        var historical = users.GetValueOrDefault(HistoricalParticipantIdentity.Key(group, value));
        if (historical != Guid.Empty) return historical;
        return users.GetValueOrDefault(value.Trim().ToLowerInvariant()) is var id && id != Guid.Empty ? id
            : throw BaselineRequired($"Il proprietario \"{value}\" delle classifiche non corrisponde a nessun utente né a un partecipante storico del gruppo.");
    }

    // The seasons we have are already in the career totals when every owner's total is at least what those seasons add up to.
    // An owner without a career row is not sealed: the first refresh adds his seasons. Returns the rows to seal.
    private static List<HallRankContribution> Reconcile(HallArchive archive, FantasyStandingSnapshot[] snapshots, Dictionary<string, Guid> users, Guid group)
    {
        if (archive.Ranks.GroupBy(x => x.OwnerUserId).Any(x => x.Count() > 1) || archive.WinningTeams.GroupBy(x => x.OwnerUserId).Any(x => x.Count() > 1) ||
            archive.WinningPlayers.GroupBy(x => x.Player.PlayerId).Any(x => x.Count() > 1))
            throw BaselineRequired("L'albo ha più righe per lo stesso proprietario o giocatore: serve una verifica manuale.");
        var label = snapshots.SelectMany(x => x.Stages).SelectMany(x => x.Teams).GroupBy(x => Owner(users, group, x.Owner)).ToDictionary(x => x.Key, x => x.First().Owner);
        var contributions = snapshots.SelectMany(x => HallContributionMath.FromSnapshot(x, owner => Owner(users, group, owner))).ToList();
        var sealedRows = new List<HallRankContribution>(); var problems = new List<string>();
        foreach (var owner in contributions.GroupBy(x => x.OwnerUserId))
        {
            var rank = archive.Ranks.SingleOrDefault(x => x.OwnerUserId == owner.Key);
            if (rank is null) continue;
            var short_ = new[]
            {
                (rank.Points, owner.Sum(x => x.Points), "punti"), ((float)rank.Victories, owner.Sum(x => x.Victories), "vittorie"), ((float)rank.Draws, owner.Sum(x => x.Draws), "pareggi"),
                ((float)rank.Defeats, owner.Sum(x => x.Defeats), "sconfitte"), ((float)rank.Goals, owner.Sum(x => x.Goals), "gol fatti"), ((float)rank.SufferedGoals, owner.Sum(x => x.SufferedGoals), "gol subiti"),
                (rank.ValuePoints, owner.Sum(x => x.ValuePoints), "punti valore"), (rank.SufferedValuePoints, owner.Sum(x => x.SufferedValuePoints), "punti valore subiti"),
            }.Where(x => x.Item1 + 0.001f < x.Item2).Select(x => $"{x.Item3} {x.Item1:0.##} su {x.Item2:0.##}").ToArray();
            if (short_.Length > 0) problems.Add($"{label[owner.Key]} ({string.Join(", ", short_)})");
            else sealedRows.AddRange(owner);
        }
        if (problems.Count > 0)
            throw BaselineRequired("Il totale storico è inferiore alle stagioni disponibili per " + string.Join("; ", problems.Take(4)) + (problems.Count > 4 ? $" e altri {problems.Count - 4}" : "") +
                ": l'albo non contiene tutte le stagioni importate, serve la conferma di un amministratore.");
        return sealedRows;
    }
    private static bool AddWin(ICollection<HallWinStage> wins, string name, int year)
    {
        var stage = wins.SingleOrDefault(x => x.Stage == name);
        if (stage == null) wins.Add(stage = new() { Stage = name, Ordinal = wins.Count });
        if (stage.Years.Any(x => x.Year == year)) return false;
        stage.Years.Add(new() { Ordinal = stage.Years.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1, Year = year }); return true;
    }
    private static bool Equal(HallRankContribution a, HallRankContribution b) => a.Points == b.Points && a.Victories == b.Victories && a.Draws == b.Draws && a.Defeats == b.Defeats &&
        a.Goals == b.Goals && a.SufferedGoals == b.SufferedGoals && a.ValuePoints == b.ValuePoints && a.SufferedValuePoints == b.SufferedValuePoints && a.Money == b.Money && a.PlusMoney == b.PlusMoney;
    private static void Copy(HallRankContribution a, HallRankContribution b)
    { a.Points = b.Points; a.Victories = b.Victories; a.Draws = b.Draws; a.Defeats = b.Defeats; a.Goals = b.Goals; a.SufferedGoals = b.SufferedGoals;
        a.ValuePoints = b.ValuePoints; a.SufferedValuePoints = b.SufferedValuePoints; a.Money = b.Money; a.PlusMoney = b.PlusMoney; }
    private static void ApplyDelta(HallCareerRank a, HallRankContribution next, HallRankContribution? old)
    {
        checked
        {
            a.Points += next.Points - (old?.Points ?? 0); a.ValuePoints += next.ValuePoints - (old?.ValuePoints ?? 0); a.SufferedValuePoints += next.SufferedValuePoints - (old?.SufferedValuePoints ?? 0);
            a.Victories = (int)((long)a.Victories + next.Victories - (old?.Victories ?? 0)); a.Draws = (int)((long)a.Draws + next.Draws - (old?.Draws ?? 0));
            a.Defeats = (int)((long)a.Defeats + next.Defeats - (old?.Defeats ?? 0)); a.Goals = (int)((long)a.Goals + next.Goals - (old?.Goals ?? 0));
            a.SufferedGoals = (int)((long)a.SufferedGoals + next.SufferedGoals - (old?.SufferedGoals ?? 0));
            a.Money = (int)((long)a.Money + next.Money - (old?.Money ?? 0)); a.PlusMoney = (int)((long)a.PlusMoney + next.PlusMoney - (old?.PlusMoney ?? 0));
        }
        if (!new[] { a.Points, a.ValuePoints, a.SufferedValuePoints }.All(float.IsFinite)) throw new ApplicationFault(409, "invalid_hall_totals", "Totali dell'albo non validi.");
    }
    private static ApplicationFault Changed() => new(409, "hall_changed", "Ricaricare l'albo prima di aggiornarlo.");
    private static ApplicationFault Missing() => new(404, "hall_not_found", "Albo o lega non trovati nel gruppo richiesto.");
    private static ApplicationFault BaselineRequired(string? reason = null) => new(409, "hall_baseline_reconciliation_required",
        reason ?? "I totali storici non sono ricostruibili dalle stagioni disponibili. Confermare esplicitamente il checkpoint senza alterare lo storico prima del refresh incrementale.");
}
