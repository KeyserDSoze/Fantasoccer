namespace Fantasoccer
{
    public static class Field
    {
        public const int Span = 90;
        public const int Max = 410;
        public static readonly FieldRange GoalKeeperLine = new(0, 90);
        public static readonly FieldRange DefensorLine = new(91, 210);
        public static readonly FieldRange MidfielderLine = new(211, 320);
        public static readonly FieldRange ForwardLine = new(321, 410);
        public static readonly FieldRange InField = new(0, 500);
        public static readonly FieldRange FirstBackup = new(620, 700);
        public static readonly FieldRange SecondBackup = new(701, 800);
        public static readonly FieldRange GoalKeeperBackup = new(0, 40);
        public static readonly FieldRange DefensorBackup = new(125, 165);
        public static readonly FieldRange MidfielderBackup = new(250, 290);
        public static readonly FieldRange ForwardBackup = new(375, 415);
        public static readonly FieldRange TribuneY = new(1000, 1600);
        public static readonly FieldRange TribuneX = new(0, 1000);
        public static readonly FieldRange Invasion = new(416, 1000);
    }
}