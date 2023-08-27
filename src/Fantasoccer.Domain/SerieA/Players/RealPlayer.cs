namespace Fantasoccer
{
    public class RealPlayer
    {
        public string Name { get; set; }
        public string RealTeam { get; set; }
        public Role Role { get; set; }
        public int Year { get; set; }
        public bool IsActive { get; set; }
        public string Key => KeyUtils.GetPlayerKey(Name);
    }
}
