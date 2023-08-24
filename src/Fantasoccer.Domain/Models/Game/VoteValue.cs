namespace Fantasoccer
{
    public record VoteValue(float Value, bool Special)
    {
        public static VoteValue Empty => new(-1000, false);
        public bool IsEmpty => Value == -1000;
        public bool IsZero => Value == 0;
    }
}