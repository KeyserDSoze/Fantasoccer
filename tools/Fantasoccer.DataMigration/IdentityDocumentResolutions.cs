using System.Text.Json;
using System.Text.Json.Serialization;
using Fantasoccer;
using Fantasoccer.StorageMigration;

namespace Fantasoccer.DataMigration;

public sealed record IdentityDocumentResolution(string PreferredDocumentSha256, string OtherDocumentSha256);
public sealed record AppliedIdentityResolution(string PreferredDocumentSha256, string OtherDocumentSha256,
    string PreferredArchiveReference, string OtherArchiveReference);

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
            if (rule is null || !Hash(rule.PreferredDocumentSha256) || !Hash(rule.OtherDocumentSha256)) throw Invalid();
            var preferred = rule.PreferredDocumentSha256.ToLowerInvariant();
            var other = rule.OtherDocumentSha256.ToLowerInvariant();
            if (!used.Add(preferred) || !used.Add(other)) throw Invalid();
            result.Add(new(preferred, other));
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
        foreach (var rule in rules)
        {
            if (!byHash.TryGetValue(rule.PreferredDocumentSha256, out var preferredMatches) ||
                !byHash.TryGetValue(rule.OtherDocumentSha256, out var otherMatches))
                throw new CoreImportException("identity_resolution_document_not_found");
            if (preferredMatches.Length != 1 || otherMatches.Length != 1)
                throw new CoreImportException("identity_resolution_ambiguous");
            var preferred = preferredMatches[0]; var other = otherMatches[0];
            if (preferred.Email != other.Email) throw new CoreImportException("identity_resolution_email_mismatch");
            if (byEmail[preferred.Email].Length != 2) throw new CoreImportException("identity_resolution_ambiguous");
            // This policy is deliberately narrow: one email lookup and one
            // technical-key alias. It cannot reverse the decision, silently
            // absorb a third copy, or override contradictory email metadata.
            if (!IsEmailLookup(preferred) || IsEmailLookup(other))
                throw new CoreImportException("identity_resolution_lookup_required");
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
