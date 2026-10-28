using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Business;
using Fantasoccer.Business.Services;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Competitions;

public sealed record OpeningStandingTeamDto(Guid TeamId, Guid BasketId, string Owner, string? Name, float Score, int Prize, int Position);
public sealed record OpeningStandingsDto(Guid Group, Guid League, int Year, int SerieADays, OpeningStandingTeamDto[] Teams, int Version)
{
    public bool IsComplete { get; init; } = true;
}

public sealed class OpeningCompetitionApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions,
    TeamRosterApplicationService rosters, ITeamCalculator calculator, TimeProvider clock)
{
    public async Task<OpeningStandingsDto?> GetAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken = default)
    {
        await ContextAsync(caller, group, league, year, cancellationToken);
        var snapshot = await database.OpeningStandingSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        return snapshot is null ? null : Dto(snapshot);
    }

    public Task<OpeningStandingsDto> PrepareAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken = default)
        => PrepareCoreAsync(caller, group, league, year, cancellationToken);

    internal Task<OpeningStandingsDto> PrepareScheduledAsync(Guid group, Guid league, int year, CancellationToken cancellationToken)
        => PrepareCoreAsync(null, group, league, year, cancellationToken);

    private async Task<OpeningStandingsDto> PrepareCoreAsync(ClaimsPrincipal? caller, Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var (season, rules) = await ContextAsync(caller, group, league, year, cancellationToken);
        var stored = await database.OpeningStandingSnapshots.SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        var complete = true;
        if (rules.WaitForPostponedMatches)
        {
            var matches = await database.FootballMatches.AsNoTracking().Where(x => x.SeasonIndex == year && x.SerieADay <= rules.OpeningCompetitionSerieADays).ToArrayAsync(cancellationToken);
            var votes = await database.FootballVotes.AsNoTracking().Where(x => x.SeasonIndex == year && x.Day <= rules.OpeningCompetitionSerieADays &&
                x.Source == FootballVoteSource.Official && x.IsFinal && x.HasVote).Select(x => new { x.Day, x.ClubId }).ToArrayAsync(cancellationToken);
            complete = Enumerable.Range(1, rules.OpeningCompetitionSerieADays).All(day => CompetitionLifecycleApplicationService.MatchdayReady(
                matches.Where(x => x.SerieADay == day).ToArray(), true, votes.Where(x => x.Day == day).Select(x => x.ClubId).ToHashSet(), clock.GetUtcNow()));
        }
        // Never reopen an awarded opening competition because a rule changed.
        // A scheduled retry only finalizes an already prepared pending snapshot.
        if ((caller is null && stored is { IsComplete: true }) || !complete && stored is { IsComplete: true } || caller is null && !complete && stored is not null)
        { await transaction.CommitAsync(cancellationToken); return Dto(stored!); }
        var previous = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => x.GroupId == group && x.SeasonIndex == year - 1 && !x.IsArchived &&
                database.GroupLeagueBaskets.Any(link => link.GroupId == group && link.LeagueId == league && link.BasketId == x.BasketId)).AsSplitQuery().ToArrayAsync(cancellationToken);
        var sourceRosters = await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .Include(x => x.AdditionalOwners).ThenInclude(x => x.User).Where(x => x.GroupId == group && x.SeasonIndex == year - 1 && x.Kind == RosterKind.Season).AsSplitQuery().ToArrayAsync(cancellationToken);
        var opening = await database.RosterSnapshots.Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).AsSplitQuery().ToListAsync(cancellationToken);
        var scores = new Dictionary<Guid, float>();
        foreach (var team in previous)
        {
            if (!opening.Any(x => x.TeamId == team.Id && x.Kind == RosterKind.OpeningSeason))
            {
                var source = sourceRosters.SingleOrDefault(x => x.TeamId == team.Id)
                    ?? throw new ApplicationFault(409, "previous_roster_missing", "Una rosa della stagione precedente non è disponibile. Preparazione annullata.");
                var snapshot = TeamRosterApplicationService.Copy(source, RosterKind.OpeningSeason, 0, league);
                snapshot.SeasonIndex = year;
                snapshot.TeamOriginYear = year - 1;
                foreach (var player in source.Players.DistinctBy(x => x.PlayerId))
                    if (!database.PlayerSeasons.Local.Any(x => x.PlayerId == player.PlayerId && x.SeasonIndex == year) &&
                        !await database.PlayerSeasons.AnyAsync(x => x.PlayerId == player.PlayerId && x.SeasonIndex == year, cancellationToken))
                        database.PlayerSeasons.Add(new() { PlayerId = player.PlayerId, SeasonIndex = year,
                            ClubId = player.ClubId, Position = (PlayerPosition)player.PlayerRole,
                            IsActive = player.IsActive, IsVisible = player.IsVisible });
                database.RosterSnapshots.Add(snapshot); opening.Add(snapshot);
            }
            scores[team.Id] = 0;
        }
        var settings = LeagueRulesMapping.ToDomain(rules);
        var type = (LeagueType)(season.CompetitionType == 0 ? season.League.CompetitionType == 0 ? 1 : season.League.CompetitionType : season.CompetitionType);
        for (var day = 1; day <= rules.OpeningCompetitionSerieADays; day++)
        {
            await calculator.BuildRequestAsync(year, day, type, settings, true, true, true);
            foreach (var team in previous)
            {
                var snapshot = opening.SingleOrDefault(x => x.TeamId == team.Id && x.Kind == RosterKind.OpeningMatchday && x.Day == day)
                    ?? opening.Single(x => x.TeamId == team.Id && x.Kind == RosterKind.OpeningSeason);
                scores[team.Id] += calculator.CalculatePoint(snapshot.Players.Where(x => x.Status == 0).Select(CompetitionMapping.Domain)).Value;
            }
        }
        var prizes = OpeningCompetitionPrizeCalculator.Calculate(rules.OpeningPrizes.Select(x => new OpeningCompetitionPrizeRule(x.Position, x.Credits)),
            scores.Select(x => (x.Key.ToString(), x.Value)));
        if (stored is null) database.OpeningStandingSnapshots.Add(stored = new() { GroupId = group, LeagueId = league, SeasonIndex = year });
        stored.SerieADays = rules.OpeningCompetitionSerieADays;
        stored.IsComplete = complete;
        stored.Version++;
        stored.Teams.Clear();
        foreach (var (team, ordinal) in previous.OrderByDescending(x => scores[x.Id]).ThenBy(x => x.Id).Select((team, index) => (team, index)))
            stored.Teams.Add(new() { TeamId = team.Id, BasketId = team.BasketId, TeamOriginYear = year - 1, Ordinal = ordinal,
                Owner = team.Owners.Single(x => x.IsPrimary).Membership.User.OwnerReference, Name = team.Name, Score = scores[team.Id],
                Prize = complete ? prizes.GetValueOrDefault(team.Id.ToString()) : 0, Position = scores.Count(x => x.Value > scores[team.Id]) + 1 });
        await database.SaveChangesAsync(cancellationToken);
        var result = Dto(stored);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<TeamRosterDto> TeamAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, int day, Guid? teamId = null, CancellationToken cancellationToken = default)
    {
        var (_, rules) = await ContextAsync(caller, group, league, year, cancellationToken);
        if (day < 1 || day > rules.OpeningCompetitionSerieADays) throw new ApplicationFault(400, "invalid_opening_day", "Giornata iniziale non valida.");
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        var teams = await database.TeamSeasons.AsNoTracking().Where(x => x.GroupId == group && x.SeasonIndex == year - 1 &&
            database.GroupLeagueBaskets.Any(link => link.GroupId == group && link.LeagueId == league && link.BasketId == x.BasketId) &&
            (teamId != null ? x.Id == teamId : x.Owners.Any(owner => owner.Membership.User.Email == email))).Select(x => new { x.Id, x.BasketId }).ToArrayAsync(cancellationToken);
        if (teams.Length != 1) throw new ApplicationFault(409, "opening_team_selection_required", "Specificare la squadra iniziale tramite il suo ID.");
        var team = teams[0];
        var snapshot = await rosters.GetAsync(caller, group, team.BasketId, year, email, RosterKind.OpeningMatchday, day, league, cancellationToken, team.Id);
        if (snapshot != null) return snapshot;
        var source = await rosters.GetAsync(caller, group, team.BasketId, year, email, RosterKind.OpeningSeason, 0, league, cancellationToken, team.Id)
            ?? throw new ApplicationFault(409, "opening_not_prepared", "Preparare prima la competizione iniziale.");
        return source with { Kind = (int)RosterKind.OpeningMatchday, Day = day, Version = 0, SourceVersion = source.Version };
    }

    private async Task<(LeagueSeason Season, LeagueRules Rules)> ContextAsync(ClaimsPrincipal? caller, Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        var season = caller is not null ? await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken)
            : await database.LeagueSeasons.AsNoTracking().Include(x => x.League).SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && !x.IsArchived && !x.League.IsArchived, cancellationToken)
                ?? throw new ApplicationFault(404, "competition_not_found", "Competizione attiva non trovata.");
        var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year, cancellationToken);
        if (!season.League.IsMain || !rules.OpeningCompetitionEnabled || rules.OpeningCompetitionSerieADays is < 1 or > 38)
            throw new ApplicationFault(404, "opening_competition_unavailable", "Competizione iniziale non disponibile o configurazione delle giornate non valida.");
        return (season, rules);
    }
    private static OpeningStandingsDto Dto(OpeningStandingSnapshot row) => new(row.GroupId, row.LeagueId, row.SeasonIndex, row.SerieADays,
        row.Teams.OrderBy(x => x.Ordinal).Select(x => new OpeningStandingTeamDto(x.TeamId, x.BasketId, x.Owner, x.Name, x.Score, x.Prize, x.Position)).ToArray(), row.Version) { IsComplete = row.IsComplete };
}
