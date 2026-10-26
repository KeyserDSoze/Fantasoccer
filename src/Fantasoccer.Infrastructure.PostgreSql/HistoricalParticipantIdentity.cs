using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fantasoccer.Infrastructure.PostgreSql;

// A source participant key, never a login or an inferred email. Group scope and
// exact source spelling are part of the identity; case/spacing are not merged.
public static class HistoricalParticipantIdentity
{
    public const string Prefix = "historical-owner:";
    public static string Key(Guid groupId, string label) => Prefix + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { groupId.ToString("D"), label }))));
}
