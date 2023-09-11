namespace Fantasoccer
{
    public class AnnualTeam
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public List<string> AdditionalOwners { get; set; }
        public bool IsOwner(string email)
            => email != null && (Owner == email || AdditionalOwners?.Contains(email) == true);
    }
}
