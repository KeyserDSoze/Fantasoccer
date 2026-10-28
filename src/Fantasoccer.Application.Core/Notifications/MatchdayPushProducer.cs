using System.Globalization;
using System.Text.Json;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Notifications;

public sealed class MatchdayPushProducer(FantasoccerDbContext database, PushOutboxApplicationService outbox, ITeamCalculator scoring,
    Fantasoccer.Application.Core.Football.IFootballLiveDataStore liveData, TimeProvider clock)
{
    public async Task RunAsync(int year, CancellationToken cancellationToken = default)
    {
        if (database.ChangeTracker.HasChanges()) throw new InvalidOperationException("Matchday push production requires a dedicated clean scope.");
        var now = clock.GetUtcNow();
        var schedules = (await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && !x.Delayed && !x.WasPostponed && x.KickoffUtc != null).ToArrayAsync(cancellationToken))
            .GroupBy(x => x.SerieADay).Select(x => new { Day = x.Key, Start = x.Min(game => game.KickoffUtc!.Value), End = x.Max(game => game.KickoffUtc!.Value) }).ToArray();
        var live = schedules.Where(x => now >= x.Start && now <= x.End.AddHours(10.25)).MaxBy(x => x.Day);
        var next = schedules.Where(x => now < x.Start).MinBy(x => x.Day);
        var last = schedules.Where(x => now >= x.End.AddMinutes(135) && now < x.End.AddHours(48)).MaxBy(x => x.Day);
        if (live == null && last == null && (next == null || next.Start - now > TimeSpan.FromHours(8))) return;
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var links = await database.LeagueBasketSeasons.AsNoTracking().Where(x => x.SeasonIndex == year).ToArrayAsync(cancellationToken);
        var leagues = await database.LeagueSeasons.AsNoTracking().Include(x => x.League).Where(x => x.SeasonIndex == year && !x.IsArchived && !x.League.IsArchived).ToArrayAsync(cancellationToken);
        var teams = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.SeasonIndex == year && !x.IsArchived).AsSplitQuery().ToArrayAsync(cancellationToken);
        var preferences = await database.UserPushPreferences.AsNoTracking().AsSplitQuery().ToDictionaryAsync(x => x.UserId, cancellationToken);
        var rules = await database.LeagueRules.AsNoTracking().Where(x => x.SeasonIndex == year).AsSplitQuery().ToArrayAsync(cancellationToken);
        var matches = await database.FantasyMatches.AsNoTracking().Include(x => x.Matchday).Where(x => x.SeasonIndex == year && x.Result!.IsCancelled != true).ToArrayAsync(cancellationToken);
        var daily = await database.RosterSnapshots.AsNoTracking().Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .Where(x => x.SeasonIndex == year && x.Kind == RosterKind.Matchday && (x.Day == (live == null ? -1 : live.Day) || x.Day == (last == null ? -1 : last.Day))).AsSplitQuery().ToArrayAsync(cancellationToken);

        async Task Queue(FantasyTeamSeason team, LeagueSeason league, int day, int kind, string key, string body, DateTimeOffset expiry)
        {
            foreach (var owner in team.Owners.Where(x => x.Membership.Roles != GroupRole.None && x.Membership.User.IsEnabled))
            {
                if (!PushDeliveryAccess.IsEnabled(preferences.GetValueOrDefault(owner.UserId), kind)) continue;
                await outbox.EnqueueAsync(new(key, team.GroupId, league.LeagueId, year, day, owner.UserId, kind, "FantaZone", body,
                    kind == 2 ? "/edit" : kind == 3 ? "/calendar" : "/live", expiry, team.Id, team.BasketId), cancellationToken);
            }
        }
        bool Participates(FantasyTeamSeason team, LeagueSeason league) => links.Any(link => link.GroupId == team.GroupId && link.LeagueId == league.LeagueId && link.BasketId == team.BasketId);
        async Task<PushDayCheckpoint> Checkpoint(int day)
        {
            var checkpoint = database.PushDayCheckpoints.Local.SingleOrDefault(x => x.SeasonIndex == year && x.Day == day)
                ?? await database.PushDayCheckpoints.SingleOrDefaultAsync(x => x.SeasonIndex == year && x.Day == day, cancellationToken);
            if (checkpoint is null) database.PushDayCheckpoints.Add(checkpoint = new() { SeasonIndex = year, Day = day });
            return checkpoint;
        }

        if (next != null && next.Start - now <= TimeSpan.FromHours(8))
        {
            var checkpoint = await Checkpoint(next.Day);
            if (!checkpoint.DeploymentReminder)
            {
                foreach (var league in leagues)
                    foreach (var team in teams.Where(team => Participates(team, league)))
                        await Queue(team, league, next.Day, 2, $"deployment:{team.Id}", $"[{league.League.Name}] Ricorda di schierare la formazione!", next.Start);
                checkpoint.DeploymentReminder = true;
            }
        }
        if (live != null)
        {
            var checkpoint = await Checkpoint(live.Day);
            // Provisional votes come from the shared cache (one global snapshot).
            // Baselines/checkpoints stay in PostgreSQL to deduplicate notifications.
            var snapshot = await liveData.LiveVotesAsync(year, live.Day, cancellationToken);
            var knownPlayers = (await database.PlayerSeasons.AsNoTracking().Where(x => x.SeasonIndex == year).Select(x => x.PlayerId).ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            var knownClubs = (await database.Clubs.AsNoTracking().Select(x => x.Id).ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            // Players unknown to the season catalogue cannot be in any roster.
            var votes = (snapshot?.Payload.Players ?? []).Where(x => knownPlayers.Contains(x.PlayerId)).Select(x => LiveRow(x, year, live.Day, knownClubs)).ToArray();
            var previous = checkpoint.HasMatchEventVotes ? checkpoint.Votes.ToDictionary(x => x.PlayerId) :
                (await database.FootballVotes.AsNoTracking().Where(x => x.SeasonIndex == year && x.Day == live.Day && x.Source == FootballVoteSource.Notification).ToArrayAsync(cancellationToken))
                    .ToDictionary(x => x.PlayerId, x => Baseline(x, 0));
            var changes = votes.Select(vote => (Vote: vote, Text: Describe(previous.GetValueOrDefault(vote.PlayerId), vote))).Where(x => x.Text.Length > 0).ToArray();
            if (changes.Length > 0)
            {
                checkpoint.MatchEventSequence++;
                foreach (var (vote, text) in changes)
                    foreach (var team in teams.Where(team => daily.Any(roster => roster.TeamId == team.Id && roster.Day == live.Day && roster.Players.Any(player => player.PlayerId == vote.PlayerId && player.Status == 0))))
                    {
                        var teamLeagues = leagues.Where(league => Participates(team, league)).OrderByDescending(x => x.League.IsMain).ThenBy(x => x.LeagueId).ToArray();
                        if (teamLeagues.FirstOrDefault() is { } ownLeague)
                            await Queue(team, ownLeague, live.Day, 0, $"own:{checkpoint.MatchEventSequence}:{vote.PlayerId}:{team.Id}", $"[{ownLeague.League.Name}] {vote.Player.Name}: {text}", now.AddHours(48));
                        foreach (var game in matches.Where(x => x.Matchday.SerieADay == live.Day && (x.HomeTeamId == team.Id || x.AwayTeamId == team.Id)))
                        {
                            var opponent = teams.SingleOrDefault(x => x.Id == (game.HomeTeamId == team.Id ? game.AwayTeamId : game.HomeTeamId));
                            var league = teamLeagues.SingleOrDefault(x => x.LeagueId == game.LeagueId);
                            if (opponent != null && league != null)
                                await Queue(opponent, league, live.Day, 1, $"opponent:{checkpoint.MatchEventSequence}:{game.Id}:{vote.PlayerId}:{team.Id}",
                                    $"[{league.League.Name}] {vote.Player.Name}: {text} ({team.Name})", now.AddHours(48));
                        }
                    }
            }
            if (votes.Length > 0 || snapshot is not null)
            {
                checkpoint.Votes.Clear();
                await database.SaveChangesAsync(cancellationToken);
                foreach (var (vote, ordinal) in votes.OrderBy(x => x.PlayerId).Select((vote, index) => (vote, index))) checkpoint.Votes.Add(Baseline(vote, ordinal));
                checkpoint.HasMatchEventVotes = true;
            }
            var deadline = live.End.AddHours(4);
            if (now < deadline && deadline - now <= TimeSpan.FromHours(2) && !checkpoint.LiveChangesReminder)
            {
                foreach (var league in leagues)
                {
                    var settings = rules.Single(x => x.GroupId == league.GroupId && x.LeagueId == league.LeagueId);
                    foreach (var team in teams.Where(team => Participates(team, league)))
                    {
                        var used = daily.SingleOrDefault(x => x.TeamId == team.Id && x.Day == live.Day)?.FormationChanges ?? 0;
                        var remaining = settings.LiveFormationChanges - used;
                        if (remaining <= 0) continue;
                        await Queue(team, league, live.Day, 5, $"live-reminder:{team.Id}", $"[{league.League.Name}] Ti restano {remaining} cambi formazione prima della scadenza live.", deadline);
                    }
                }
                checkpoint.LiveChangesReminder = true;
            }
            await FormationChangesAsync(year, live.Day, daily.Where(x => x.Day == live.Day).ToArray(), checkpoint, teams, leagues, rules, matches, Queue, cancellationToken);
        }
        if (last != null)
        {
            var checkpoint = await Checkpoint(last.Day);
            var endedGames = matches.Where(x => x.Matchday.SerieADay == last.Day && leagues.Any(league => league.GroupId == x.GroupId && league.LeagueId == x.LeagueId)).ToArray();
            if (!checkpoint.EndDay && endedGames.Length > 0 && endedGames.All(x => x.Result is not null))
            {
                foreach (var game in endedGames)
                    foreach (var home in new[] { true, false })
                    {
                        var team = teams.SingleOrDefault(x => x.Id == (home ? game.HomeTeamId : game.AwayTeamId));
                        var league = leagues.Single(x => x.GroupId == game.GroupId && x.LeagueId == game.LeagueId);
                        if (team is null) continue;
                        var result = game.Result!;
                        var mine = home ? result.HomeGoals : result.AwayGoals; var theirs = home ? result.AwayGoals : result.HomeGoals;
                        await Queue(team, league, last.Day, 3, $"end:{game.Id}:{team.Id}", $"[{league.League.Name}] Giornata {last.Day}: {mine}-{theirs} contro {(home ? game.AwayName : game.HomeName)}.", now.AddHours(48));
                    }
                checkpoint.EndDay = true;
            }
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    private async Task FormationChangesAsync(int year, int day, RosterSnapshot[] rosters, PushDayCheckpoint checkpoint, FantasyTeamSeason[] teams, LeagueSeason[] leagues,
        LeagueRules[] rules, FantasyMatch[] matches, Func<FantasyTeamSeason, LeagueSeason, int, int, string, string, DateTimeOffset, Task> queue, CancellationToken cancellationToken)
    {
        foreach (var roster in rosters.Where(x => x.FormationHistory.Count > 0))
        {
            var team = teams.SingleOrDefault(x => x.Id == roster.TeamId);
            if (team is null) continue;
            var states = roster.Players.Where(x => x.Status == 0).ToDictionary(x => x.PlayerId, CompetitionMapping.Domain);
            // History identifies display names in the source; refuse ambiguity
            // rather than apply a historical move to an arbitrary active player.
            void Position(string name, int position)
            {
                var candidates = states.Values.Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (candidates.Length == 1) candidates[0].Position = (FantaSoccerRole)position;
            }
            var history = roster.FormationHistory.OrderBy(x => x.Ordinal).ToArray();
            foreach (var change in history.Reverse()) foreach (var player in change.Players) Position(player.PlayerName, player.FromPosition);
            var groups = await database.LegacyIdentityMaps.AsNoTracking().Where(x => x.Kind == "group" && x.TargetId == roster.GroupId).Select(x => x.LegacyKey).ToArrayAsync(cancellationToken);
            var baskets = await database.LegacyIdentityMaps.AsNoTracking().Where(x => x.Kind == "basket" && x.TargetId == roster.BasketId).Select(x => x.LegacyKey).ToArrayAsync(cancellationToken);
            var legacyBasket = baskets.SingleOrDefault() is { } basketKey ? JsonSerializer.Deserialize<string[]>(basketKey)?.LastOrDefault() : null;
            var ownerEmail = await database.Users.Where(x => x.Id == roster.OwnerUserId).Select(x => x.Email).SingleAsync(cancellationToken);
            foreach (var change in history)
            {
                var seen = await database.PushFormationChangeReceipts.AnyAsync(x => x.GroupId == roster.GroupId && x.BasketId == roster.BasketId && x.TeamId == roster.TeamId && x.SeasonIndex == year && x.Day == day && x.ChangeOrdinal == change.Ordinal, cancellationToken);
                seen |= groups.Length == 1 && legacyBasket != null && checkpoint.FormationReceipts.Any(x => x.Receipt == $"{groups[0]}:{legacyBasket}:{ownerEmail}:{change.Ordinal}");
                var before = states.Values.Select(Clone).ToArray();
                foreach (var player in change.Players) Position(player.PlayerName, player.ToPosition);
                if (seen) continue;
                foreach (var league in leagues.Where(league => matches.Any(game => game.LeagueId == league.LeagueId && game.Matchday.SerieADay == day && (game.HomeTeamId == team.Id || game.AwayTeamId == team.Id))))
                {
                    var row = rules.Single(x => x.GroupId == league.GroupId && x.LeagueId == league.LeagueId);
                    var type = (LeagueType)(league.CompetitionType != 0 ? league.CompetitionType : league.League.CompetitionType != 0 ? league.League.CompetitionType : 1);
                    if (row.Formation == (int)FormationType.Best || type == LeagueType.SuperLeague) continue;
                    await scoring.BuildRequestAsync(year, day, type, LeagueRulesMapping.ToDomain(row), false, true, false);
                    var oldPoints = scoring.CalculatePoint(before.Select(Clone)).Value; var newPoints = scoring.CalculatePoint(states.Values.Select(Clone)).Value;
                    var names = string.Join(", ", change.Players.OrderBy(x => x.Ordinal).Select(x => $"{x.PlayerName} ({(FantaSoccerRole)x.FromPosition} → {(FantaSoccerRole)x.ToPosition})"));
                    var text = $"[{league.League.Name}] {team.Name}: cambio live di {change.Author} — {names}. {oldPoints.ToString("0.#", CultureInfo.InvariantCulture)} → {newPoints.ToString("0.#", CultureInfo.InvariantCulture)} pt.";
                    await queue(team, league, day, 6, $"formation:{team.Id}:{change.Ordinal}", text.Length > 4000 ? text[..4000] : text, clock.GetUtcNow().AddHours(48));
                }
                database.PushFormationChangeReceipts.Add(new() { GroupId = roster.GroupId, BasketId = roster.BasketId, TeamId = roster.TeamId, SeasonIndex = year, Day = day, ChangeOrdinal = change.Ordinal, RecordedAtUtc = clock.GetUtcNow() });
            }
        }
    }
    private static FootballVote LiveRow(Fantasoccer.Application.Core.Football.CachedLiveVote row, int year, int day, HashSet<string> clubs) => new()
    {
        PlayerId = row.PlayerId, SeasonIndex = year, Day = day, Source = FootballVoteSource.Live, ClubId = row.ClubId is { } club && clubs.Contains(club) ? club : null,
        PlayerRole = row.PlayerRole, IsActive = row.IsActive, IsVisible = row.IsVisible, VoteRole = row.Vote.Role, Value = row.Vote.Value, IsFinal = row.Vote.IsFinal,
        Goals = row.Vote.Goals, Penalties = row.Vote.Penalties, Assists = row.Vote.Assists, StoppedPenalties = row.Vote.StoppedPenalties, SufferedGoals = row.Vote.SufferedGoals,
        WrongedPenalties = row.Vote.WrongedPenalties, OwnGoals = row.Vote.OwnGoals, Status = row.Vote.Status, ManOfTheMatch = row.Vote.ManOfTheMatch, HasVote = row.Vote.HasVote,
        IsOut = row.Vote.IsOut, IsIn = row.Vote.IsIn, Injured = row.Vote.Injured, Player = new FootballPlayer { Id = row.PlayerId, Name = row.Name }
    };
    private static Player Clone(Player player) => new() { Name = player.Name, RealTeam = player.RealTeam, Role = player.Role, IsActive = player.IsActive,
        Visible = player.Visible, Position = player.Position, Status = player.Status, Price = player.Price, Revenue = player.Revenue };
    private static string Describe(PushVoteBaseline? previous, FootballVote current)
    {
        var changes = new List<string>();
        void Change(int before, int after, string label) { if (after > before) changes.Add(label); else if (after < before) changes.Add(label + " annullato"); }
        Change(previous?.Goals ?? 0, current.Goals, "Goal"); Change(previous?.Assists ?? 0, current.Assists, "Assist");
        Change(previous?.Penalties ?? 0, current.Penalties, "Rigore"); Change(previous?.OwnGoals ?? 0, current.OwnGoals, "Autogol");
        Change(previous?.WrongedPenalties ?? 0, current.WrongedPenalties, "Rigore sbagliato"); Change(previous?.SufferedGoals ?? 0, current.SufferedGoals, "Goal subito");
        Change(previous?.StoppedPenalties ?? 0, current.StoppedPenalties, "Rigore parato");
        Change(previous?.ManOfTheMatch == true ? 1 : 0, current.ManOfTheMatch ? 1 : 0, "Migliore in campo");
        Change(previous?.Status == 1 ? 1 : 0, current.Status == 1 ? 1 : 0, "Giallo"); Change(previous?.Status == 2 ? 1 : 0, current.Status == 2 ? 1 : 0, "Rosso");
        return string.Join(", ", changes);
    }
    private static PushVoteBaseline Baseline(FootballVote vote, int ordinal) => new() { Ordinal = ordinal, PlayerId = vote.PlayerId, ClubId = vote.ClubId, PlayerRole = vote.PlayerRole,
        IsActive = vote.IsActive, IsVisible = vote.IsVisible, VoteRole = vote.VoteRole, Value = vote.Value, IsFinal = vote.IsFinal, Goals = vote.Goals, Penalties = vote.Penalties,
        Assists = vote.Assists, StoppedPenalties = vote.StoppedPenalties, SufferedGoals = vote.SufferedGoals, WrongedPenalties = vote.WrongedPenalties, OwnGoals = vote.OwnGoals,
        Status = vote.Status, ManOfTheMatch = vote.ManOfTheMatch, HasVote = vote.HasVote, IsOut = vote.IsOut, IsIn = vote.IsIn, Injured = vote.Injured };
}
