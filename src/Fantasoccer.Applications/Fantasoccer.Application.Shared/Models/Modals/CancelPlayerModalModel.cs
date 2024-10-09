namespace Fantasoccer.Application.Shared.Models
{
    public sealed class CancelPlayerModalModel
    {
        public Player Player { get; set; }
        public int Price { get; set; }
        public string Owner { get; set; }
        public PlayerInTeamStatus Status { get; set; }
    }
}
