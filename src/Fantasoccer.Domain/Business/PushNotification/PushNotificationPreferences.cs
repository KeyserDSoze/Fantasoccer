namespace Fantasoccer;

public sealed class PushNotificationPreferences
{
    public bool? MatchEvents { get; set; }
    public bool? OpponentMatchEvents { get; set; }
    public bool? DeploymentReminder { get; set; }
    public bool? EndDay { get; set; }
    public bool? MarketEvents { get; set; }
    public bool? LiveChangesReminder { get; set; }
    public bool? LiveFormationChangeEvents { get; set; }
}

public enum PushNotificationKind
{
    MatchEvents, OpponentMatchEvents, DeploymentReminder, EndDay,
    MarketEvents, LiveChangesReminder, LiveFormationChangeEvents
}
