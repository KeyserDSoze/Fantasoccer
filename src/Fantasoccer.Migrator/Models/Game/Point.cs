namespace FantacalcioV4
{
    public record Point(float Value, bool DefensiveBonus, bool OwnGoal)
    {
        public static Point Zero { get; } = new Point(0, false, false);
    }
}