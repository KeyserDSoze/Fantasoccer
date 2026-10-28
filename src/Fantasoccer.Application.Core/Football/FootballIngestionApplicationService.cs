using System.Security.Claims;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Application.Core.Football;

public sealed record RefreshFootballRequest(string Operation, int Year, int? Day = null);
public sealed partial class FootballIngestionApplicationService(FantasoccerDbContext database, ISerieAInformation schedule, IAllPlayers catalogue,
    ILiveVote live, IOfficialVote official, IFactory<IFormationParser> formations, AccountApplicationService accounts,
    TeamRosterAvailabilityService availability, ITeamCalculator scoring, IFootballLiveDataStore liveData, TimeProvider clock)
{
    public async Task RefreshAsync(ClaimsPrincipal caller, RefreshFootballRequest request, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(caller, cancellationToken);
        await RunCoreAsync(request, cancellationToken, caller);
    }
    public Task RunAsync(RefreshFootballRequest request, CancellationToken cancellationToken = default) => RunCoreAsync(request, cancellationToken, null);
    private async Task RunCoreAsync(RefreshFootballRequest request, CancellationToken cancellationToken, ClaimsPrincipal? caller)
    {
        var currentYear = FantacalcioTime.SeasonYearAt(clock.GetUtcNow().UtcDateTime);
        if (request.Year is < 1 or > 200 || request.Year > currentYear ||
            (request.Year != currentYear && (caller == null || request.Operation is not ("official" or "statistics"))) || request.Day is < 1 or > 38)
            throw new ApplicationFault(400, "invalid_ingestion_context", "L'ingestione aggiorna solo la stagione corrente e giornate valide.");
        // Provider network calls happen before the SQL lock/transaction. Failure,
        // cancellation or an empty parse never replaces saved SQL data with empty.
        switch (request.Operation)
        {
            case "calendar": await CalendarAsync(request.Year, cancellationToken, caller); break;
            case "catalogue": await CatalogueAsync(request.Year, cancellationToken, caller); break;
            case "live":
                if (request.Day is null) throw Invalid("Specificare la giornata dei voti.");
                await LiveVotesAsync(request.Year, request.Day.Value, cancellationToken, caller); break;
            case "official":
                if (request.Day is null) throw Invalid("Specificare la giornata dei voti.");
                await OfficialVotesAsync(request.Year, request.Day.Value, cancellationToken, caller); break;
            case "chances":
                if (request.Day is null) throw Invalid("Specificare la giornata delle probabilità.");
                await ChancesAsync(request.Year, request.Day.Value, cancellationToken, caller); break;
            case "statistics":
                var until = request.Day ?? await database.FootballDatasets.Where(x => x.SeasonIndex == request.Year && x.Kind == "official").Select(x => (int?)x.Day).MaxAsync(cancellationToken)
                    ?? throw Invalid("Nessun voto ufficiale disponibile per le statistiche.");
                await StatisticsAsync(request.Year, until, cancellationToken, caller); break;
            default: throw Invalid("Operazione di ingestione non valida.");
        }
    }
    private async Task CalendarAsync(int year, CancellationToken cancellationToken, ClaimsPrincipal? caller)
    {
        var first = await database.FootballMatches.Where(x => x.SeasonIndex == year && !x.Delayed && !x.WasPostponed && x.KickoffUtc != null && x.KickoffUtc >= clock.GetUtcNow().AddHours(-12))
            .Select(x => (int?)x.SerieADay).MinAsync(cancellationToken);
        var exists = await database.FootballMatchdays.AnyAsync(x => x.SeasonIndex == year, cancellationToken);
        var parsed = await schedule.GetCalendarAsync(exists ? first ?? 38 : 1, exists ? Math.Min(38, (first ?? 38) + 3) : 38).WaitAsync(cancellationToken);
        await StoreCalendarAsync(parsed, year, cancellationToken, caller);
    }
    private async Task StoreCalendarAsync(RealCalendar parsed, int year, CancellationToken cancellationToken, ClaimsPrincipal? caller)
    {
        if (parsed.Year != year || parsed.Days is not { Count: > 0 }) throw Invalid("Il provider non ha restituito un calendario valido.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AuthorizeAsync(caller, cancellationToken);
        await SeasonAsync(year, cancellationToken);
        var clubs = await database.Clubs.ToArrayAsync(cancellationToken);
        foreach (var day in parsed.Days)
        {
            if (day.SerieADay is < 1 or > 38 || day.Games is not { Count: > 0 }) throw Invalid("Giornata del provider incompleta.");
            var row = await database.FootballMatchdays.Include(x => x.Matches).SingleOrDefaultAsync(x => x.SeasonIndex == year && x.SerieADay == day.SerieADay, cancellationToken);
            if (row is null) database.FootballMatchdays.Add(row = new() { SeasonIndex = year, SerieADay = day.SerieADay });
            foreach (var game in day.Games)
            {
                var home = Club(game.Home, clubs); var away = Club(game.Away, clubs);
                if (home.Id == away.Id) throw Invalid("Squadre reali non distinte.");
                await MembershipAsync(home.Id, year, ClubMembershipSource.Calendar, cancellationToken);
                await MembershipAsync(away.Id, year, ClubMembershipSource.Calendar, cancellationToken);
                var match = row.Matches.SingleOrDefault(x => x.HomeClubId == home.Id && x.AwayClubId == away.Id);
                if (match is null) row.Matches.Add(match = new() { SeasonIndex = year, SerieADay = day.SerieADay,
                    Ordinal = row.Matches.Select(x => x.Ordinal).DefaultIfEmpty(-1).Max() + 1, HomeClub = home, AwayClub = away });
                var newlyFinished = match.IsFinished != true && game.IsFinished == true;
                if (game.Delayed || newlyFinished) match.ActualOfficialVotesReady = false;
                if (!match.WasPostponed)
                    match.OriginalKickoffUtc = game.Delayed ? match.KickoffUtc ?? Utc(game.OriginalDate ?? game.Date) : Utc(game.OriginalDate ?? game.Date);
                match.WasPostponed |= match.Delayed || game.Delayed || game.WasPostponed;
                match.KickoffUtc = Utc(game.Date); match.Delayed = game.Delayed;
                if (game.IsFinished is not null) match.IsFinished = game.IsFinished;
                match.HomeGoals = game.HomeGoal; match.AwayGoals = game.AwayGoal;
            }
        }
        await DatasetAsync("realcalendar", year, 0, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await RealRankAsync(year, cancellationToken);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private async Task CatalogueAsync(int year, CancellationToken cancellationToken, ClaimsPrincipal? caller)
    {
        var parsed = await catalogue.GetAsync().WaitAsync(cancellationToken);
        if (parsed is not { Count: > 0 } || parsed.GroupBy(x => x.Key).Any(x => x.Count() != 1)) throw Invalid("Catalogo del provider vuoto o ambiguo.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AuthorizeAsync(caller, cancellationToken);
        await SeasonAsync(year, cancellationToken);
        var existing = await database.PlayerSeasons.Include(x => x.Player).Where(x => x.SeasonIndex == year).ToArrayAsync(cancellationToken);
        if (existing.Length > 0 && parsed.Count < existing.Count(x => x.IsActive) * 0.8) throw Invalid("Il catalogo del provider è troppo ridotto; nessun giocatore è stato disattivato.");
        var players = await database.Players.ToArrayAsync(cancellationToken); var clubs = await database.Clubs.ToArrayAsync(cancellationToken);
        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in parsed)
        {
            ValidatePlayer(sample);
            var row = existing.SingleOrDefault(x => x.PlayerId == sample.Key);
            var person = row?.Player ?? Person(sample, players);
            var club = Club(sample.RealTeam, clubs);
            active.Add(person.Id);
            if (row is null && existing.Any(x => x.PlayerId == person.Id)) throw Invalid("La PlayerKey stagionale è cambiata: serve una riconciliazione esplicita.");
            if (row is null) database.PlayerSeasons.Add(row = new() { Player = person, SeasonIndex = year });
            row.Club = club; row.Position = (PlayerPosition)sample.Role; row.IsActive = true; row.IsVisible = sample.Visible;
        }
        foreach (var row in existing.Where(x => !active.Contains(x.PlayerId))) row.IsActive = false;
        await DatasetAsync("realplayerswrapper", year, 0, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        var seasons = await database.PlayerSeasons.Where(x => x.SeasonIndex == year).ToDictionaryAsync(x => x.PlayerId, cancellationToken);
        var rosters = await database.RosterSnapshots.Where(x => x.SeasonIndex == year && x.Kind == RosterKind.Season).AsSplitQuery().ToArrayAsync(cancellationToken);
        foreach (var roster in rosters)
        {
            var changed = false;
            foreach (var player in roster.Players.Where(x => x.Status == 0))
                if (seasons.TryGetValue(player.PlayerId, out var source) && (player.ClubId != source.ClubId || player.IsActive != source.IsActive))
                { player.ClubId = source.ClubId!; player.IsActive = source.IsActive; changed = true; }
            if (changed) { roster.Version++; roster.LastUpdateUtc = clock.GetUtcNow(); }
        }
        foreach (var group in rosters.GroupBy(x => x.GroupId)) await availability.SynchronizeFutureAsync(group.Key, year, group.Select(x => x.TeamId).ToArray(), cancellationToken);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    // Official votes remain authoritative PostgreSQL data used by final scoring and statistics.
    private async Task OfficialVotesAsync(int year, int day, CancellationToken cancellationToken, ClaimsPrincipal? caller, HashSet<string>? clubFilter = null)
    {
        var samples = await official.GetAsync(year, day).WaitAsync(cancellationToken);
        if (clubFilter is not null) samples = samples.Where(x => ProviderClubCode(x.RealTeam) is { } code && clubFilter.Contains(code)).ToList();
        if (samples is not { Count: > 0 } || samples.Any(x => x.Vote == null || !float.IsFinite(x.Vote.Value)) || samples.GroupBy(x => x.Key).Any(x => x.Count() != 1))
            throw Invalid("Voti del provider mancanti o ambigui; lo snapshot salvato non viene cancellato.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AuthorizeAsync(caller, cancellationToken);
        await SeasonAsync(year, cancellationToken);
        var players = await database.Players.ToArrayAsync(cancellationToken); var clubs = await database.Clubs.ToArrayAsync(cancellationToken);
        const FootballVoteSource source = FootballVoteSource.Official;
        var identities = await database.PlayerSeasons.Include(x => x.Player).Where(x => x.SeasonIndex == year).ToArrayAsync(cancellationToken);
        var rows = await database.FootballVotes.Where(x => x.SeasonIndex == year && x.Day == day && x.Source == source).ToArrayAsync(cancellationToken);
        var received = new HashSet<string>(StringComparer.Ordinal);
        var finalClubs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            ValidatePlayer(sample);
            var person = identities.SingleOrDefault(x => x.PlayerId == sample.Key)?.Player ?? Person(sample, players);
            var club = Club(sample.RealTeam, clubs); received.Add(club.Id);
            if (sample.Vote.IsFinal && sample.Vote.HasVote) finalClubs.Add(club.Id);
            await PlayerSeasonAsync(person, club, sample, year, cancellationToken);
            var row = rows.SingleOrDefault(x => x.PlayerId == person.Id);
            if (row is null) database.FootballVotes.Add(row = new() { Player = person, SeasonIndex = year, Day = day, Source = source });
            row.Club = club; row.PlayerRole = (int)sample.Role; row.IsActive = sample.IsActive; row.IsVisible = sample.Visible;
            var vote = sample.Vote; row.VoteRole = (int)vote.Role; row.Value = vote.Value; row.IsFinal = vote.IsFinal; row.Goals = vote.Goal; row.Penalties = vote.Penalty;
            row.Assists = vote.Assist; row.StoppedPenalties = vote.StoppedPenalty; row.SufferedGoals = vote.SufferedGoal; row.WrongedPenalties = vote.WrongedPenalty;
            row.OwnGoals = vote.OwnGoal; row.Status = (int)vote.Status; row.ManOfTheMatch = vote.ManOfTheMatch; row.HasVote = vote.HasVote; row.IsOut = vote.IsOut; row.IsIn = vote.IsIn; row.Injured = vote.Injured;
        }
        {
            var matches = await database.FootballMatches.Where(x => x.SeasonIndex == year && x.SerieADay == day).ToArrayAsync(cancellationToken);
            // A recovery refresh updates only the recovered clubs. It never
            // rewrites other historical official votes of the same day.
            var scoped = clubFilter is null ? matches : matches.Where(x => clubFilter.Contains(x.HomeClubId) && clubFilter.Contains(x.AwayClubId)).ToArray();
            var expected = (clubFilter is null ? matches.Where(x => !x.WasPostponed && !x.Delayed) : scoped).SelectMany(x => new[] { x.HomeClubId, x.AwayClubId }).ToArray();
            if (matches.Length == 0 || !expected.All(received.Contains)) throw new ApplicationFault(409, "official_matchday_incomplete", "Mancano voti ufficiali per una squadra reale; nessuno snapshot è stato sostituito.");
            foreach (var match in scoped)
            {
                var finished = !match.Delayed && (match.IsFinished == true || match.IsFinished is null && match.KickoffUtc is { } kickoff && clock.GetUtcNow() >= kickoff.AddMinutes(135));
                if (finished && finalClubs.Contains(match.HomeClubId) && finalClubs.Contains(match.AwayClubId)) match.ActualOfficialVotesReady = true;
                else match.ActualOfficialVotesReady = false;
            }
        }
        await DatasetAsync("official", year, day, cancellationToken);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    // Provisional live votes are a global operational snapshot in the distributed
    // cache, never PostgreSQL rows. Validation is unchanged: an empty, ambiguous or
    // non-finite provider response never replaces the snapshot already published.
    private async Task LiveVotesAsync(int year, int day, CancellationToken cancellationToken, ClaimsPrincipal? caller, HashSet<string>? clubFilter = null)
    {
        var fetchedAt = clock.GetUtcNow();
        var samples = await live.GetAsync(year, day).WaitAsync(cancellationToken);
        if (clubFilter is not null) samples = samples.Where(x => ProviderClubCode(x.RealTeam) is { } code && clubFilter.Contains(code)).ToList();
        if (samples is not { Count: > 0 } || samples.Any(x => x.Vote == null || !float.IsFinite(x.Vote.Value)) || samples.GroupBy(x => x.Key).Any(x => x.Count() != 1))
            throw Invalid("Voti del provider mancanti o ambigui; lo snapshot salvato non viene cancellato.");
        await AuthorizeAsync(caller, cancellationToken);
        var clubs = await database.Clubs.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var names = await database.PlayerSeasons.AsNoTracking().Where(x => x.SeasonIndex == year).Select(x => new { x.PlayerId, x.Player.Name })
            .ToDictionaryAsync(x => x.PlayerId, x => x.Name, cancellationToken);
        var entries = new List<CachedLiveVote>(samples.Count);
        foreach (var sample in samples)
        {
            ValidatePlayer(sample);
            var code = ProviderClubCode(sample.RealTeam) ?? throw Invalid("Nome/codice club del provider non verificabile o discordante.");
            entries.Add(new(sample.Key, names.GetValueOrDefault(sample.Key) ?? sample.Name.Trim(), code,
                clubs.GetValueOrDefault(code) ?? FantacalcioClubCodes.CanonicalName(code, sample.RealTeam.Name), (int)sample.Role,
                sample.IsActive, sample.Visible, FootballLiveDataMapping.Cached(sample.Vote)));
        }
        await liveData.PublishLiveVotesAsync(fetchedAt, new(year, day, entries.OrderBy(x => x.PlayerId, StringComparer.Ordinal).ToArray()), clubFilter, cancellationToken);
    }
    private async Task ChancesAsync(int year, int day, CancellationToken cancellationToken, ClaimsPrincipal? caller)
    {
        var fetchedAt = clock.GetUtcNow();
        var sources = new[] { "Fantagazzetta", "Gazzetta", "Injury" };
        var samples = new List<ChancedRealPlayer>();
        foreach (var name in sources)
        {
            var parser = formations.Create(name) ?? throw new InvalidOperationException("Formation parser missing.");
            var values = await parser.GetPlayersAsync().WaitAsync(cancellationToken);
            if (values.Count == 0) throw Invalid("Una sorgente probabilità non ha restituito dati; si conserva lo snapshot precedente.");
            samples.AddRange(values);
        }
        await AuthorizeAsync(caller, cancellationToken);
        var players = await database.PlayerSeasons.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
            .Where(x => x.SeasonIndex == year && x.ClubId != null).OrderBy(x => x.PlayerId).ToArrayAsync(cancellationToken);
        if (players.Length == 0) throw Invalid("Catalogo stagionale non disponibile; si conserva lo snapshot precedente.");
        // Same priority as before: flags are OR-ed across sources, status and
        // description come from the last source reporting the player (Injury last).
        var found = samples.ToLookup(x => x.Key, StringComparer.Ordinal);
        var entries = players.Select(player =>
        {
            var rows = found[player.PlayerId].ToArray();
            var last = rows.LastOrDefault()?.Chance;
            return new CachedChance(player.PlayerId, player.Player.Name, player.ClubId, player.Club!.Name, (int)player.Position, player.IsActive, player.IsVisible,
                rows.Any(x => x.Chance.Fantagazzetta), rows.Any(x => x.Chance.Gazzetta), rows.Any(x => x.Chance.Mediaset), rows.Any(x => x.Chance.Sky),
                (int)(last?.Status ?? ChanceType.Normal), last?.Description);
        }).ToArray();
        await liveData.PublishChancesAsync(fetchedAt, new(year, day, sources, entries), cancellationToken);
    }
    private async Task PlayerSeasonAsync(FootballPlayer person, FootballClub club, RealPlayer sample, int year, CancellationToken cancellationToken)
    {
        if (!database.PlayerSeasons.Local.Any(x => x.PlayerId == person.Id && x.SeasonIndex == year) && !await database.PlayerSeasons.AnyAsync(x => x.PlayerId == person.Id && x.SeasonIndex == year, cancellationToken))
            database.PlayerSeasons.Add(new() { Player = person, Club = club, SeasonIndex = year,
                Position = (PlayerPosition)sample.Role, IsActive = sample.IsActive, IsVisible = sample.Visible });
    }
    private FootballPlayer Person(RealPlayer sample, FootballPlayer[] players)
    {
        ValidatePlayer(sample);
        var matches = players.Concat(database.Players.Local).Distinct().Where(x => x.Id == sample.Key).ToArray();
        if (matches.Length > 1) throw Invalid("Identità del giocatore ambigua.");
        if (matches.Length == 1) return matches[0];
        var player = new FootballPlayer { Name = sample.Name.Trim() }; database.Players.Add(player); return player;
    }
    private static void ValidatePlayer(RealPlayer sample)
    {
        if (string.IsNullOrWhiteSpace(sample?.Name) || string.IsNullOrWhiteSpace(sample.Key) || sample.Role is < Role.Undefined or > Role.Forward)
            throw Invalid("Giocatore del provider non valido.");
    }
    private FootballClub Club(RealTeam sample, FootballClub[] clubs)
    {
        if (string.IsNullOrWhiteSpace(sample?.Name)) throw Invalid("Squadra reale del provider non valida.");
        string code;
        try { code = KeyUtils.GetClubKey(sample.ExplicitAbbreviation, sample.Name); }
        catch (ArgumentException) { throw Invalid("Nome/codice club del provider non verificabile o discordante."); }
        var matches = clubs.Concat(database.Clubs.Local).Distinct().Where(x => x.Id == code).ToArray();
        if (matches.Length > 1) throw Invalid("Identità della squadra reale ambigua.");
        if (matches.Length == 1)
        {
            matches[0].Name = FantacalcioClubCodes.CanonicalName(code, matches[0].Name);
            matches[0].Abbreviation = code;
            return matches[0];
        }
        var club = new FootballClub { Id = code, Name = FantacalcioClubCodes.CanonicalName(code, sample.Name), Abbreviation = code };
        database.Clubs.Add(club); return club;
    }
    private async Task MembershipAsync(string code, int year, ClubMembershipSource source, CancellationToken cancellationToken)
    {
        var row = database.ClubSeasons.Local.SingleOrDefault(x => x.ClubId == code && x.SeasonIndex == year)
            ?? await database.ClubSeasons.SingleOrDefaultAsync(x => x.ClubId == code && x.SeasonIndex == year, cancellationToken);
        if (row is null) database.ClubSeasons.Add(new() { ClubId = code, SeasonIndex = year, Sources = source });
        else row.Sources |= source;
    }
    private async Task SeasonAsync(int year, CancellationToken cancellationToken)
    { if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year, cancellationToken)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 }); }
    private async Task DatasetAsync(string kind, int year, int day, CancellationToken cancellationToken)
    { if (!await database.FootballDatasets.AnyAsync(x => x.Kind == kind && x.SeasonIndex == year && x.Day == day, cancellationToken)) database.FootballDatasets.Add(new() { Kind = kind, SeasonIndex = year, Day = day }); }
    private static DateTimeOffset? Utc(DateTime? date)
    {
        if (date is null) return null;
        var wall = DateTime.SpecifyKind(date.Value, DateTimeKind.Unspecified); var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        if (zone.IsInvalidTime(wall) || zone.IsAmbiguousTime(wall)) throw Invalid("Ora locale del calendario ambigua o non valida.");
        return new(DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(wall, zone), DateTimeKind.Utc));
    }
    private async Task RealRankAsync(int year, CancellationToken cancellationToken)
    {
        var games = await database.FootballMatches.Where(x => x.SeasonIndex == year).ToArrayAsync(cancellationToken);
        var ranks = games.SelectMany(x => new[] { x.HomeClubId, x.AwayClubId }).Distinct().ToDictionary(id => id, id => new FootballClubRank { SeasonIndex = year, ClubId = id });
        foreach (var code in ranks.Keys) await MembershipAsync(code, year, ClubMembershipSource.Standings, cancellationToken);
        foreach (var game in games.Where(x => !x.Delayed && x.IsFinished != false && x.KickoffUtc < clock.GetUtcNow() && x.HomeGoals != null && x.AwayGoals != null))
        {
            var home = ranks[game.HomeClubId]; var away = ranks[game.AwayClubId]; home.Played++; away.Played++;
            home.GoalsFor += game.HomeGoals!.Value; home.GoalsAgainst += game.AwayGoals!.Value; away.GoalsFor += game.AwayGoals.Value; away.GoalsAgainst += game.HomeGoals.Value;
            if (game.HomeGoals > game.AwayGoals) { home.Victories++; home.Points += 3; away.Defeats++; }
            else if (game.HomeGoals < game.AwayGoals) { away.Victories++; away.Points += 3; home.Defeats++; }
            else { home.Draws++; away.Draws++; home.Points++; away.Points++; }
        }
        await database.FootballClubRanks.Where(x => x.SeasonIndex == year).ExecuteDeleteAsync(cancellationToken);
        foreach (var (rank, ordinal) in ranks.Values.OrderByDescending(x => x.Points).ThenByDescending(x => x.GoalsFor - x.GoalsAgainst).ThenByDescending(x => x.GoalsFor).ThenBy(x => x.GoalsAgainst).ThenBy(x => x.ClubId).Select((rank, index) => (rank, index)))
        { rank.Ordinal = ordinal; database.FootballClubRanks.Add(rank); }
        await DatasetAsync("realrank", year, 0, cancellationToken);
    }
    private async Task StatisticsAsync(int year, int until, CancellationToken cancellationToken, ClaimsPrincipal? caller)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AuthorizeAsync(caller, cancellationToken);
        var players = await database.PlayerSeasons.Include(x => x.Player).Include(x => x.Club).Where(x => x.SeasonIndex == year && x.ClubId != null).ToArrayAsync(cancellationToken);
        var votes = await database.FootballVotes.Include(x => x.Player).Include(x => x.Club).Where(x => x.SeasonIndex == year && x.Day <= until && x.Source == FootballVoteSource.Official).ToArrayAsync(cancellationToken);
        var domain = votes.GroupBy(x => x.Day).ToDictionary(x => x.Key, x => new VotedRealPlayerWrapper { Players = x.Select(row => new VotedRealPlayer
        { Name = row.Player.Name, Role = (Role)row.PlayerRole, RealTeam = row.Club is null ? null! : FootballMapping.DomainClub(row.Club), Vote = FootballMapping.DomainVote(row) }).ToList() });
        var samples = FootballStatisticsCalculator.Calculate(players.Select(x => new RealPlayer { Name = x.Player.Name, Role = (Role)x.Position, RealTeam = FootballMapping.DomainClub(x.Club!), IsActive = x.IsActive, Visible = x.IsVisible }).ToList(), until, domain, scoring);
        var existing = await database.FootballPlayerStatistics.Where(x => x.SeasonIndex == year).AsSplitQuery().ToArrayAsync(cancellationToken);
        foreach (var sample in samples.Players)
        {
            var player = players.Single(x => x.PlayerId == sample.Key);
            var row = existing.SingleOrDefault(x => x.PlayerId == player.PlayerId);
            if (row is null) database.FootballPlayerStatistics.Add(row = new() { SeasonIndex = year, PlayerId = player.PlayerId });
            row.ClubId = player.ClubId!; row.PlayerRole = (int)player.Position; row.IsActive = player.IsActive; row.IsVisible = player.IsVisible;
            row.Summatory = sample.Summatory; row.FantaSummatory = sample.FantaSummatory; row.WithVote = sample.WithVote; row.WithoutVote = sample.WithoutVote; row.NoPlayed = sample.NoPlayed;
            row.WithSpecial = sample.WithSpecial; row.Goals = sample.Goal; row.Penalties = sample.Penalty; row.Assists = sample.Assist; row.StoppedPenalties = sample.StoppedPenalty;
            row.SufferedGoals = sample.SufferedGoal; row.WrongedPenalties = sample.WrongedPenalty; row.OwnGoals = sample.OwnGoal; row.YellowCards = sample.YellowCard; row.RedCards = sample.RedCard;
            row.EnoughVotes = sample.IsEnough; row.ManOfTheMatch = sample.ManOfTheMatch; row.Injured = sample.Injured; row.HasGameHistory = true;
            row.Games.Clear();
        }
        await database.SaveChangesAsync(cancellationToken);
        foreach (var sample in samples.Players)
        {
            var row = database.FootballPlayerStatistics.Local.Single(x => x.SeasonIndex == year && x.PlayerId == players.Single(player => KeyUtils.GetPlayerKey(player.Player.Name) == sample.Key).PlayerId);
            foreach (var (game, ordinal) in sample.AllGames!.Select((game, ordinal) => (game, ordinal))) row.Games.Add(new() { Day = game.SerieADay, Ordinal = ordinal, Vote = game.Vote, Positiveness = game.Positiveness });
        }
        await DatasetAsync("statplayerswrapper", year, 0, cancellationToken);
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private async Task AuthorizeAsync(ClaimsPrincipal? caller, CancellationToken cancellationToken)
    {
        if (caller == null) return; // Internal scheduled runner, never an HTTP/MCP principal.
        var actor = await accounts.CurrentAsync(caller, cancellationToken);
        if (!caller.IsInRole("super_admin") || !accounts.IsGlobalAdministrator(actor.Email)) throw new ApplicationFault(403, "football_admin_required", "Serve un amministratore globale per aggiornare le sorgenti condivise.");
    }
    private static ApplicationFault Invalid(string message) => new(409, "football_provider_incomplete", message);
}
