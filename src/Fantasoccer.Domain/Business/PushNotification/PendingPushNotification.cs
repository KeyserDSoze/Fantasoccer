namespace Fantasoccer;

public sealed class PendingPushNotification
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public PushNotificationKind Kind { get; set; }
    public Notification Notification { get; set; } = new();
    public List<string> CompletedEndpoints { get; set; } = [];
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class PushNotificationDeliveryResult
{
    public List<string> CompletedEndpoints { get; set; } = [];
    public List<string> RetryEndpoints { get; set; } = [];
    public bool IsComplete => RetryEndpoints.Count == 0;
}
