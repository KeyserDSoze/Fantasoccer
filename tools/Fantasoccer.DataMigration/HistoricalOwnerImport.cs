using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static string OwnerKey(CorePlan plan, Guid groupId, string? value)
    {
        if (TryEmail(value, out var email)) return email;
        // Only explicitly enabled non-email labels are participant identifiers.
        // A malformed email, empty value or oversized/control payload is still an
        // error. Exact case/spacing and group scope prevent accidental merging.
        if (!plan.ImportLegacyNamedOwners || string.IsNullOrWhiteSpace(value) || value.Length > 200 ||
            value.Contains('@') || value.Any(char.IsControl))
            throw new CoreImportException("invalid_email", "historicalOwner");
        return HistoricalParticipantIdentity.Key(groupId, value);
    }

    private static UserAccount ExistingOwner(CorePlan plan, Guid groupId, string value, string missingCode)
        => plan.Users.GetValueOrDefault(OwnerKey(plan, groupId, value)) ?? throw new CoreImportException(missingCode);
}
