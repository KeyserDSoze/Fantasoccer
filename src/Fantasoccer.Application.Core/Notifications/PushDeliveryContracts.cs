namespace Fantasoccer.Application.Core.Notifications;

public sealed record PushPayloadData(string Uri, string RecipientEmail);
public sealed record PushPayload(string Title, string Body, PushPayloadData Data, string? Icon, string? Badge, string Tag, bool RequireInteraction);
// ProviderAccepted = the Web Push service accepted the message (HTTP 201). It is
// not a delivery/read confirmation from the device. Receipts written before the
// rename stored "Delivered" with the same meaning (see PushOutcomes).
public enum PushTransportOutcome { ProviderAccepted, Gone, Retry, Rejected }
public static class PushOutcomes
{
    public static string Stored(PushTransportOutcome outcome) => outcome.ToString();
    public static bool IsProviderAccepted(string? stored) => stored is "ProviderAccepted" or "Delivered";
}
public interface IPushDeliveryTransport
{
    Task<PushTransportOutcome> SendAsync(PushSubscriptionDto device, PushPayload payload, int timeToLiveSeconds, CancellationToken cancellationToken);
}
public sealed record QueuedPushRequest(string EventKey, Guid GroupId, Guid LeagueId, int Year, int Day, Guid UserId, int Kind,
    string Title, string Body, string Uri, DateTimeOffset ExpiresAtUtc, Guid? TeamId = null, Guid? BasketId = null);
public sealed record ConfirmPushScopeRequest(Guid GroupId, Guid LeagueId, string Uri);
public sealed record QuarantinedPushDto(Guid Id, int Year, int Day, Guid UserId, int Kind, string? Reason, int Attempts, DateTimeOffset ExpiresAtUtc);
public sealed record PushDispatchResult(int Claimed, int Completed, int Retried, int Suppressed);
