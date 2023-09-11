namespace Fantasoccer
{
    public sealed class GroupForIdentity
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required IdentityRole Role { get; set; }
    }
}
