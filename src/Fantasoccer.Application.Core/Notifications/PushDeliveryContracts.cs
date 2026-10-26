namespace Fantasoccer.Application.Core.Notifications;

public sealed record PushPayloadData(string Uri, string RecipientEmail);
public sealed record PushPayload(string Title, string Body, PushPayloadData Data, string? Icon, string? Badge, string Tag, bool RequireInteraction);
public enum PushTransportOutcome { Delivered, Gone, Retry, Rejected }
public interface IPushDeliveryTransport
{
    Task<PushTransportOutcome> SendAsync(PushSubscriptionDto device, PushPayload payload, int timeToLiveSeconds, CancellationToken cancellationToken);
}
public sealed record QueuedPushRequest(string EventKey, Guid GroupId, Guid LeagueId, int Year, int Day, Guid UserId, int Kind,
    string Title, string Body, string Uri, DateTimeOffset ExpiresAtUtc, Guid? TeamId = null, Guid? BasketId = null);
public sealed record ConfirmPushScopeRequest(Guid GroupId, Guid LeagueId, string Uri);
public sealed record QuarantinedPushDto(Guid Id, int Year, int Day, Guid UserId, int Kind, string? Reason, int Attempts, DateTimeOffset ExpiresAtUtc);
public sealed record PushDispatchResult(int Claimed, int Completed, int Retried, int Suppressed);
