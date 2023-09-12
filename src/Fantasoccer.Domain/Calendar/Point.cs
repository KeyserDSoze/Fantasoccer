using System.Text.Json.Serialization;

namespace Fantasoccer
{
    public class Point
    {
        [JsonPropertyName("v")]
        public float Value { get; }
        [JsonPropertyName("d")]
        public bool DefensiveBonus { get; }
        [JsonPropertyName("o")]
        public bool OwnGoal { get; }
        public Point(float value, bool defensiveBonus, bool ownGoal)
        {
            Value = value;
            DefensiveBonus = defensiveBonus;
            OwnGoal = ownGoal;
        }
        public static Point Zero => new(0, false, false);
    }
}
