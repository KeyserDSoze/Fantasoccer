namespace Fantasoccer
{
    public record VoteValue
    {
        public float Value { get; set; } = -1000;
        public bool Special { get; set; }
        public static VoteValue Empty => new();
        public bool IsEmpty => Value == -1000;
        public bool IsZero => Value == 0;
    }
}
