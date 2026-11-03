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
        _ = await RefreshCoreAsync(group, league, request.ExpectedVersion, cancellationToken, caller);
    }
    // Returns what was decided when the starting point was sealed automatically (for the job log), otherwise null.
    public Task<string?> RefreshScheduledAsync(Guid group, Guid league, CancellationToken cancellationToken = default) => RefreshCoreAsync(group, league, null, cancellationToken);

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

    private async Task<string?> RefreshCoreAsync(Guid group, Guid league, int? expectedVersion, CancellationToken cancellationToken, ClaimsPrincipal? caller = null)
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
        EnsureOwners(snapshots, users, group);
        List<HallRankContribution> baselines; string? note = null;
        if (archive.ContributionsInitialized) baselines = await database.HallRankContributions.Where(x => x.GroupId == group && x.LeagueId == league).ToListAsync(cancellationToken);
        else
        {
            // Imported career totals that do not match the seasons we have: if they contain them (older years, leagues born later and seasons not
            // played are only in the totals), those seasons are already counted and the checkpoint is sealed here. Otherwise an administrator decides.
            (baselines, note) = Reconcile(archive, snapshots, users, group);
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
        if (!changed) return note;
        archive.Version++;
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return note;
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
    private static Guid Owner(Dictionary<string, Guid> users, Guid group, string value) => TryOwner(users, group, value, out var id) ? id
        : throw BaselineRequired("Un proprietario delle classifiche non corrisponde a nessun utente né a un partecipante storico del gruppo.");
    private static bool TryOwner(Dictionary<string, Guid> users, Guid group, string value, out Guid id)
    {
        id = users.GetValueOrDefault(HistoricalParticipantIdentity.Key(group, value));
        if (id == Guid.Empty) id = users.GetValueOrDefault(value.Trim().ToLowerInvariant());
        return id != Guid.Empty;
    }

    // Owners named in the standings must all be recognised; the message names the team and the season (e-mail addresses are masked in the logs).
    private static void EnsureOwners(FantasyStandingSnapshot[] snapshots, Dictionary<string, Guid> users, Guid group)
    {
        var missing = snapshots.SelectMany(snapshot => snapshot.Stages.SelectMany(x => x.Teams).Select(team => (snapshot.SeasonIndex, team.Name, team.Owner)))
            .Where(x => !TryOwner(users, group, x.Owner, out _)).DistinctBy(x => (x.SeasonIndex, x.Name)).ToArray();
        if (missing.Length > 0)
            throw BaselineRequired("Proprietario non riconosciuto (nessun utente né partecipante storico del gruppo) per " +
                string.Join(", ", missing.Take(5).Select(x => $"\"{x.Name}\" {Season(x.SeasonIndex)}")) + (missing.Length > 5 ? $" e altre {missing.Length - 5}" : "") + ".");
    }

    // The old Hall of Fame summed every standings document at each run, the season in progress included (as it was then); since the import
    // only that season's standings changed. So, per owner, the career totals are the earlier seasons plus part of the latest one: the earlier
    // seasons are sealed as they are and the latest one with the part already counted, and the refresh adds the rest. When the totals also hold
    // older seasons we have no standings for, the latest season Y whose standings are all within the totals is taken as already counted.
    // An owner without a career row is never sealed (his seasons are added). Rows that the import split for the same person or player are merged.
    private static (List<HallRankContribution> Rows, string Note) Reconcile(HallArchive archive, FantasyStandingSnapshot[] snapshots, Dictionary<string, Guid> users, Guid group)
    {
        var merged = MergeDuplicates(archive);
        var names = snapshots.SelectMany(x => x.Stages).SelectMany(x => x.Teams).GroupBy(x => Owner(users, group, x.Owner)).ToDictionary(x => x.Key, x => x.Last().Name);
        var contributions = snapshots.SelectMany(x => HallContributionMath.FromSnapshot(x, owner => Owner(users, group, owner))).ToList();
        var ranked = contributions.Where(x => archive.Ranks.Any(rank => rank.OwnerUserId == x.OwnerUserId)).ToList();
        var seasons = contributions.Select(x => x.SeasonIndex).Distinct().OrderByDescending(x => x).ToArray();
        var prefix = merged > 0 ? $"unite {merged} righe doppie dello stesso proprietario o giocatore; " : "";
        if (seasons.Length == 0) return ([], prefix + "punto di partenza fissato in automatico: nessuna classifica salvata.");
        static float[] Metrics(HallCareerRank x) => [x.Points, x.Victories, x.Draws, x.Defeats, x.Goals, x.SufferedGoals, x.ValuePoints, x.SufferedValuePoints];
        static float[] Sum(IEnumerable<HallRankContribution> rows) { var list = rows.ToArray(); return [list.Sum(x => x.Points), list.Sum(x => x.Victories), list.Sum(x => x.Draws),
            list.Sum(x => x.Defeats), list.Sum(x => x.Goals), list.Sum(x => x.SufferedGoals), list.Sum(x => x.ValuePoints), list.Sum(x => x.SufferedValuePoints)]; }
        static float Tolerance(float value) => Math.Max(0.05f, Math.Abs(value) * 2e-5f); // float sums of fifteen seasons in a different order
        string[] labels = ["punti", "vittorie", "pareggi", "sconfitte", "gol fatti", "gol subiti", "punti valore", "punti valore subiti"];
        var owners = ranked.Select(x => x.OwnerUserId).Distinct().ToArray();

        // 1. Earlier seasons fully counted, the latest one partly (or fully).
        var latest = seasons[0];
        var partial = owners.All(owner =>
        {
            var total = Metrics(archive.Ranks.Single(x => x.OwnerUserId == owner));
            var before = Sum(ranked.Where(x => x.OwnerUserId == owner && x.SeasonIndex < latest));
            var now = Sum(ranked.Where(x => x.OwnerUserId == owner && x.SeasonIndex == latest));
            return total.Select((value, index) => (rest: value - before[index], now: now[index], tol: Tolerance(value))).All(x => x.rest >= -x.tol && x.rest <= x.now + x.tol);
        });
        if (partial)
        {
            var rows = ranked.Where(x => x.SeasonIndex < latest).ToList(); var full = true;
            foreach (var owner in owners)
            {
                var rank = archive.Ranks.Single(x => x.OwnerUserId == owner);
                var before = ranked.Where(x => x.OwnerUserId == owner && x.SeasonIndex < latest).ToArray();
                var now = ranked.SingleOrDefault(x => x.OwnerUserId == owner && x.SeasonIndex == latest);
                if (now is null) continue;
                float Part(float total, float sum, float current) => Math.Clamp(total - sum, 0, current);
                var counted = new HallRankContribution
                {
                    GroupId = now.GroupId, LeagueId = now.LeagueId, SeasonIndex = latest, OwnerUserId = owner,
                    Points = Part(rank.Points, before.Sum(x => x.Points), now.Points), Victories = (int)Part(rank.Victories, before.Sum(x => x.Victories), now.Victories),
                    Draws = (int)Part(rank.Draws, before.Sum(x => x.Draws), now.Draws), Defeats = (int)Part(rank.Defeats, before.Sum(x => x.Defeats), now.Defeats),
                    Goals = (int)Part(rank.Goals, before.Sum(x => x.Goals), now.Goals), SufferedGoals = (int)Part(rank.SufferedGoals, before.Sum(x => x.SufferedGoals), now.SufferedGoals),
                    ValuePoints = Part(rank.ValuePoints, before.Sum(x => x.ValuePoints), now.ValuePoints),
                    SufferedValuePoints = Part(rank.SufferedValuePoints, before.Sum(x => x.SufferedValuePoints), now.SufferedValuePoints),
                    PlusMoney = (int)Part(rank.PlusMoney, before.Sum(x => x.PlusMoney), now.PlusMoney), Money = (int)Part(rank.Money, before.Sum(x => x.Money), now.Money),
                };
                full &= Math.Abs(counted.Points - now.Points) <= Tolerance(now.Points) && counted.Victories == now.Victories && counted.Goals == now.Goals;
                rows.Add(counted);
            }
            return (rows, prefix + (full
                ? $"punto di partenza fissato in automatico: tutte le stagioni fino al {Season(latest)} sono già nei totali."
                : $"punto di partenza fissato in automatico: stagioni fino al {Season(latest - 1)} già nei totali, la {Season(latest)} in parte (fino all'import); il resto viene aggiunto ora."));
        }

        // 2. Older seasons only in the totals: the latest season whose standings all fit in them.
        string[] Gaps(int until) => owners.SelectMany(owner =>
        {
            var total = Metrics(archive.Ranks.Single(x => x.OwnerUserId == owner));
            var sum = Sum(ranked.Where(x => x.OwnerUserId == owner && x.SeasonIndex <= until));
            return total.Select((value, index) => (value, sum: sum[index], label: labels[index])).Where(x => x.value + Tolerance(x.value) < x.sum)
                .Select(x => $"{names.GetValueOrDefault(owner, "?")}: {x.label} {x.value:0.##} nei totali, {x.sum:0.##} nelle stagioni fino al {Season(until)}");
        }).ToArray();
        var cutoff = seasons.Cast<int?>().FirstOrDefault(y => Gaps(y!.Value).Length == 0);
        if (cutoff is null)
        {
            var gaps = Gaps(seasons.Min());
            throw BaselineRequired("I totali storici sono inferiori anche alla sola stagione più vecchia disponibile (" + string.Join("; ", gaps.Take(3)) +
                "): l'albo non contiene le stagioni importate, serve la conferma di un amministratore.");
        }
        var later = seasons.Where(x => x > cutoff).Order().Select(Season).ToArray();
        return (ranked.Where(x => x.SeasonIndex <= cutoff).ToList(), prefix + $"punto di partenza fissato in automatico: i totali contengono anche anni senza classifica; stagioni fino al {Season(cutoff.Value)} considerate già incluse" +
            (later.Length > 0 ? $", aggiunte {string.Join(", ", later)}" : "") + ".");
    }

    // The import kept one row per legacy label: the same person (or player) can appear twice. Same totals and wins, one row.
    private static int MergeDuplicates(HallArchive archive)
    {
        var merged = 0;
        foreach (var rows in archive.Ranks.GroupBy(x => x.OwnerUserId).Where(x => x.Count() > 1).Select(x => x.OrderBy(row => row.Ordinal).ToArray()).ToArray())
            foreach (var extra in rows.Skip(1))
            {
                var keep = rows[0];
                keep.Points += extra.Points; keep.Victories += extra.Victories; keep.Draws += extra.Draws; keep.Defeats += extra.Defeats; keep.Goals += extra.Goals;
                keep.SufferedGoals += extra.SufferedGoals; keep.ValuePoints += extra.ValuePoints; keep.SufferedValuePoints += extra.SufferedValuePoints;
                keep.PlusMoney += extra.PlusMoney; keep.Money += extra.Money; archive.Ranks.Remove(extra); merged++;
            }
        foreach (var rows in archive.WinningTeams.GroupBy(x => x.OwnerUserId).Where(x => x.Count() > 1).Select(x => x.OrderBy(row => row.Ordinal).ToArray()).ToArray())
            foreach (var extra in rows.Skip(1)) { MergeWins(rows[0].Wins, extra.Wins); archive.WinningTeams.Remove(extra); merged++; }
        foreach (var rows in archive.WinningPlayers.GroupBy(x => x.Player.PlayerId).Where(x => x.Count() > 1).Select(x => x.OrderBy(row => row.Ordinal).ToArray()).ToArray())
            foreach (var extra in rows.Skip(1)) { MergeWins(rows[0].Wins, extra.Wins); archive.WinningPlayers.Remove(extra); merged++; }
        return merged;
    }
    private static void MergeWins(ICollection<HallWinStage> target, IEnumerable<HallWinStage> source)
    {
        foreach (var stage in source) foreach (var year in stage.Years) AddWin(target, stage.Stage, year.Year);
    }

    private static string Season(int year) => $"{year + 2011}/{(year + 2012) % 100:00}";

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
