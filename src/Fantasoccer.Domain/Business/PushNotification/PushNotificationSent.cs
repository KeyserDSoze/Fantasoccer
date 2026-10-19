namespace Fantasoccer
{
    public sealed class PushNotificationSent
    {
        public bool DeploymentReminder { get; set; }
        public bool EndDay { get; set; }
        public bool LiveChangesReminder { get; set; }
        public List<string> LiveFormationChangeNotifications { get; set; } = [];
        // Checkpoint and durable deliveries are committed in the same document.
        public VotedRealPlayerWrapper? MatchEventVotes { get; set; }
        public long MatchEventSequence { get; set; }
        public List<PendingPushNotification> PendingNotifications { get; set; } = [];
        // Used only by the private index document (year/day 0/0), not by days.
        public List<YearDayKey> ActiveOutboxDays { get; set; } = [];
    }
}
