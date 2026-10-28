using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Competitions;

// Structural rules shared by the workspace commands and the whole-group editor.
// Every method runs inside the caller's write transaction.
internal static class CompetitionStructure
{
    // Autonomous leagues keep the established rule as an explicit act: teams of a
    // linked autonomous basket are enrolled when the link or the team is created.
    // Derived leagues are never enrolled here: they only admit qualified/selected teams.
    public static async Task EnsureAutonomousEnrollmentAsync(FantasoccerDbContext database, Guid group, Guid? actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var links = await database.LeagueBasketSeasons.AsNoTracking().Where(x => x.GroupId == group &&
                database.Leagues.Any(l => l.GroupId == group && l.LeagueId == x.LeagueId && l.Mode == LeagueMode.Autonomous && !l.IsArchived) &&
                database.LeagueSeasons.Any(s => s.GroupId == group && s.LeagueId == x.LeagueId && s.SeasonIndex == x.SeasonIndex && !s.IsArchived))
            .ToArrayAsync(cancellationToken);
        if (links.Length == 0) return;
        var teams = await database.TeamSeasons.AsNoTracking().Where(x => x.GroupId == group && !x.IsArchived)
            .Select(x => new { x.Id, x.BasketId, x.SeasonIndex }).ToArrayAsync(cancellationToken);
        var existing = (await database.LeagueParticipations.AsNoTracking().Where(x => x.GroupId == group)
            .Select(x => new { x.LeagueId, x.SeasonIndex, x.TeamId }).ToArrayAsync(cancellationToken))
            .Select(x => (x.LeagueId, x.SeasonIndex, x.TeamId)).ToHashSet();
        foreach (var link in links)
            foreach (var team in teams.Where(x => x.BasketId == link.BasketId && x.SeasonIndex == link.SeasonIndex))
                if (existing.Add((link.LeagueId, link.SeasonIndex, team.Id)))
                    database.LeagueParticipations.Add(new() { GroupId = group, LeagueId = link.LeagueId, SeasonIndex = link.SeasonIndex, TeamId = team.Id,
                        BasketId = team.BasketId, Source = ParticipationSource.BasketEnrollment, AdmittedAtUtc = now, AdmittedByUserId = actor });
    }

    public static async Task<GroupBasket> CreateDerivedBasketAsync(FantasoccerDbContext database, Guid group, Guid basketId, string name, int season, Guid origin,
        CancellationToken cancellationToken)
    {
        var source = await database.Baskets.SingleOrDefaultAsync(x => x.GroupId == group && x.BasketId == origin, cancellationToken)
            ?? throw Invalid("Il basket di origine non appartiene al gruppo.");
        if (source.Kind != BasketKind.Autonomous) throw Invalid("Un basket derivato deriva sempre da un basket autonomo.");
        if (!await database.BasketSeasons.AnyAsync(x => x.GroupId == group && x.BasketId == origin && x.SeasonIndex == season, cancellationToken))
            throw Invalid("Il basket di origine non esiste nella stagione richiesta.");
        var basket = new GroupBasket { GroupId = group, BasketId = basketId, Name = name, Kind = BasketKind.Derived };
        database.Baskets.Add(basket);
        database.BasketDerivations.Add(new() { GroupId = group, BasketId = basketId, OriginBasketId = origin });
        return basket;
    }

