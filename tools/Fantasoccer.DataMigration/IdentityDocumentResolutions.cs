using System.Text.Json;
using System.Text.Json.Serialization;
using Fantasoccer;
using Fantasoccer.StorageMigration;

namespace Fantasoccer.DataMigration;

// Either pinned to the exact documents (two SHA-256) or approved for one email: the email
// form survives later profile changes (a login rewrites the document) but never merges data
// and still needs exactly two copies of that email, one of them being the email lookup.
public sealed record IdentityDocumentResolution(string? PreferredDocumentSha256 = null, string? OtherDocumentSha256 = null,
    bool AllowEmailLookupAlias = false, string? Email = null);
public sealed record AppliedIdentityResolution(string PreferredDocumentSha256, string OtherDocumentSha256,
    string PreferredArchiveReference, string OtherArchiveReference, bool EmailLookupAlias = false);

public static class IdentityResolutionConfiguration
{
    public static IReadOnlyList<IdentityDocumentResolution> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        if (json.Length > 32768) throw Invalid();
        try
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
            return Normalize(JsonSerializer.Deserialize<IdentityDocumentResolution[]>(json, options) ?? throw Invalid());
        }
        catch (JsonException) { throw Invalid(); }
    }

    public static IReadOnlyList<IdentityDocumentResolution> Normalize(IReadOnlyList<IdentityDocumentResolution>? rules)
    {
        if (rules is null) return [];
        if (rules.Count > 100) throw Invalid();
        var result = new List<IdentityDocumentResolution>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (rule is null) throw Invalid();
            if (rule.Email is not null)
            {
                if (rule.PreferredDocumentSha256 is not null || rule.OtherDocumentSha256 is not null) throw Invalid();
                string canonical;
                canonical = CoreDatasetImporter.CanonicalEmail(rule.Email);
                if (!used.Add("email:" + canonical)) throw Invalid();
                result.Add(new(null, null, rule.AllowEmailLookupAlias, canonical));
                continue;
            }
            if (!Hash(rule.PreferredDocumentSha256) || !Hash(rule.OtherDocumentSha256)) throw Invalid();
            var preferred = rule.PreferredDocumentSha256!.ToLowerInvariant();
            var other = rule.OtherDocumentSha256!.ToLowerInvariant();
            if (!used.Add(preferred) || !used.Add(other)) throw Invalid();
            result.Add(new(preferred, other, rule.AllowEmailLookupAlias));
        }
        return result.AsReadOnly();
    }

    private static bool Hash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    private static CoreImportException Invalid() => new("invalid_identity_resolution_config");
}

public sealed partial class CoreDatasetImporter
{
    private sealed record IdentityCandidate(LegacyJsonDocument Document, AppIdentity Account, string Email, bool KeyReconciled);
    private sealed record IdentitySelection(IdentityCandidate[] Selected, (IdentityCandidate Preferred, IdentityCandidate Other)[] Resolved);

    private static IdentitySelection SelectIdentityCandidates(IReadOnlyList<IdentityCandidate> candidates,
        IReadOnlyList<IdentityDocumentResolution>? configuredRules)
    {
        var rules = IdentityResolutionConfiguration.Normalize(configuredRules);
        var byEmail = candidates.GroupBy(x => x.Email, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        var byHash = candidates.GroupBy(x => x.Document.Sha256, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        var resolvedByEmail = new Dictionary<string, (IdentityCandidate Preferred, IdentityCandidate Other)>(StringComparer.Ordinal);
        // Approvals given by email become the same pair of documents as a pinned approval, chosen from the
        // current dump: the copy named like the email is preferred. A duplicate that no longer exists is ignored.
        var pinned = new List<IdentityDocumentResolution>();
        foreach (var rule in rules)
        {
            if (rule.Email is null) { pinned.Add(rule); continue; }
            if (!byEmail.TryGetValue(rule.Email, out var copies) || copies.Length == 1) continue;
            if (copies.Length != 2) throw new CoreImportException("identity_resolution_ambiguous");
            var lookups = copies.Where(IsEmailLookup).ToArray();
            var preferred = rule.AllowEmailLookupAlias
                ? copies.SingleOrDefault(x => string.Equals(x.Document.BlobName, rule.Email, StringComparison.Ordinal))
                : lookups.Length == 1 ? lookups[0] : null;
            if (preferred is null) throw new CoreImportException(rule.AllowEmailLookupAlias ? "identity_resolution_email_alias_required" : "identity_resolution_lookup_required");
            var other = copies.Single(x => !ReferenceEquals(x, preferred));
            pinned.Add(new(preferred.Document.Sha256, other.Document.Sha256, rule.AllowEmailLookupAlias));
        }
        foreach (var rule in pinned)
        {
            if (!byHash.TryGetValue(rule.PreferredDocumentSha256!, out var preferredMatches) ||
                !byHash.TryGetValue(rule.OtherDocumentSha256!, out var otherMatches))
                throw new CoreImportException("identity_resolution_document_not_found");
            if (preferredMatches.Length != 1 || otherMatches.Length != 1)
                throw new CoreImportException("identity_resolution_ambiguous");
            var preferred = preferredMatches[0]; var other = otherMatches[0];
            if (preferred.Email != other.Email) throw new CoreImportException("identity_resolution_email_mismatch");
            if (byEmail[preferred.Email].Length != 2) throw new CoreImportException("identity_resolution_ambiguous");
            // The default only handles one email lookup and one technical-key
            // alias. A separate operator opt-in handles case-only email aliases;
            // neither policy absorbs a third copy or contradictory metadata.
            if (!IsEmailLookup(preferred))
                throw new CoreImportException("identity_resolution_lookup_required");
            if (rule.AllowEmailLookupAlias)
            {
                if (!IsEmailLookup(other) ||
                    !string.Equals(preferred.Document.BlobName, other.Document.BlobName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(preferred.Document.BlobName, other.Document.BlobName, StringComparison.Ordinal))
                    throw new CoreImportException("identity_resolution_email_alias_required");
            }
            else if (IsEmailLookup(other)) throw new CoreImportException("identity_resolution_lookup_required");
            if (!resolvedByEmail.TryAdd(preferred.Email, (preferred, other)))
                throw new CoreImportException("identity_resolution_ambiguous");
        }

        var selected = new List<IdentityCandidate>();
        foreach (var group in byEmail.Values)
        {
            if (group.Length == 1) { selected.Add(group[0]); continue; }
            if (resolvedByEmail.TryGetValue(group[0].Email, out var resolution)) { selected.Add(resolution.Preferred); continue; }
            throw new CoreImportException("identity_collision", documentSha256: group[1].Document.Sha256,
                identityConflict: CompareIdentityDocuments(group[1].Document, group[1].Account, group[0].Document, group[0].Account, group[0].Email));
        }
        return new(selected.ToArray(), resolvedByEmail.Values.ToArray());

        static bool IsEmailLookup(IdentityCandidate candidate) => TryEmail(candidate.Document.BlobName, out var email) && email == candidate.Email;
    }
}
