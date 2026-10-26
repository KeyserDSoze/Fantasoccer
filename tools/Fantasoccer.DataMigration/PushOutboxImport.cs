using System.Globalization;
using System.Text.Json;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadPushOutbox(VerifiedJsonDataset dataset, CorePlan plan, Func<string, string, Guid> map,
        Func<RealPlayer, int, bool, PlayerSeason> player, Func<RealTeam?, Guid> club, Func<string, string, UserAccount> addUser, Action<int> season)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var keys = new HashSet<(int, int)>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "pushnotificationsent"))
        {
            var entity = document.Envelope.Deserialize<Entity<PushNotificationSent, YearDayKey>>(json) ?? throw new CoreImportException("invalid_push_outbox");
            var key = entity.Key ?? throw new CoreImportException("missing_push_outbox_key");
            var value = entity.Value ?? throw new CoreImportException("missing_push_outbox");
            if (!keys.Add((key.Year, key.Day)) || key.Day is < 0 or > 38 || key.Year < 0 || (key.Year == 0 && key.Day != 0)) throw new CoreImportException("invalid_push_outbox_scope");
            map("push-day", Composite(key.Year.ToString(CultureInfo.InvariantCulture), key.Day.ToString(CultureInfo.InvariantCulture)));
            if (key.Year == 0)
            {
                if (value.PendingNotifications.Count > 0 || value.MatchEventVotes != null || value.MatchEventSequence != 0 || value.DeploymentReminder || value.EndDay || value.LiveChangesReminder || value.LiveFormationChangeNotifications.Count > 0)
                    throw new CoreImportException("unexpected_push_index_state");
                foreach (var active in value.ActiveOutboxDays)
                {
                    season(active.Year);
                    if (active.Day is < 0 or > 38) throw new CoreImportException("invalid_push_active_day");
                    plan.PushActiveDays.Add(new() { Ordinal = plan.PushActiveDays.Count, SeasonIndex = active.Year, Day = active.Day });
                }
                continue;
            }
            season(key.Year);
            if (value.ActiveOutboxDays.Count > 0) throw new CoreImportException("unexpected_push_day_index");
            var checkpoint = new PushDayCheckpoint { SeasonIndex = key.Year, Day = key.Day, DeploymentReminder = value.DeploymentReminder, EndDay = value.EndDay,
                LiveChangesReminder = value.LiveChangesReminder, HasMatchEventVotes = value.MatchEventVotes != null, MatchEventSequence = value.MatchEventSequence,
                FormationReceipts = value.LiveFormationChangeNotifications.Select((receipt, ordinal) => new LegacyFormationPushReceipt { Receipt = receipt, Ordinal = ordinal }).ToArray() };
            foreach (var sample in value.MatchEventVotes?.Players ?? [])
            {
                var identity = player(sample, key.Year, false);
                var vote = sample.Vote ?? throw new CoreImportException("missing_push_baseline_vote");
                if (!float.IsFinite(vote.Value)) throw new CoreImportException("invalid_push_baseline_vote");
                checkpoint.Votes.Add(new() { Ordinal = checkpoint.Votes.Count, PlayerId = identity.PlayerId, ClubId = club(sample.RealTeam), PlayerRole = (int)sample.Role,
                    IsActive = sample.IsActive, IsVisible = sample.Visible, VoteRole = (int)vote.Role, Value = vote.Value, IsFinal = vote.IsFinal, Goals = vote.Goal, Penalties = vote.Penalty,
                    Assists = vote.Assist, StoppedPenalties = vote.StoppedPenalty, SufferedGoals = vote.SufferedGoal, WrongedPenalties = vote.WrongedPenalty, OwnGoals = vote.OwnGoal,
                    Status = (int)vote.Status, ManOfTheMatch = vote.ManOfTheMatch, HasVote = vote.HasVote, IsOut = vote.IsOut, IsIn = vote.IsIn, Injured = vote.Injured });
            }
            plan.PushCheckpoints.Add(checkpoint);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pending in value.PendingNotifications)
            {
                if (string.IsNullOrEmpty(pending.Id) || !ids.Add(pending.Id) || !Enum.IsDefined(pending.Kind) || pending.Attempts < 0) throw new CoreImportException("invalid_pending_push");
                var notification = pending.Notification ?? throw new CoreImportException("missing_pending_push_payload");
                var user = addUser(Email(pending.Email), pending.Email);
                var legacyKey = Composite(key.Year.ToString(CultureInfo.InvariantCulture), key.Day.ToString(CultureInfo.InvariantCulture), pending.Id);
                var delivery = new PushDelivery { Id = map("push-delivery", legacyKey), DeduplicationKey = "legacy-" + PushEndpointIdentity.Hash(legacyKey), LegacyId = pending.Id,
                    SeasonIndex = key.Year, Day = key.Day, UserId = user.Id, Kind = (int)pending.Kind, Title = notification.Title, Body = notification.Body,
                    HasData = notification.Data != null, Uri = notification.Data?.Uri, LegacyRecipientEmail = notification.Data?.RecipientEmail,
                    Icon = notification.Icon, Badge = notification.Badge, Sound = notification.Sound, Tag = notification.Tag, RequireInteraction = notification.RequireInteraction,
                    Attempts = pending.Attempts, NextAttemptAtUtc = pending.NextAttemptAt.ToUniversalTime(), ExpiresAtUtc = pending.ExpiresAt.ToUniversalTime(),
                    Status = PushDeliveryStatus.Quarantined, QuarantineReason = "legacy_scope_unverified" };
                foreach (var endpoint in pending.CompletedEndpoints)
                    delivery.Receipts.Add(new() { Ordinal = delivery.Receipts.Count, EndpointHash = PushEndpointIdentity.Hash(endpoint), LegacyEndpoint = endpoint, Outcome = "legacy_completed" });
                plan.PushDeliveries.Add(delivery);
            }
        }
    }
}
