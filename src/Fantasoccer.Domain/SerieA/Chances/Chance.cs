namespace Fantasoccer
{
    public class Chance
    {
        public bool Fantagazzetta { get; set; }
        public bool Gazzetta { get; set; }
        public bool Mediaset { get; set; }
        public bool Sky { get; set; }
        public ChanceType Status { get; set; }
        public static Chance Default() => new()
        {
            Status = ChanceType.Normal,
            Fantagazzetta = false,
            Gazzetta = false,
            Mediaset = false,
            Sky = false
        };
    }
}
