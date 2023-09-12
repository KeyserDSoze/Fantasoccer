namespace FantacalcioV4
{
    public record Chance(bool Fantagazzetta, bool Gazzetta, bool Mediaset, bool Sky, ChanceType Status)
    {
        public static Chance Default() => new(false, false, false, false, ChanceType.Normal);
    }
}