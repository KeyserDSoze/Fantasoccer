namespace FantacalcioV4
{
    public record FieldRange(int Start, int End)
    {
        public bool IsValid(int value)
            => value >= this.Start && value <= this.End;
    }
}