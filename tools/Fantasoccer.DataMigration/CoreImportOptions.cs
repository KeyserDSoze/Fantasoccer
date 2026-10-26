namespace Fantasoccer.DataMigration;

public sealed record CoreImportOptions(string? LegacyEncryptionKey = null, string? LegacyEncryptionIV = null,
    bool ImportLegacyAuctions = true, bool ImportLegacyLogs = false,
    IReadOnlyList<IdentityDocumentResolution>? IdentityResolutions = null, bool ImportLegacyNamedOwners = false,
    IReadOnlyList<PlayerCatalogResolution>? PlayerCatalogResolutions = null);
