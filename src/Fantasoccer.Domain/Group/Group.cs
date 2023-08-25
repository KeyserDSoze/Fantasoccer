namespace Fantasoccer
{
    public class Group
    {
        public string Name { get; set; }
        public List<League> Leagues { get; set; }
        public List<UserOfAGroup> ActiveUsers { get; set; }
        public List<UserOfAGroup> RemovedUsers { get; set; }
    }
}
