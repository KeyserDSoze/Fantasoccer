using System.Text.Json;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadPushPreferences(VerifiedJsonDataset dataset, CorePlan plan, Func<string, string, UserAccount> addUser)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var document in dataset.Documents.Where(x => x.Container == "pushnotification"))
        {
            var entity = document.Envelope.Deserialize<Entity<PushNotificationSettings, string>>(options) ?? throw new CoreImportException("invalid_push_preferences");
            var email = Email(entity.Key);
            var user = addUser(email, email);
            var source = entity.Value ?? throw new CoreImportException("missing_push_preferences");
            if (plan.PushPreferences.Any(x => x.UserId == user.Id)) throw new CoreImportException("push_preferences_collision");
            var preferences = new UserPushPreferences { UserId = user.Id, MatchEvents = source.MatchEvents, OpponentMatchEvents = source.OpponentMatchEvents,
                DeploymentReminder = source.DeploymentReminder, EndDay = source.EndDay, MarketEvents = source.MarketEvents,
                LiveChangesReminder = source.LiveChangesReminder, LiveFormationChangeEvents = source.LiveFormationChangeEvents };
            foreach (var (subscription, ordinal) in (source.Subscriptions ?? []).Select((subscription, ordinal) => (subscription, ordinal)))
            {
                if (subscription is null) throw new CoreImportException("missing_push_subscription");
                var valid = PushSubscriptionValidator.IsValid(subscription);
                preferences.LegacySubscriptions.Add(new() { Ordinal = ordinal, Endpoint = subscription.Endpoint, P256dh = subscription.P256DH, Auth = subscription.Auth, IsValid = valid });
                if (string.IsNullOrEmpty(subscription.Endpoint)) continue;
                var hash = PushEndpointIdentity.Hash(subscription.Endpoint);
                var duplicate = plan.PushDevices.SingleOrDefault(x => x.EndpointHash == hash);
                if (duplicate != null)
                {
                    if (duplicate.UserId != user.Id) throw new CoreImportException("push_endpoint_owner_collision");
                    continue; // Preserve every source row above; first endpoint wins as in the legacy sender.
                }
                plan.PushDevices.Add(new() { EndpointHash = hash, UserId = user.Id, Endpoint = subscription.Endpoint,
                    P256dh = subscription.P256DH ?? "", Auth = subscription.Auth ?? "", IsValid = valid });
            }
            plan.PushPreferences.Add(preferences);
        }
    }
}
