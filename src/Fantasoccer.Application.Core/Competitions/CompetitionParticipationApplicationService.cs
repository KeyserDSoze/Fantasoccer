using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Competitions;

public sealed record ParticipantDto(Guid TeamId, Guid BasketId, string Name, bool IsDerived, Guid AuthoritativeTeamId, Guid AuthoritativeBasketId,
    string Owner, string[] AdditionalOwners, int Source, Guid? SourceLeagueId, int? QualificationRank, bool IsArchived, string? LogoUrl);
public sealed record LeagueParticipantsDto(Guid LeagueId, int SeasonIndex, int Mode, LeagueSourceDto[] Sources, ParticipantDto[] Participants);
public sealed record QualificationCandidateDto(Guid SourceLeagueId, string SourceLeagueName, int Rank, Guid OriginTeamId, string Name, string Owner,
    bool Qualified, bool AlreadyAdmitted, Guid? TargetBasketId, string? Problem);
public sealed record QualificationPreviewDto(Guid LeagueId, int SeasonIndex, QualificationCandidateDto[] Candidates);
public sealed record QualifiedTeamRequest(Guid SourceLeagueId, Guid OriginTeamId);
public sealed record ConfirmQualificationRequest(QualifiedTeamRequest[] Teams);
public sealed record AdmitParticipantRequest(Guid TeamId);

