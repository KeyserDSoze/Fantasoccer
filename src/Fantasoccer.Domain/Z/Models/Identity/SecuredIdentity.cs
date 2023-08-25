using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public record SecuredToken(DateTime ExpiringTime, string Username, string Random, bool IsAdmin, bool IsSuperAdmin)
    {
        [JsonIgnore]
        public bool IsValid => FantacalcioTime.ItalyNow < ExpiringTime;
    }
    public record SecuredIdentity(string Username, string Name, string Surname, string Email, bool IsActive, bool IsAdmin, bool IsSuperAdmin, List<TeamByGroup> Groups, Settings Settings)
    {
        public string Token { get; } = new SecuredToken(FantacalcioTime.ItalyNow.AddMonths(5), Username, Alea.GetTimedKey(), IsAdmin, IsSuperAdmin).Encrypt();
        public static implicit operator SecuredIdentity(Identity identity)
            => new(identity.Username, identity.Name, identity.Surname, identity.Email, identity.IsActive, identity.IsAdmin, identity.IsSuperAdmin, identity.Groups, identity.Settings);
    }
}
