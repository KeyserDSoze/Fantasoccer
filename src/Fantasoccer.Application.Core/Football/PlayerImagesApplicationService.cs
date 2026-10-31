using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Business.Services;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fantasoccer.Application.Core.Football;

public sealed record RefreshPlayerImageRequest(Guid GroupId, int Year, string PlayerId);
public sealed record RefreshTeamImagesRequest(Guid GroupId, int Year, Guid TeamId);
public sealed record RefreshedPlayerImageDto(string PlayerId, string PlayerKey, string PlayerName, string Revision);
public sealed record PlayerImageFailureDto(string PlayerId, string PlayerKey, string PlayerName, string? Error);
public sealed record TeamImagesRefreshedDto(int Total, int SucceededCount, int FailedCount, RefreshedPlayerImageDto[] Succeeded, PlayerImageFailureDto[] Failed);

public sealed class PlayerImagesApplicationService(FantasoccerDbContext database, IAuthorizationService authorization, IPlayerImageRefresher images,
    ILogger<PlayerImagesApplicationService> logger)
{
    public async Task<RefreshedPlayerImageDto> RefreshAsync(ClaimsPrincipal caller, RefreshPlayerImageRequest request, CancellationToken cancellationToken = default)
    {
        await MemberAsync(caller, request.GroupId);
        var player = await database.PlayerSeasons.AsNoTracking().Include(x => x.Player).Include(x => x.Club)
            .SingleOrDefaultAsync(x => x.SeasonIndex == request.Year && x.PlayerId == request.PlayerId, cancellationToken)
            ?? throw new ApplicationFault(404, "player_not_found", "Giocatore non trovato nella stagione selezionata.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            if (!await images.ReloadAsync(Domain(player), timeout.Token)) throw new ApplicationFault(404, "official_image_unavailable", "Nessuna foto ufficiale disponibile. L'immagine attuale non è stata modificata.");
        }
        catch (ExternalHttpDisabledException) { throw ExternalFetchingDisabled(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new ApplicationFault(504, "image_source_timeout", "La fonte immagini non ha risposto entro il limite."); }
        catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException or InvalidDataException)
        {
            // Never log provider bodies, signed Blob URLs, player data or connection strings.
            logger.LogWarning("Player image source failed ({FailureType}, HTTP {SourceStatus}).", error.GetType().Name, (error as HttpRequestException)?.StatusCode);
            throw new ApplicationFault(502, "image_source_unavailable", "La fonte immagini non è disponibile o ha restituito dati non validi.");
        }
        return new(player.PlayerId, player.PlayerId, player.Player.Name, Guid.NewGuid().ToString("N"));
    }
    public async Task<TeamImagesRefreshedDto> RefreshTeamAsync(ClaimsPrincipal caller, RefreshTeamImagesRequest request, CancellationToken cancellationToken = default)
    {
        await MemberAsync(caller, request.GroupId);
        if (request.Year != FantacalcioTime.ActualYear) throw new ApplicationFault(400, "invalid_image_season", "L'aggiornamento immagini della rosa richiede la stagione corrente.");
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        var team = await database.TeamSeasons.AsNoTracking().Include(x => x.Owners).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == request.TeamId && x.GroupId == request.GroupId && x.SeasonIndex == request.Year && !x.IsArchived, cancellationToken)
            ?? throw new ApplicationFault(404, "team_not_found", "Squadra non trovata nel gruppo e nella stagione richiesti.");
        if (!team.Owners.Any(x => x.Membership.User.Email == email)) throw new ApplicationFault(403, "team_images_owner_required", "Solo i proprietari della squadra possono aggiornare le immagini della rosa.");
        var roster = await database.RosterSnapshots.AsNoTracking().Include(x => x.Players).ThenInclude(x => x.Player).Include(x => x.Players).ThenInclude(x => x.Club)
            .Where(x => x.GroupId == request.GroupId && x.TeamId == request.TeamId && x.SeasonIndex == request.Year && x.Kind == RosterKind.Season).AsSplitQuery().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApplicationFault(404, "roster_not_found", "Rosa non trovata.");
        var active = roster.Players.Where(x => x.Status == 0).DistinctBy(x => x.PlayerId).ToArray();
        IReadOnlyList<PlayerImageReloadResult> result;
        try
        {
            result = await images.ReloadManyAsync(active.Select(x => new RealPlayer { PlayerId = x.PlayerId, Name = x.Player.Name, Role = (Role)x.PlayerRole,
                RealTeam = FootballMapping.DomainClubOrEmpty(x.Club), IsActive = x.IsActive, Visible = x.IsVisible }).ToArray(), cancellationToken);
        }
        catch (ExternalHttpDisabledException) { throw ExternalFetchingDisabled(); }
        var succeeded = result.Where(x => x.Succeeded).Select(x => new RefreshedPlayerImageDto(x.PlayerId, x.PlayerKey, x.PlayerName, Guid.NewGuid().ToString("N"))).ToArray();
        var failed = result.Where(x => !x.Succeeded).Select(x => new PlayerImageFailureDto(x.PlayerId, x.PlayerKey, x.PlayerName, x.Error)).ToArray();
        return new(result.Count, succeeded.Length, failed.Length, succeeded, failed);
    }
    private async Task MemberAsync(ClaimsPrincipal caller, Guid group)
    {
        if (!(await authorization.AuthorizeAsync(caller, group, "workspace-member")).Succeeded) throw new ApplicationFault(403, "player_images_access_denied", "Accesso al gruppo non consentito.");
    }
    private static RealPlayer Domain(PlayerSeason row) => new() { PlayerId = row.PlayerId, Name = row.Player.Name, Role = (Role)row.Position,
        RealTeam = row.Club == null ? null! : FootballMapping.DomainClub(row.Club), IsActive = row.IsActive, Visible = row.IsVisible };
    private static ApplicationFault ExternalFetchingDisabled() => new(503, "external_http_disabled",
        "Le chiamate alle fonti esterne sono disabilitate in questo ambiente. Per aggiornare le immagini abilita ExternalHttp:Enabled (EXTERNAL_HTTP_ENABLED=true nel file .env locale) e riavvia l'API.");
}
