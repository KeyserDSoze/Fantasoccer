using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Notifications;

public sealed class PushOutboxApplicationService(FantasoccerDbContext database, AccountApplicationService accounts, TimeProvider clock)
{
    // Producers must commit their checkpoint/event and the outbox together. No
    // network I/O occurs here. Stable keys prevent requeueing after a crash.
    public async Task EnqueueAsync(QueuedPushRequest request, CancellationToken cancellationToken = default)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Push enqueue requires the producer transaction.");
        if (request.Day is < 0 or > 38 || request.Kind is < 0 or > 6 || string.IsNullOrWhiteSpace(request.EventKey) ||
            !SafeUri(request.Uri) || request.Title.Length > 256 || request.Body.Length > 4096 || (request.TeamId == null) != (request.BasketId == null))
            throw new InvalidOperationException("Invalid scoped push event.");
        var key = Hash($"{request.GroupId}|{request.LeagueId}|{request.Year}|{request.Day}|{request.UserId}|{request.Kind}|{request.EventKey}");
        if (database.PushDeliveries.Local.Any(x => x.DeduplicationKey == key) || await database.PushDeliveries.AnyAsync(x => x.DeduplicationKey == key, cancellationToken)) return;
        database.PushDeliveries.Add(new() { Id = Guid.NewGuid(), DeduplicationKey = key, GroupId = request.GroupId, LeagueId = request.LeagueId,
            SeasonIndex = request.Year, Day = request.Day, UserId = request.UserId, TeamId = request.TeamId, BasketId = request.BasketId, Kind = request.Kind,
            Title = request.Title, Body = request.Body, Uri = request.Uri, Tag = "push-" + key, ExpiresAtUtc = request.ExpiresAtUtc,
            NextAttemptAtUtc = clock.GetUtcNow() });
    }
    public async Task<QuarantinedPushDto[]> QuarantineAsync(ClaimsPrincipal caller, CancellationToken cancellationToken = default)
    {
        await GlobalAdminAsync(caller, cancellationToken);
        return await database.PushDeliveries.AsNoTracking().Where(x => x.Status == PushDeliveryStatus.Quarantined).OrderBy(x => x.Id).Take(200)
            .Select(x => new QuarantinedPushDto(x.Id, x.SeasonIndex, x.Day, x.UserId, x.Kind, x.QuarantineReason, x.Attempts, x.ExpiresAtUtc)).ToArrayAsync(cancellationToken);
    }
    public async Task ConfirmScopeAsync(ClaimsPrincipal caller, Guid id, ConfirmPushScopeRequest request, CancellationToken cancellationToken = default)
    {
        if (!SafeUri(request.Uri)) throw new ApplicationFault(400, "invalid_push_link", "Usare un percorso locale senza credenziali.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var actor = await GlobalAdminAsync(caller, cancellationToken);
        var delivery = await database.PushDeliveries.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApplicationFault(404, "push_delivery_not_found", "Consegna non trovata.");
        if (delivery.Status != PushDeliveryStatus.Quarantined) throw new ApplicationFault(409, "push_delivery_not_quarantined", "Consegna già riconciliata.");
        if (!await database.LeagueSeasons.AnyAsync(x => x.GroupId == request.GroupId && x.LeagueId == request.LeagueId && x.SeasonIndex == delivery.SeasonIndex, cancellationToken))
            throw new ApplicationFault(400, "invalid_push_scope", "La lega non appartiene al gruppo e alla stagione richiesti.");
        delivery.GroupId = request.GroupId; delivery.LeagueId = request.LeagueId; delivery.Uri = request.Uri;
        if (!await PushDeliveryAccess.IsCurrentOwnerAsync(database, delivery, cancellationToken)) throw new ApplicationFault(409, "push_recipient_not_authorized", "Il destinatario non è proprietario attivo nella lega selezionata.");
        delivery.Status = PushDeliveryStatus.Pending; delivery.QuarantineReason = null;
        database.PushDeliveryScopeConfirmations.Add(new() { DeliveryId = id, ActorUserId = actor.Id, GroupId = request.GroupId, LeagueId = request.LeagueId,
            SeasonIndex = delivery.SeasonIndex, Uri = request.Uri, RecordedAtUtc = clock.GetUtcNow() });
        // Preserve original attempts, expiry, browser tag and endpoint receipts.
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private async Task<AccountIdentityDto> GlobalAdminAsync(ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var actor = await accounts.CurrentAsync(caller, cancellationToken);
        if (!caller.IsInRole("super_admin") || !accounts.IsGlobalAdministrator(actor.Email)) throw new ApplicationFault(403, "push_global_admin_required", "Serve un amministratore globale per riconciliare le consegne legacy.");
        return actor;
    }
    public static bool SafeUri(string? uri) => uri is { Length: > 0 and <= 2048 } && uri[0] == '/' && !uri.StartsWith("//", StringComparison.Ordinal) &&
        !uri.Contains('\\') && !uri.Any(char.IsControl) && !uri.Contains('%');
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal static class PushDeliveryAccess
{
    public static Task<bool> IsCurrentOwnerAsync(FantasoccerDbContext database, PushDelivery delivery, CancellationToken cancellationToken) =>
        database.TeamSeasons.AnyAsync(team => team.GroupId == delivery.GroupId && team.SeasonIndex == delivery.SeasonIndex && !team.IsArchived &&
            (delivery.TeamId == null || team.Id == delivery.TeamId && team.BasketId == delivery.BasketId) &&
            team.Owners.Any(owner => owner.UserId == delivery.UserId && owner.Membership.Roles != GroupRole.None && owner.Membership.User.IsEnabled) &&
            database.LeagueBasketSeasons.Any(link => link.GroupId == team.GroupId && link.LeagueId == delivery.LeagueId && link.SeasonIndex == team.SeasonIndex && link.BasketId == team.BasketId) &&
            database.LeagueSeasons.Any(league => league.GroupId == team.GroupId && league.LeagueId == delivery.LeagueId && league.SeasonIndex == team.SeasonIndex && !league.IsArchived && !league.League.IsArchived), cancellationToken);

    public static bool IsEnabled(UserPushPreferences? settings, int kind) => kind switch
    {
        0 => settings?.MatchEvents ?? true, 1 => settings?.OpponentMatchEvents ?? false, 2 => settings?.DeploymentReminder ?? false,
        3 => settings?.EndDay ?? false, 4 => settings?.MarketEvents ?? false, 5 => settings?.LiveChangesReminder ?? false,
        6 => settings?.LiveFormationChangeEvents ?? false, _ => false
    };
}
