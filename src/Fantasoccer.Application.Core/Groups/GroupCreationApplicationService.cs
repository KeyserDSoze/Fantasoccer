using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fantasoccer.Application.Core.Groups;

public sealed record RedeemGroupCodeRequest(string Name, string Code);
public sealed record IssueGroupCodeRequest(string? Label);
public sealed record GroupCodeDto(Guid Id, string Label, DateTimeOffset CreatedAtUtc, Guid CreatedByUserId,
    string Status, DateTimeOffset? RedeemedAtUtc, Guid? RedeemedByUserId, Guid? RedeemedGroupId);
public sealed record IssuedGroupCodeDto(string Code, GroupCodeDto Details);

public sealed class GroupCreationApplicationService(FantasoccerDbContext database, AccountApplicationService accounts, TimeProvider clock)
{
    public async Task<IssuedGroupCodeDto> IssueAsync(ClaimsPrincipal caller, IssueGroupCodeRequest request, CancellationToken cancellationToken = default)
    {
        var label = request.Label?.Trim() ?? "";
        if (label.Length > 200) throw new ApplicationFault(400, "invalid_code_label", "La nota deve contenere al massimo 200 caratteri.");
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var actor = await accounts.RequireGlobalAdministratorAsync(caller, cancellationToken);
        var code = "FZ-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var row = new GroupCreationCode { CodeHash = Hash(code), Label = label, CreatedByUserId = actor.Id, CreatedAtUtc = clock.GetUtcNow() };
        database.GroupCreationCodes.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(code, Project(row));
    }

    public async Task RevokeAsync(ClaimsPrincipal caller, Guid id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        await accounts.RequireGlobalAdministratorAsync(caller, cancellationToken);
        var row = await database.GroupCreationCodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApplicationFault(404, "group_code_not_found", "Codice non trovato.");
        if (row.RedeemedAtUtc != null) throw new ApplicationFault(409, "group_code_already_used", "Un codice già usato non può essere revocato o riattivato.");
        row.RevokedAtUtc ??= clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<GroupSummaryDto> RedeemAsync(ClaimsPrincipal caller, RedeemGroupCodeRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim();
        if (name is not { Length: >= 2 and <= 200 }) throw new ApplicationFault(400, "invalid_group_name", "Il nome deve contenere fra 2 e 200 caratteri.");
        var code = request.Code?.Trim().ToUpperInvariant();
        if (code is not { Length: 51 } || !code.StartsWith("FZ-", StringComparison.Ordinal) || code.AsSpan(3).ContainsAnyExcept("0123456789ABCDEF")) throw Unavailable();
        var digest = Hash(code);
        // The same write lock covers account status, code use, new group and
        // owner membership. Concurrent redemption cannot create a second group;
        // any failure rolls back the consumption along with the group.
        await using var transaction = await CoreMigrationLock.BeginInteractiveWriteAsync(database, cancellationToken);
        var actor = await accounts.CurrentAsync(caller, cancellationToken);
        var row = await database.GroupCreationCodes.SingleOrDefaultAsync(x => x.CodeHash == digest, cancellationToken);
        if (row == null || row.RedeemedAtUtc != null || row.RevokedAtUtc != null) throw Unavailable();
        var group = new FantasyGroup { Id = Guid.NewGuid(), Name = name };
        const GroupRole roles = GroupRole.SuperAdmin | GroupRole.Admin;
        database.GroupMemberships.Add(new() { Group = group, UserId = actor.Id, Roles = roles });
        row.RedeemedAtUtc = clock.GetUtcNow(); row.RedeemedByUserId = actor.Id; row.RedeemedGroupId = group.Id;
        var selection = await database.AccountSelections.SingleOrDefaultAsync(x => x.UserId == actor.Id, cancellationToken);
        if (selection == null) database.AccountSelections.Add(selection = new() { UserId = actor.Id });
        selection.GroupId = group.Id; selection.LeagueId = null; selection.Year = null; selection.WorkingUserId = null;
        await database.UnresolvedAccountSelections.Where(x => x.UserId == actor.Id).ExecuteDeleteAsync(cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(group.Id, group.Name, (int)roles);
    }

    internal static GroupCodeDto Project(GroupCreationCode row) => new(row.Id, row.Label, row.CreatedAtUtc, row.CreatedByUserId,
        row.RedeemedAtUtc != null ? "Used" : row.RevokedAtUtc != null ? "Revoked" : "Available", row.RedeemedAtUtc, row.RedeemedByUserId, row.RedeemedGroupId);
    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
    private static ApplicationFault Unavailable() => new(409, "group_code_unavailable", "Codice non valido, già utilizzato o revocato. Richiedi un nuovo codice all'amministratore.");
}
