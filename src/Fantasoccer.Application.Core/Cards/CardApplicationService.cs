using System.Security.Claims;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Core.Cards;

public sealed class CardApplicationService(FantasoccerDbContext database, AccountApplicationService accounts, ICardImageStorage images, IOptions<AssetLinkOptions> links)
{
    private const string DefaultLibrary = "default";
    public async Task<CardDto[]> ListAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        await accounts.CurrentAsync(caller, cancellationToken);
        return (await database.RuleCards.AsNoTracking().Where(x => x.Library == DefaultLibrary && !x.IsArchived).OrderBy(x => x.Ordinal).AsSplitQuery().ToArrayAsync(cancellationToken)).Select(Dto).ToArray();
    }
    public async Task<CardDto> GetAsync(ClaimsPrincipal caller, Guid id, CancellationToken cancellationToken = default)
    {
        await accounts.CurrentAsync(caller, cancellationToken);
        return Dto(await CardAsync(id, false, cancellationToken));
    }
    public async Task<CardDto> CreateAsync(ClaimsPrincipal caller, WriteCardRequest request, CancellationToken cancellationToken = default)
    {
        await AdminAsync(caller, cancellationToken);
        var card = Build(request);
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AdminAsync(caller, cancellationToken);
        if (!await database.CardLibraries.AnyAsync(x => x.Name == DefaultLibrary, cancellationToken)) database.CardLibraries.Add(new() { Name = DefaultLibrary });
        card.Id = Guid.NewGuid();
        card.Ordinal = (await database.RuleCards.Where(x => x.Library == DefaultLibrary).Select(x => (int?)x.Ordinal).MaxAsync(cancellationToken) ?? -1) + 1;
        database.RuleCards.Add(card);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Dto(card);
    }
    public async Task<CardDto> UpdateAsync(ClaimsPrincipal caller, Guid id, WriteCardRequest request, CancellationToken cancellationToken = default)
    {
        var replacement = Build(request);
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AdminAsync(caller, cancellationToken);
        var card = await CardAsync(id, true, cancellationToken);
        if (card.Version != request.ExpectedVersion) throw Changed();
        card.Name = replacement.Name; card.Description = replacement.Description; card.Type = replacement.Type;
        card.Points = replacement.Points; card.NegativePoints = replacement.NegativePoints; card.EnemyPoints = replacement.EnemyPoints;
        // Remove old dependent role lists before replacing an owned condition
        // with the same composite keys; all changes remain in this transaction.
        card.MyCondition = null; card.EnemyCondition = null;
        await database.SaveChangesAsync(cancellationToken);
        card.MyCondition = replacement.MyCondition; card.EnemyCondition = replacement.EnemyCondition;
        card.Version++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Dto(card);
    }
    public async Task DeleteAsync(ClaimsPrincipal caller, Guid id, int expectedVersion, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AdminAsync(caller, cancellationToken);
        var card = await CardAsync(id, true, cancellationToken);
        if (card.Version != expectedVersion) throw Changed();
        // Keep historical references and shared/legacy image versions intact.
        card.IsArchived = true; card.Version++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task<CardDto> UploadImageAsync(ClaimsPrincipal caller, Guid id, int expectedVersion, byte[] bytes, CancellationToken cancellationToken = default)
    {
        await AdminAsync(caller, cancellationToken);
        await CardAsync(id, false, cancellationToken);
        var format = ImageFileValidation.Validate(bytes, 5 * 1024 * 1024, allowGif: true);
        var name = $"{id}/{Guid.NewGuid():N}.{format.Extension}";
        await images.UploadAsync(name, bytes, format.ContentType, cancellationToken);
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await AdminAsync(caller, cancellationToken);
        var card = await CardAsync(id, true, cancellationToken);
        if (card.Version != expectedVersion) throw Changed();
        card.ImageBlobName = name; card.Version++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Dto(card);
    }
    private async Task<RuleCard> CardAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = database.RuleCards.Where(x => x.Library == DefaultLibrary && x.Id == id && !x.IsArchived).AsSplitQuery();
        return await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApplicationFault(404, "card_not_found", "Carta non trovata.");
    }
    private async Task AdminAsync(ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var actor = await accounts.CurrentAsync(caller, cancellationToken);
        if (!caller.IsInRole("super_admin") || !accounts.IsGlobalAdministrator(actor.Email)) throw new ApplicationFault(403, "card_admin_required", "Serve un amministratore globale per modificare il catalogo condiviso.");
    }
    private static RuleCard Build(WriteCardRequest request)
    {
        var name = request.Name?.Trim(); var description = request.Description?.Trim();
        if (name is not { Length: >= 1 and <= 200 } || description is not { Length: >= 1 and <= 20000 } || request.Type is < 0 or > 5 ||
            !new[] { request.Points, request.NegativePoints, request.EnemyPoints }.All(float.IsFinite))
            throw new ApplicationFault(400, "invalid_card", "Nome, descrizione, tipo o punti della carta non validi.");
        return new() { Library = DefaultLibrary, Name = name, Description = description, Type = request.Type, Points = request.Points,
            NegativePoints = request.NegativePoints, EnemyPoints = request.EnemyPoints, MyCondition = CardRulesMapping.FromDto(request.MyCondition), EnemyCondition = CardRulesMapping.FromDto(request.EnemyCondition) };
    }
    private CardDto Dto(RuleCard card) => new(card.Id, card.Name, card.Description, card.Type, card.Points, card.NegativePoints, card.EnemyPoints,
        CardRulesMapping.ToDto(card.MyCondition), CardRulesMapping.ToDto(card.EnemyCondition), card.ImageBlobName is not { } image ? null :
            links.Value.PublicBlobBaseUrl.TrimEnd('/') + "/cardimages/" + string.Join('/', image.Split('/').Select(Uri.EscapeDataString)), card.Version);
    private static ApplicationFault Changed() => new(409, "card_changed", "La carta è cambiata. Ricaricare prima di salvare.");
}