    // The derived team only links to the origin: no roster copy, no owners, no credits.
    public static async Task<FantasyTeamSeason> CreateDerivedTeamAsync(FantasoccerDbContext database, Guid group, Guid derivedBasket, int season, Guid originTeam,
        string? name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var derivation = await database.BasketDerivations.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == group && x.BasketId == derivedBasket, cancellationToken)
            ?? throw Invalid("Il basket non è derivato: le sue squadre hanno rose proprie.");
        if (!await database.BasketSeasons.AnyAsync(x => x.GroupId == group && x.BasketId == derivedBasket && x.SeasonIndex == season, cancellationToken))
            throw Invalid("Il basket derivato non esiste nella stagione richiesta.");
        var origin = await database.TeamSeasons.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == group && x.Id == originTeam && x.SeasonIndex == season, cancellationToken)
            ?? throw Invalid("Squadra originale non trovata nel gruppo e nella stagione.");
        if (origin.BasketId != derivation.OriginBasketId || origin.IsArchived)
            throw Invalid("La squadra originale deve appartenere, attiva, al basket di origine del basket derivato.");
        var existing = await database.TeamDerivations.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == group && x.BasketId == derivedBasket &&
            x.SeasonIndex == season && x.OriginTeamId == originTeam, cancellationToken);
        if (existing is not null)
            return await database.TeamSeasons.SingleAsync(x => x.GroupId == group && x.Id == existing.TeamId, cancellationToken);
        var display = string.IsNullOrWhiteSpace(name) ? origin.Name : name.Trim();
        var team = new FantasyTeamSeason { Id = Guid.NewGuid(), GroupId = group, BasketId = derivedBasket, SeasonIndex = season, Name = display, NormalizedName = display.ToLowerInvariant() };
        database.TeamSeasons.Add(team);
        database.TeamDerivations.Add(new() { GroupId = group, BasketId = derivedBasket, SeasonIndex = season, TeamId = team.Id,
            OriginBasketId = derivation.OriginBasketId, OriginTeamId = originTeam, CreatedAtUtc = now });
        return team;
    }

    // Sources of a derived league: same group and season (also enforced by FK).
    public static async Task SetLeagueSourcesAsync(FantasoccerDbContext database, Guid group, Guid league, int season, LeagueSourceDto[] sources, CancellationToken cancellationToken)
    {
        if (sources.Select(x => x.SourceLeagueId).Distinct().Count() != sources.Length || sources.Any(x => x.SourceLeagueId == league || x.QualifiedTeams is < 1 or > 64))
            throw Invalid("Leghe di origine duplicate, coincidenti con la lega o con un numero di qualificate non valido.");
        foreach (var source in sources)
            if (!await database.LeagueSeasons.AnyAsync(x => x.GroupId == group && x.LeagueId == source.SourceLeagueId && x.SeasonIndex == season, cancellationToken))
                throw Invalid("Una lega di origine non appartiene al gruppo o alla stagione.");
        var stored = await database.LeagueDerivations.Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == season).ToListAsync(cancellationToken);
        foreach (var removed in stored.Where(x => sources.All(source => source.SourceLeagueId != x.SourceLeagueId)))
        {
            if (await database.LeagueParticipations.AnyAsync(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == season && x.SourceLeagueId == removed.SourceLeagueId, cancellationToken))
                throw new ApplicationFault(409, "league_source_in_use", "Esistono squadre qualificate da questa lega di origine: la provenienza non può essere rimossa.");
            database.LeagueDerivations.Remove(removed);
        }
        foreach (var (source, ordinal) in sources.Select((source, ordinal) => (source, ordinal)))
        {
            var row = stored.SingleOrDefault(x => x.SourceLeagueId == source.SourceLeagueId);
            if (row is null) database.LeagueDerivations.Add(row = new() { GroupId = group, LeagueId = league, SeasonIndex = season, SourceLeagueId = source.SourceLeagueId });
            row.Ordinal = ordinal; row.QualifiedTeams = source.QualifiedTeams; row.Stage = string.IsNullOrWhiteSpace(source.Stage) ? null : source.Stage.Trim();
        }
    }

    public static async Task ValidateBasketKindsAsync(FantasoccerDbContext database, Guid group, LeagueMode mode, IEnumerable<Guid> baskets, CancellationToken cancellationToken)
    {
        var ids = baskets.Distinct().ToArray();
        var expected = mode == LeagueMode.Derived ? BasketKind.Derived : BasketKind.Autonomous;
        var kinds = await database.Baskets.Where(x => x.GroupId == group && ids.Contains(x.BasketId)).Select(x => x.Kind).ToArrayAsync(cancellationToken);
        var local = database.Baskets.Local.Where(x => x.GroupId == group && ids.Contains(x.BasketId)).Select(x => x.Kind);
        if (kinds.Concat(local).Any(x => x != expected))
            throw Invalid(mode == LeagueMode.Derived ? "Una lega derivata usa soltanto basket derivati." : "Una lega autonoma usa soltanto basket autonomi.");
    }

    private static ApplicationFault Invalid(string message) => new(400, "invalid_competition_structure", message);
}
