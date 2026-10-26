namespace Fantasoccer.Application.Core.Notifications;

public sealed record PushSubscriptionDto(string Endpoint, string P256dh, string Auth, long Version = 0, bool IsValid = true);
public sealed record PushSettingsDto(bool MatchEvents, bool OpponentMatchEvents, bool DeploymentReminder, bool EndDay, bool MarketEvents,
    bool LiveChangesReminder, bool LiveFormationChangeEvents, PushSubscriptionDto[] Subscriptions, int Version, string? PublicKey, bool DeliveryEnabled);
public sealed record PushPreferencesPatch(bool? MatchEvents = null, bool? OpponentMatchEvents = null, bool? DeploymentReminder = null, bool? EndDay = null,
    bool? MarketEvents = null, bool? LiveChangesReminder = null, bool? LiveFormationChangeEvents = null);
public sealed record RegisterNotificationDeviceRequest(PushSubscriptionDto Subscription, string? PreviousEndpoint = null, string? DeviceProof = null);
public sealed record RegisteredNotificationDeviceDto(PushSettingsDto Settings, string DeviceProof);
public sealed record RemoveNotificationDeviceRequest(string Endpoint);
