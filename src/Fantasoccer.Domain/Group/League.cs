namespace Fantasoccer
{
    public class League
    {
        public string Name { get; set; }
        public bool IsMain { get; set; }
        public int Participant { get; set; }
        public List<AnnualLeague> Years { get; set; }
        public List<UserOfAGroup> ActiveUsers { get; set; }
        public List<UserOfAGroup> RemovedUsers { get; set; }
    }
}