// Participations are explicit and stable: confirming a qualification freezes the
// admitted teams; later changes to the source standings never move them.
public sealed class CompetitionParticipationApplicationService(FantasoccerDbContext database, CompetitionApplicationService competitions,
    CompetitionTeamResolver teams, IOptions<AssetLinkOptions> assets, TimeProvider clock)
{
    public async Task<LeagueParticipantsDto> GetAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken = default)
    {
        var season = await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        var rows = await database.LeagueParticipations.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).ToArrayAsync(cancellationToken);
        var resolved = (await teams.ResolveManyAsync(group, year, rows.Select(x => x.TeamId).ToArray(), cancellationToken)).ToDictionary(x => x.TeamId);
        var sources = await database.LeagueDerivations.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year)
            .OrderBy(x => x.Ordinal).Select(x => new LeagueSourceDto(x.SourceLeagueId, x.QualifiedTeams, x.Stage)).ToArrayAsync(cancellationToken);
        return new(league, year, (int)season.League.Mode, sources, rows.Select(row =>
        {
            var team = resolved[row.TeamId];
            return new ParticipantDto(team.TeamId, team.BasketId, team.Name, team.IsDerived, team.AuthoritativeTeamId, team.AuthoritativeBasketId,
                team.OwnerReference, team.AdditionalOwners, (int)row.Source, row.SourceLeagueId, row.QualificationRank, team.IsArchived, AssetLinks.Logo(assets.Value, team.Logo));
        }).OrderBy(x => x.IsArchived).ThenBy(x => x.Name).ToArray());
    }

    public async Task AdmitAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, AdmitParticipantRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await competitions.AuthorizeManagerAsync(caller, group, league, year, cancellationToken);
        var actor = await ActorAsync(caller, cancellationToken);
        var team = await database.TeamSeasons.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == group && x.SeasonIndex == year && x.Id == request.TeamId && !x.IsArchived, cancellationToken)
            ?? throw new ApplicationFault(404, "team_not_found", "Squadra non trovata nel gruppo e nella stagione.");
        if (!await database.LeagueBasketSeasons.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.BasketId == team.BasketId, cancellationToken))
            throw new ApplicationFault(409, "basket_not_linked", "Il basket della squadra non è collegato alla lega in questa stagione.");
        if (await database.LeagueParticipations.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.TeamId == team.Id, cancellationToken)) return;
        database.LeagueParticipations.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = year, TeamId = team.Id, BasketId = team.BasketId,
            Source = ParticipationSource.Manual, AdmittedAtUtc = clock.GetUtcNow(), AdmittedByUserId = actor });
        (await database.Groups.SingleAsync(x => x.Id == group, cancellationToken)).Revision++;
        await DatabaseFaults.GuardAsync(() => database.SaveChangesAsync(cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task WithdrawAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, Guid teamId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await competitions.AuthorizeManagerAsync(caller, group, league, year, cancellationToken);
        var row = await database.LeagueParticipations.SingleOrDefaultAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.TeamId == teamId, cancellationToken)
            ?? throw new ApplicationFault(404, "participant_not_found", "La squadra non partecipa alla competizione.");
        // Calendars, lineups and results keep their participants: history is never rewritten.
        if (await database.FantasyMatches.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && (x.HomeTeamId == teamId || x.AwayTeamId == teamId), cancellationToken) ||
            await database.LeagueLineups.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.TeamId == teamId, cancellationToken))
            throw new ApplicationFault(409, "participant_in_use", "La squadra ha già partite o formazioni nella competizione: la partecipazione resta stabile.");
        database.LeagueParticipations.Remove(row);
        (await database.Groups.SingleAsync(x => x.Id == group, cancellationToken)).Revision++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<QualificationPreviewDto> PreviewAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, CancellationToken cancellationToken = default)
    {
        await competitions.AuthorizeAsync(caller, group, league, year, cancellationToken);
        return new(league, year, await CandidatesAsync(group, league, year, cancellationToken));
    }

    // Explicit selection: the manager confirms the teams. Each must belong to its
    // declared source league; ranks are recorded when the team is in the standing.
    public async Task<LeagueParticipantsDto> ConfirmAsync(ClaimsPrincipal caller, Guid group, Guid league, int year, ConfirmQualificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Teams is not { Length: > 0 and <= 128 } || request.Teams.Distinct().Count() != request.Teams.Length)
            throw new ApplicationFault(400, "invalid_qualification", "Selezionare le squadre qualificate, senza duplicati.");
        await using (var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken))
        {
            var season = await competitions.AuthorizeManagerAsync(caller, group, league, year, cancellationToken);
            if (season.League.Mode != LeagueMode.Derived) throw new ApplicationFault(409, "league_not_derived", "Solo una lega derivata riceve squadre qualificate.");
            var actor = await ActorAsync(caller, cancellationToken);
            var candidates = await CandidatesAsync(group, league, year, cancellationToken);
            var derivedBaskets = await DerivedBasketsAsync(group, league, year, cancellationToken);
            var now = clock.GetUtcNow();
            foreach (var selection in request.Teams)
            {
                var candidate = candidates.FirstOrDefault(x => x.SourceLeagueId == selection.SourceLeagueId && x.OriginTeamId == selection.OriginTeamId);
                if (!await database.LeagueDerivations.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.SourceLeagueId == selection.SourceLeagueId, cancellationToken))
                    throw new ApplicationFault(400, "invalid_qualification", "La lega di provenienza non è una sorgente di questa lega.");
                var origin = await database.TeamSeasons.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == group && x.SeasonIndex == year && x.Id == selection.OriginTeamId, cancellationToken)
                    ?? throw new ApplicationFault(404, "team_not_found", "Squadra originale non trovata.");
                var target = derivedBaskets.GetValueOrDefault(origin.BasketId)
                    ?? throw new ApplicationFault(409, "derived_basket_missing", "Collegare alla lega un basket derivato dal basket della squadra originale.");
                var derived = await CompetitionStructure.CreateDerivedTeamAsync(database, group, target, year, origin.Id, null, now, cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
                if (await database.LeagueParticipations.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year && x.TeamId == derived.Id, cancellationToken)) continue;
                database.LeagueParticipations.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = year, TeamId = derived.Id, BasketId = target,
                    Source = ParticipationSource.Qualification, SourceLeagueId = selection.SourceLeagueId, QualificationRank = candidate?.Rank,
                    AdmittedAtUtc = now, AdmittedByUserId = actor });
                await DatabaseFaults.GuardAsync(() => database.SaveChangesAsync(cancellationToken));
            }
            (await database.Groups.SingleAsync(x => x.Id == group, cancellationToken)).Revision++;
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        database.ChangeTracker.Clear();
        return await GetAsync(caller, group, league, year, cancellationToken);
    }

    private async Task<QualificationCandidateDto[]> CandidatesAsync(Guid group, Guid league, int year, CancellationToken cancellationToken)
    {
        var sources = await database.LeagueDerivations.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year)
            .OrderBy(x => x.Ordinal).ToArrayAsync(cancellationToken);
        var derivedBaskets = await DerivedBasketsAsync(group, league, year, cancellationToken);
        var admitted = (await database.LeagueParticipations.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year)
            .Select(x => x.TeamId).ToArrayAsync(cancellationToken)).ToHashSet();
        var admittedOrigins = (await teams.ResolveManyAsync(group, year, admitted, cancellationToken)).Select(x => x.AuthoritativeTeamId).ToHashSet();
        var result = new List<QualificationCandidateDto>();
        foreach (var source in sources)
        {
            var name = await database.Leagues.Where(x => x.GroupId == group && x.LeagueId == source.SourceLeagueId).Select(x => x.Name).SingleAsync(cancellationToken);
            var standing = await database.FantasyStandingSnapshots.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == source.SourceLeagueId && x.SeasonIndex == year && x.SnapshotDay == 0)
                .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);
            var stage = standing?.Stages.Where(x => source.Stage == null || x.Stage == source.Stage).OrderBy(x => x.Ordinal).FirstOrDefault();
            var rows = stage?.Teams.OrderBy(x => x.Ordinal).ToArray() ?? [];
            var resolved = (await teams.ResolveManyAsync(group, year, rows.Select(x => x.TeamId).ToArray(), cancellationToken)).ToDictionary(x => x.TeamId);
            foreach (var (row, index) in rows.Select((row, index) => (row, index)))
            {
                if (!resolved.TryGetValue(row.TeamId, out var team)) continue;
                var target = derivedBaskets.GetValueOrDefault(team.AuthoritativeBasketId);
                result.Add(new(source.SourceLeagueId, name, index + 1, team.AuthoritativeTeamId, team.Name, team.OwnerReference, index < source.QualifiedTeams,
                    admittedOrigins.Contains(team.AuthoritativeTeamId), target, target is null ? "derived_basket_missing" : null));
            }
            if (rows.Length == 0) result.Add(new(source.SourceLeagueId, name, 0, Guid.Empty, "", "", false, false, null, "standing_unavailable"));
        }
        return result.ToArray();
    }

    // Derived baskets linked to the league, keyed by their (autonomous) origin basket.
    private async Task<Dictionary<Guid, Guid?>> DerivedBasketsAsync(Guid group, Guid league, int year, CancellationToken cancellationToken) =>
        (await database.LeagueBasketSeasons.AsNoTracking().Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year)
            .Join(database.BasketDerivations, link => new { link.GroupId, link.BasketId }, derivation => new { derivation.GroupId, derivation.BasketId },
                (link, derivation) => new { derivation.OriginBasketId, derivation.BasketId }).ToArrayAsync(cancellationToken))
        .GroupBy(x => x.OriginBasketId).ToDictionary(x => x.Key, x => x.Count() == 1 ? (Guid?)x.Single().BasketId : null);

    private async Task<Guid> ActorAsync(ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        return await database.Users.Where(x => x.Email == email && x.IsEnabled).Select(x => x.Id).SingleAsync(cancellationToken);
    }
}
