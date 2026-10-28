using System.Security.Claims;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Teams;

public sealed class AssetLinkOptions { public string PublicBlobBaseUrl { get; set; } = "https://fasostorage.blob.core.windows.net"; }
public static class AssetLinks
{
    public static string? Logo(AssetLinkOptions options, string? blobName) => blobName is null ? null
        : options.PublicBlobBaseUrl.TrimEnd('/') + "/logos/" + string.Join('/', blobName.Split('/').Select(Uri.EscapeDataString));
}
public interface ITeamLogoStorage
{
    Task<byte[]?> DownloadAsync(string blobName, CancellationToken cancellationToken);
    Task UploadAsync(string blobName, byte[] image, string contentType, CancellationToken cancellationToken);
}
public sealed record TeamAssetResult(string? LogoUrl);

public sealed class TeamAssetsApplicationService(FantasoccerDbContext database, IAuthorizationService authorization,
    ITeamLogoStorage storage, IOptions<AssetLinkOptions> links)
{
    public async Task<TeamAssetResult> UploadAsync(ClaimsPrincipal caller, Guid groupId, Guid basketId, int year, string owner,
        byte[] image, CancellationToken cancellationToken = default, Guid? teamId = null)
    {
        var format = ImageFileValidation.Validate(image, 2 * 1024 * 1024);
        var team = await OwnedAsync(caller, groupId, basketId, year, owner, false, cancellationToken, teamId);
        var key = $"{groupId}/{year}/{team.Id}/{Guid.NewGuid():N}.{format.Extension}";
        await storage.UploadAsync(key, image, format.ContentType, cancellationToken);
        // A failed/ambiguous DB commit may leave an unreferenced versioned blob,
        // never a deleted historical/shared logo or a partially overwritten file.
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        team = await OwnedAsync(caller, groupId, basketId, year, owner, true, cancellationToken, teamId);
        team.LogoBlobName = key;
        (await database.Groups.SingleAsync(x => x.Id == groupId, cancellationToken)).Revision++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(AssetLinks.Logo(links.Value, key));
    }

    public async Task<TeamAssetResult> CopyPreviousAsync(ClaimsPrincipal caller, Guid groupId, Guid basketId, int year, string owner,
        CancellationToken cancellationToken = default, Guid? teamId = null)
    {
        await OwnedAsync(caller, groupId, basketId, year, owner, false, cancellationToken, teamId);
        var email = owner.Trim().ToLowerInvariant();
        var previous = await database.TeamSeasons.AsNoTracking().Where(x => x.GroupId == groupId && x.BasketId == basketId && x.SeasonIndex == year - 1 &&
            x.Owners.Any(person => person.IsPrimary && person.Membership.User.Email == email)).Select(x => x.LogoBlobName).SingleOrDefaultAsync(cancellationToken);
        var image = previous is null ? null : await storage.DownloadAsync(previous, cancellationToken);
        if (image is null) throw new ApplicationFault(404, "previous_logo_not_found", "Logo della stagione precedente non disponibile.");
        return await UploadAsync(caller, groupId, basketId, year, owner, image, cancellationToken, teamId);
    }

    public async Task RenameAsync(ClaimsPrincipal caller, Guid groupId, Guid basketId, int year, string owner, string name, CancellationToken cancellationToken = default, Guid? teamId = null)
    {
        name = name.Trim();
        if (name.Length is < 3 or > 30) throw new ApplicationFault(400, "invalid_team_name", "Il nome deve contenere da 3 a 30 caratteri.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var team = await OwnedAsync(caller, groupId, basketId, year, owner, true, cancellationToken, teamId);
        var normalized = name.ToLowerInvariant();
        if (await database.TeamSeasons.AnyAsync(x => x.GroupId == groupId && x.BasketId == basketId && x.SeasonIndex == year && x.Id != team.Id && !x.IsArchived && x.NormalizedName == normalized, cancellationToken))
            throw new ApplicationFault(409, "duplicate_team_name", "Nome già usato nel basket.");
        team.Name = name; team.NormalizedName = normalized;
        (await database.Groups.SingleAsync(x => x.Id == groupId, cancellationToken)).Revision++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // teamId selects the exact team (a user can own several teams per group, also
    // a derived one whose owners are those of its origin); owner remains for old clients.
    private async Task<FantasyTeamSeason> OwnedAsync(ClaimsPrincipal caller, Guid groupId, Guid basketId, int year, string owner, bool tracking,
        CancellationToken cancellationToken, Guid? teamId = null)
    {
        if (!(await authorization.AuthorizeAsync(caller, groupId, "workspace-member")).Succeeded) throw Denied();
        var email = caller.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();
        IQueryable<FantasyTeamSeason> query;
        if (teamId is { } id)
        {
            var origin = await database.TeamDerivations.AsNoTracking().Where(x => x.GroupId == groupId && x.TeamId == id).Select(x => (Guid?)x.OriginTeamId).SingleOrDefaultAsync(cancellationToken) ?? id;
            query = database.TeamSeasons.Where(x => x.GroupId == groupId && x.BasketId == basketId && x.SeasonIndex == year && !x.IsArchived && x.Id == id &&
                database.TeamOwnerships.Any(person => person.GroupId == groupId && person.TeamId == origin && person.Membership.User.Email == email));
        }
        else
        {
            var target = owner.Trim().ToLowerInvariant();
            query = database.TeamSeasons.Where(x => x.GroupId == groupId && x.BasketId == basketId && x.SeasonIndex == year && !x.IsArchived &&
                x.Owners.Any(person => person.IsPrimary && person.Membership.User.Email == target) && x.Owners.Any(person => person.Membership.User.Email == email));
        }
        return await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken) ?? throw Denied();
    }
    private static ApplicationFault Denied() => new(403, "team_asset_access_denied", "Non sei proprietario o comproprietario della squadra.");
}
