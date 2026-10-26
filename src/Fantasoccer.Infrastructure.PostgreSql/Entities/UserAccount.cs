namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class UserAccount
{
    public Guid Id { get; set; }
    // Historical participants are real users even when no login email is known.
    // Null is not an invitation or an authentication identity.
    public string? Email { get; set; }
    public required string DisplayName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsHistoricalPlaceholder { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string OwnerReference => Email ?? "user:" + Id.ToString("D");
    public static bool TryOwnerReference(string? value, out Guid id)
    {
        id = Guid.Empty;
        return value?.StartsWith("user:", StringComparison.Ordinal) == true && Guid.TryParseExact(value[5..], "D", out id);
    }
    public ICollection<GroupMembership> Memberships { get; set; } = [];
}
