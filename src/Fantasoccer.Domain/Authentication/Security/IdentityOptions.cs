namespace Fantasoccer.Domain
{
    public sealed class IdentityOptions
    {
        public int ExpiringInMinutes { get; set; }
        public string Instance { get; set; }
        public string Domain { get; set; }
        public string ClientId { get; set; }
        public string Audience { get; set; }
        public string Policy { get; set; }
    }
}
