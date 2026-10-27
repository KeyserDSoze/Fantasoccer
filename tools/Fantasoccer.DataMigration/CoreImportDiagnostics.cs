using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fantasoccer.DataMigration;

// Only controlled reason labels, checksums, hashed archive paths and comparisons.
// Never serialize account objects, rejected values or original storage keys here.
public sealed record IdentityConflictDetails(string Kind, string? OtherDocumentSha256 = null,
    string? CurrentArchiveReference = null, string? OtherArchiveReference = null,
    bool? ProfilesIdentical = null, bool? DisplayNameMatches = null, bool? SelectionMatches = null,
    bool? GroupRolesMatch = null, bool? GroupLabelsMatch = null,
    bool? CurrentBlobIsEmailLookup = null, bool? OtherBlobIsEmailLookup = null);

public static class CoreImportDiagnostics
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static string Format(CoreImportException fault) => $"Core import refused: {fault.Message}."
        + (fault.Field is null ? "" : $" Field: {fault.Field}; reader: {fault.Reader}.")
        + (fault.DocumentSha256 is null ? "" : $" Document SHA-256: {fault.DocumentSha256}.")
        + (fault.IdentityConflict is null ? "" : " Identity details: " + JsonSerializer.Serialize(fault.IdentityConflict, Json))
        + (fault.PlayerValidation is null ? "" : " Player details: " + JsonSerializer.Serialize(fault.PlayerValidation, Json))
        + (fault.ClubValidation is null ? "" : " Club details: " + JsonSerializer.Serialize(fault.ClubValidation, Json))
        + (fault.VoteValidation is null ? "" : " Vote details: " + JsonSerializer.Serialize(fault.VoteValidation, Json))
        + " No partial transaction was committed.";
}
