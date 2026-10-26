using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed record CoreImportReport(int MappingVersion, int Documents, int SkippedDocuments, int Users, int Groups,
    int Teams, string[] PendingContainers, string[] PendingFields, bool FullMigrationComplete = false, string[]? PreservedArtifacts = null,
    string[]? ExcludedContainers = null, string[]? ReconciledIdentityDocuments = null);

public sealed class CoreImportException(string code, string? field = null, string? reader = null, string? documentSha256 = null) : Exception(code)
{
    // Source-code coordinates only; never the rejected email/payload/key.
    public string? Field { get; } = field;
    public string? Reader { get; } = reader;
    public string? DocumentSha256 { get; } = documentSha256;
}

public sealed partial class CoreDatasetImporter(FantasoccerDbContext database, CoreImportOptions? options = null)
{
    public const int MappingVersion = 24;
    private static readonly IReadOnlySet<string> LegacyAuctionContainers = new HashSet<string>(StringComparer.Ordinal) { "auction", "randomizedplayers" };
    public static readonly IReadOnlySet<string> Containers = new HashSet<string>(StringComparer.Ordinal)
        { "group", "appidentity", "settings", "team", "dailyteams", "openingteams", "openingdailyteams", "realplayerswrapper", "realteamwrapper", "realcalendar", "mcp-oauth",
            "live", "notification", "official", "chancedrealplayerwrapper", "statplayerswrapper", "realrank", "calendar", "rank", "dailyrank", "playedgame", "openingstandings", "randomizedplayers", "marketwrapper", "auction", "halloffame", "cards", "pushnotification", "pushnotificationsent", "news", "teamwithai" };
    public static readonly IReadOnlySet<string> Tables = new HashSet<string>(StringComparer.Ordinal) { "logs" };
    // These are intentionally not translated into live relational documents.
    // The verified export/restore retains every payload byte and manifest hash.
    public static readonly IReadOnlyDictionary<string, string> PreservedContainers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["images"] = "Blob files: retained in file storage; not SQL gameplay records",
        ["logos"] = "Blob files: retained with imported/versioned references",
        ["cardimages"] = "Blob files: retained with imported/versioned references",
        ["livegroup"] = "Private historical derived snapshots: archived in the verified dump; live reads use the SQL projection",
        ["mcp-keys"] = "Private cryptographic artifacts: preserve the original key ring separately; never publish or import as gameplay"
    };

    public static async Task<(VerifiedJsonDataset Dataset, CorePlan Plan)> AnalyzeAsync(string directory, CancellationToken cancellationToken = default, CoreImportOptions? options = null)
    {
        options ??= new();
        // Verify ALL original payload hashes, but do not decode/resolve the
        // deliberately excluded auction/log documents. They remain in the dump.
        var selected = options.ImportLegacyAuctions ? Containers : Containers.Except(LegacyAuctionContainers).ToHashSet(StringComparer.Ordinal);
        var dataset = await DumpArchive.ReadJsonDatasetAsync(directory, selected, cancellationToken, options.ImportLegacyLogs ? Tables : null);
        return (dataset, BuildPlan(dataset, options));
    }

    public async Task<CoreImportReport> ImportAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (database.ChangeTracker.HasChanges()) throw new CoreImportException("context_has_pending_changes");
        // Import owns its unit of execution. Refresh unchanged tracked rows before
        // taking locks; never discard pending writes from a caller's context.
        database.ChangeTracker.Clear();
        var (dataset, plan) = await AnalyzeAsync(directory, cancellationToken, options);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await CoreMigrationLock.AcquireAsync(database, cancellationToken);
        var state = await database.CoreMigrationStates.SingleOrDefaultAsync(cancellationToken);
        if (state?.InteractiveWritesStartedAtUtc is not null) throw new CoreImportException("interactive_writes_already_started");
        if (state?.SourceId is not null && state.SourceId != dataset.Manifest.SourceId) throw new CoreImportException("different_source");
        if (state is null)
        {
            if (await database.Users.AnyAsync(cancellationToken) || await database.Groups.AnyAsync(cancellationToken) ||
                await database.Players.AnyAsync(cancellationToken) || await database.Clubs.AnyAsync(cancellationToken) ||
                await database.FootballMatchdays.AnyAsync(cancellationToken) || await database.FootballDatasets.AnyAsync(cancellationToken) || await database.CardLibraries.AnyAsync(cancellationToken) ||
                await database.ArchivedOperationalLogs.AnyAsync(cancellationToken) || await database.NewsEditions.AnyAsync(cancellationToken) || await database.TeamAdvice.AnyAsync(cancellationToken))
                throw new CoreImportException("target_is_not_empty");
            database.CoreMigrationStates.Add(state = new());
        }
        state.SourceId = dataset.Manifest.SourceId;
        var checkpoints = await database.CoreImportCheckpoints.AsNoTracking().Where(x => x.SourceId == state.SourceId).ToArrayAsync(cancellationToken);
        if (checkpoints.Length == dataset.Documents.Count && checkpoints.Length > 0 && dataset.Documents.All(document =>
                checkpoints.Any(x => x.Container == document.Container && x.BlobName == document.BlobName &&
                    x.Sha256 == document.Sha256 && x.MappingVersion == MappingVersion)))
        {
            await transaction.CommitAsync(cancellationToken);
            return Report(dataset, plan, dataset.Documents.Count);
        }

        // Rebuild only rows owned by this import source, in one transaction. A crash
        // cannot commit half a group; unchanged snapshots do not rewrite anything.
        var maps = await database.LegacyIdentityMaps.AsNoTracking().Where(x => x.SourceId == state.SourceId).ToArrayAsync(cancellationToken);
        var groupIds = maps.Where(x => x.Kind == "group").Select(x => x.TargetId).ToArray();
        var userIds = maps.Where(x => x.Kind == "user").Select(x => x.TargetId).ToArray();
        var logIds = maps.Where(x => x.Kind == "operational-log").Select(x => x.TargetId).ToArray();
        await database.ArchivedOperationalLogs.Where(x => logIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        await database.TeamAdvice.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.NewsEditions.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.HallBaselineConfirmations.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        var libraries = maps.Where(x => x.Kind == "card-library").Select(x => x.LegacyKey).ToArray();
        var incomingLibraries = plan.CardLibraries.Select(x => x.Name).ToArray();
        if (await database.CardLibraries.AnyAsync(x => incomingLibraries.Contains(x.Name) && !libraries.Contains(x.Name), cancellationToken))
            throw new CoreImportException("target_card_library_conflict");
        await database.RuleCards.Where(x => libraries.Contains(x.Library)).ExecuteDeleteAsync(cancellationToken);
        await database.CardLibraries.Where(x => libraries.Contains(x.Name)).ExecuteDeleteAsync(cancellationToken);
        var oauthIds = maps.Where(x => x.Kind == "oauth").Select(x => x.LegacyKey).ToArray();
        var incomingOAuth = plan.OAuthRecords.Select(x => x.Id).ToArray();
        if (await database.OAuthRecords.AnyAsync(x => incomingOAuth.Contains(x.Id) && !oauthIds.Contains(x.Id), cancellationToken))
            throw new CoreImportException("target_oauth_conflict");
        await database.OAuthRecords.Where(x => oauthIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        await database.AccountSelections.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await database.UnresolvedAccountSelections.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await database.AccountPreferences.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        var deliveryIds = maps.Where(x => x.Kind == "push-delivery").Select(x => x.TargetId).ToArray();
        await database.PushDeliveryScopeConfirmations.Where(x => deliveryIds.Contains(x.DeliveryId)).ExecuteDeleteAsync(cancellationToken);
        await database.PushDeliveries.Where(x => deliveryIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        foreach (var root in maps.Where(x => x.Kind == "push-day"))
        {
            var parts = JsonSerializer.Deserialize<string[]>(root.LegacyKey)!;
            var year = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture); var day = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            if (year == 0) await database.LegacyPushActiveDays.ExecuteDeleteAsync(cancellationToken);
            else await database.PushDayCheckpoints.Where(x => x.SeasonIndex == year && x.Day == day).ExecuteDeleteAsync(cancellationToken);
        }
        await database.PushDeviceOwnershipEvents.Where(x => userIds.Contains(x.UserId) || (x.PreviousUserId != null && userIds.Contains(x.PreviousUserId.Value))).ExecuteDeleteAsync(cancellationToken);
        await database.PushDeviceBindings.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await database.UserPushPreferences.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync(cancellationToken);
        await database.RankCreditConfirmations.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.PushFormationChangeReceipts.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.HallArchives.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.OpeningStandingSnapshots.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.LeaguePlayerOrders.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.MarketTradeEvents.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.MarketTrades.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.MarketBooks.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.AuctionRosterChanges.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.AuctionSessions.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.PlayedMatchSnapshots.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.FantasyStandingSnapshots.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.FantasyCalendars.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.LeagueRules.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.RosterSnapshots.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        foreach (var root in maps.Where(x => x.Kind == "football-dataset"))
        {
            var components = JsonSerializer.Deserialize<string[]>(root.LegacyKey) ?? throw new CoreImportException("invalid_dataset_mapping");
            var kind = components[0]; var year = int.Parse(components[1], System.Globalization.CultureInfo.InvariantCulture); var day = int.Parse(components[2], System.Globalization.CultureInfo.InvariantCulture);
            if (kind is "live" or "official" or "notification")
            {
                var voteSource = kind == "live" ? FootballVoteSource.Live : kind == "official" ? FootballVoteSource.Official : FootballVoteSource.Notification;
                await database.FootballVotes.Where(x => x.SeasonIndex == year && x.Day == day && x.Source == voteSource).ExecuteDeleteAsync(cancellationToken);
            }
            if (kind == "chancedrealplayerwrapper") await database.FootballChances.Where(x => x.SeasonIndex == year && x.Day == day).ExecuteDeleteAsync(cancellationToken);
            if (kind == "statplayerswrapper") await database.FootballPlayerStatistics.Where(x => x.SeasonIndex == year).ExecuteDeleteAsync(cancellationToken);
            if (kind == "realrank") await database.FootballClubRanks.Where(x => x.SeasonIndex == year).ExecuteDeleteAsync(cancellationToken);
            await database.FootballDatasets.Where(x => x.Kind == kind && x.SeasonIndex == year && x.Day == day).ExecuteDeleteAsync(cancellationToken);
        }
        var importedSeasons = maps.Where(x => x.Kind == "calendar").Select(x => int.Parse(x.LegacyKey, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        await database.FootballMatches.Where(x => importedSeasons.Contains(x.SeasonIndex)).ExecuteDeleteAsync(cancellationToken);
        await database.FootballMatchdays.Where(x => importedSeasons.Contains(x.SeasonIndex)).ExecuteDeleteAsync(cancellationToken);
        var importedPlayers = maps.Where(x => x.Kind == "player").Select(x => x.TargetId).ToArray();
        var importedClubs = maps.Where(x => x.Kind == "club").Select(x => x.TargetId).ToArray();
        await database.PlayerSeasons.Where(x => importedPlayers.Contains(x.PlayerId)).ExecuteDeleteAsync(cancellationToken);
        await database.Players.Where(x => importedPlayers.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        await database.Clubs.Where(x => importedClubs.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        await database.TeamOwnerships.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.TeamSeasons.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.LeagueBasketSeasons.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.GroupLeagueBaskets.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.LeagueSeasons.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.BasketSeasons.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.Leagues.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.Baskets.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.GroupMemberships.Where(x => groupIds.Contains(x.GroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.Groups.Where(x => groupIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        await database.Users.Where(x => userIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        await database.LegacyIdentityMaps.Where(x => x.SourceId == state.SourceId).ExecuteDeleteAsync(cancellationToken);
        await database.CoreImportCheckpoints.Where(x => x.SourceId == state.SourceId).ExecuteDeleteAsync(cancellationToken);

        foreach (var season in plan.Seasons)
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == season, cancellationToken))
                database.Seasons.Add(new() { SeasonIndex = season, StartYear = season + 2011 });
        database.Users.AddRange(plan.Users.Values);
        database.Clubs.AddRange(plan.Clubs);
        database.Players.AddRange(plan.Players);
        database.PlayerSeasons.AddRange(plan.PlayerSeasons);
        database.FootballMatchdays.AddRange(plan.Matchdays);
        database.RosterSnapshots.AddRange(plan.Rosters);
        database.FootballDatasets.AddRange(plan.Datasets);
        database.FootballVotes.AddRange(plan.Votes);
        database.FootballChances.AddRange(plan.Chances);
        database.FootballPlayerStatistics.AddRange(plan.Statistics);
        database.FootballClubRanks.AddRange(plan.ClubRanks);
        database.FantasyCalendars.AddRange(plan.Calendars);
        database.FantasyStandingSnapshots.AddRange(plan.Standings);
        database.PlayedMatchSnapshots.AddRange(plan.PlayedMatches);
        database.OpeningStandingSnapshots.AddRange(plan.OpeningStandings);
        database.LeaguePlayerOrders.AddRange(plan.PlayerOrders);
        database.MarketBooks.AddRange(plan.MarketBooks);
        database.MarketTrades.AddRange(plan.MarketTrades);
        database.AuctionSessions.AddRange(plan.Auctions);
        database.HallArchives.AddRange(plan.HallArchives);
        database.CardLibraries.AddRange(plan.CardLibraries);
        database.RuleCards.AddRange(plan.Cards);
        database.UserPushPreferences.AddRange(plan.PushPreferences);
        database.PushDeviceBindings.AddRange(plan.PushDevices);
        database.PushDayCheckpoints.AddRange(plan.PushCheckpoints);
        database.LegacyPushActiveDays.AddRange(plan.PushActiveDays);
        database.PushDeliveries.AddRange(plan.PushDeliveries);
        database.NewsEditions.AddRange(plan.News);
        database.TeamAdvice.AddRange(plan.Advice);
        database.ArchivedOperationalLogs.AddRange(plan.Logs);
        database.HallRankContributions.AddRange(plan.HallContributions);
        database.OAuthRecords.AddRange(plan.OAuthRecords);
        database.AccountPreferences.AddRange(plan.Preferences);
        database.AccountSelections.AddRange(plan.Selections);
        database.UnresolvedAccountSelections.AddRange(plan.UnresolvedSelections);
        database.Groups.AddRange(plan.Groups);
        database.Baskets.AddRange(plan.Baskets);
        database.BasketSeasons.AddRange(plan.BasketSeasons);
        database.Leagues.AddRange(plan.Leagues);
        database.LeagueSeasons.AddRange(plan.LeagueSeasons);
        database.LeagueRules.AddRange(plan.Rules);
        database.LeagueBasketSeasons.AddRange(plan.Participations);
        database.GroupLeagueBaskets.AddRange(plan.LeagueBaskets);
        database.GroupMemberships.AddRange(plan.Memberships);
        database.TeamSeasons.AddRange(plan.Teams);
        database.TeamOwnerships.AddRange(plan.Owners);
        database.LegacyIdentityMaps.AddRange(plan.Maps);
        database.CoreImportCheckpoints.AddRange(dataset.Documents.Select(document => new CoreImportCheckpoint
        {
            SourceId = state.SourceId, Container = document.Container, BlobName = document.BlobName,
            Sha256 = document.Sha256, MappingVersion = MappingVersion
        }));
        await database.SaveChangesAsync(cancellationToken);
        if (await database.Groups.CountAsync(x => plan.GroupIds.Contains(x.Id), cancellationToken) != plan.Groups.Count ||
            await database.TeamSeasons.CountAsync(x => plan.GroupIds.Contains(x.GroupId), cancellationToken) != plan.Teams.Count)
            throw new CoreImportException("reconciliation_failed");
        await transaction.CommitAsync(cancellationToken);
        return Report(dataset, plan, 0);
    }

    public static CoreImportReport Report(VerifiedJsonDataset dataset, CorePlan plan, int skipped = 0) => new(
        MappingVersion, dataset.Documents.Count, skipped, plan.Users.Count, plan.Groups.Count, plan.Teams.Count,
        dataset.Manifest.Containers.Where(x => !Containers.Contains(x.Name) && !PreservedContainers.ContainsKey(x.Name) && x.Blobs.Count > 0).Select(x => x.Name)
            .Concat(dataset.Manifest.Tables.Where(x => x.EntityCount > 0 && !Tables.Contains(x.Name)).Select(x => "table:" + x.Name)).Order().ToArray(),
         [.. plan.UnresolvedSelections.Count == 0 ? Array.Empty<string>() : new[] { "Unresolved historical account selections: review UnresolvedAccountSelections" },
          .. plan.ReconciledIdentityDocuments.Count > 0
              ? new[] { "Some appidentity records used legacy storage IDs or unusable embedded keys instead of the profile email. Lookup consistency was verified; review reconciledIdentityDocuments. Original envelopes remain in the verified dump." } : Array.Empty<string>(),
          .. plan.ImportLegacyLogs && dataset.Manifest.Scope == "blob-current-state"
              ? new[] { "Historical log import was requested, but this dump excludes Table Storage. Export with --include-tables first." } : Array.Empty<string>(),
         .. plan.Rosters.Any(x => x.Kind == RosterKind.Season && x.RankCreditGranted == null)
             ? new[] { "Legacy rank-credit checkpoints require explicit confirmation before automatic re-awards; imported balances were not changed." } : Array.Empty<string>(),
         .. plan.CardLibraries.Any(x => x.Name != "default")
             ? new[] { "Non-default card libraries were preserved but are not exposed as a global catalogue without explicit scope confirmation." } : Array.Empty<string>(),
         .. plan.PushDeliveries.Count > 0 ? new[] { "Legacy pending push deliveries are quarantined until an administrator explicitly confirms recipient group/league scope. No notifications were sent by import." } : Array.Empty<string>(),
         .. plan.HallArchives.Any(x => !x.ContributionsInitialized) ? new[] { "Hall career totals do not match the available seasonal snapshots; confirm a baseline before incremental refresh. Imported totals were preserved." } : Array.Empty<string>()],
         PreservedArtifacts: dataset.Manifest.Containers.Where(x => PreservedContainers.ContainsKey(x.Name) && x.Blobs.Count > 0).OrderBy(x => x.Name)
             .Select(x => x.Name + ": " + PreservedContainers[x.Name])
              .Concat(dataset.Manifest.Containers.Where(x => !plan.ImportLegacyAuctions && LegacyAuctionContainers.Contains(x.Name) && x.Blobs.Count > 0)
                  .OrderBy(x => x.Name).Select(x => x.Name + ": excluded from SQL by explicit import option; original bytes preserved in verified dump"))
              .Concat(dataset.Manifest.Tables.Where(x => !plan.ImportLegacyLogs && x.Name == "logs" && x.EntityCount > 0)
                  .Select(x => "table:logs: historical logs excluded from SQL; original rows preserved in verified dump")).ToArray(),
          ExcludedContainers: (plan.ImportLegacyAuctions ? Enumerable.Empty<string>() : LegacyAuctionContainers)
              .Concat(plan.ImportLegacyLogs ? [] : new[] { "table:logs" }).Order().ToArray(),
          ReconciledIdentityDocuments: plan.ReconciledIdentityDocuments.ToArray());

    private static CorePlan BuildPlan(VerifiedJsonDataset dataset, CoreImportOptions options)
    {
        var plan = new CorePlan { ImportLegacyAuctions = options.ImportLegacyAuctions, ImportLegacyLogs = options.ImportLegacyLogs };
        var source = dataset.Manifest.SourceId;
        var logoFiles = dataset.Manifest.Containers.Where(x => x.Name == "logos").SelectMany(x => x.Blobs).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var accounts = new Dictionary<string, AppIdentity>(StringComparer.Ordinal);
        foreach (var document in dataset.Documents.Where(x => x.Container == "appidentity"))
        {
            try
            {
                var envelope = document.Envelope.Deserialize<Entity<AppIdentity, string>>();
                var account = envelope?.Value ?? throw new CoreImportException("invalid_legacy_envelope");
                var email = Email(account.Email);
                var reconciled = ValidateIdentityKey(document, envelope!.Key, email);
                if (!accounts.TryAdd(email, account)) throw new CoreImportException("identity_collision");
                var user = AddUser(email, account.Username ?? email);
                if (reconciled)
                {
                    plan.ReconciledIdentityDocuments.Add(document.Sha256);
                    // Preserve a durable source -> account audit without copying
                    // an unknown opaque key (possibly sensitive) into SQL/logs.
                    // The original embedded key is retained in the verified dump.
                    var archiveReference = "blobs/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(document.Container + "\0" + document.BlobName))) + ".bin";
                    plan.Maps.Add(new() { SourceId = source, Kind = "identity-document", LegacyKey = Composite(archiveReference, document.Sha256), TargetId = user.Id });
                }
            }
            catch (CoreImportException fault) when (fault.DocumentSha256 is null)
            {
                throw new CoreImportException(fault.Message, fault.Field, fault.Reader, document.Sha256);
            }
        }
        var legacyGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var document in dataset.Documents.Where(x => x.Container == "group"))
        {
            var (key, group) = Read<Group>(document);
            if (key != group.Id || !legacyGroups.Add(key)) throw new CoreImportException("group_key_collision");
            var groupId = Map("group", key);
            plan.Groups.Add(new() { Id = groupId, Name = Name(group.Name) });
            var memberships = new Dictionary<string, GroupMembership>(StringComparer.Ordinal);
            foreach (var member in group.Users ?? [])
            {
                var email = Email(member.Email);
                var roles = (GroupRole)(int)member.Role;
                if ((int)roles is < 0 or > 15 || memberships.ContainsKey(email)) throw new CoreImportException("membership_collision");
                if (roles != GroupRole.None && !accounts.ContainsKey(email)) throw new CoreImportException("missing_active_account");
                var user = AddUser(email, member.Username ?? email);
                if (accounts.TryGetValue(email, out var account))
                {
                    var cached = (account.Groups ?? []).Where(x => x.Id == key).ToArray();
                    if (cached.Length > 1 || (cached.SingleOrDefault()?.Role ?? IdentityRole.None) != member.Role)
                        throw new CoreImportException("membership_conflict");
                }
                memberships.Add(email, new() { GroupId = groupId, UserId = user.Id, Roles = roles });
            }
            foreach (var account in accounts.Where(x => (x.Value.Groups ?? []).Any(g => g.Id == key && g.Role != IdentityRole.None)))
                if (!memberships.ContainsKey(account.Key)) throw new CoreImportException("membership_conflict");

            var baskets = new Dictionary<string, Guid>(StringComparer.Ordinal);
            var basketSeasons = new HashSet<(Guid BasketId, int Season)>();
            foreach (var basket in group.Baskets ?? [])
            {
                var basketId = Map("basket", Composite(key, basket.Id));
                if (string.IsNullOrWhiteSpace(basket.Id) || !baskets.TryAdd(basket.Id, basketId)) throw new CoreImportException("basket_collision");
                plan.Baskets.Add(new() { GroupId = groupId, BasketId = basketId, Name = Name(basket.Name) });
                foreach (var annual in basket.Years ?? [])
                {
                    AddSeason(annual.Year);
                    if (!basketSeasons.Add((basketId, annual.Year))) throw new CoreImportException("basket_season_collision");
                    plan.BasketSeasons.Add(new() { GroupId = groupId, BasketId = basketId, SeasonIndex = annual.Year });
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var owners = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var team in annual.Teams ?? [])
                    {
                        var email = Email(team.Owner);
                        if (!owners.Add(email) || !names.Add(Name(team.Name))) throw new CoreImportException("team_collision");
                        var teamId = Map("team", Composite(key, basket.Id, annual.Year.ToString(System.Globalization.CultureInfo.InvariantCulture), email));
                        var logoKey = $"{key}/{annual.Year}/{team.Owner}.png";
                        plan.Teams.Add(new() { Id = teamId, GroupId = groupId, BasketId = basketId, SeasonIndex = annual.Year,
                            Name = Name(team.Name), NormalizedName = Name(team.Name).ToLowerInvariant(), LogoBlobName = logoFiles.Contains(logoKey) ? logoKey : null });
                        foreach (var owner in (team.AdditionalOwners ?? []).Select(value => Email(value, "team.AdditionalOwners[]")).Append(email).Distinct())
                        {
                            var user = AddUser(owner, owner);
                            if (!memberships.ContainsKey(owner))
                                memberships.Add(owner, new() { GroupId = groupId, UserId = user.Id, Roles = GroupRole.None });
                            plan.Owners.Add(new() { GroupId = groupId, TeamId = teamId, BasketId = basketId,
                                SeasonIndex = annual.Year, UserId = user.Id, IsPrimary = owner == email });
                        }
                    }
                }
            }
            var leagueIds = new HashSet<string>();
            foreach (var league in group.Leagues ?? [])
            {
                if (string.IsNullOrWhiteSpace(league.Id) || !leagueIds.Add(league.Id)) throw new CoreImportException("league_collision");
                var leagueId = Map("league", Composite(key, league.Id));
                plan.Leagues.Add(new() { GroupId = groupId, LeagueId = leagueId, Name = Name(league.Name), IsMain = league.IsMain, CompetitionType = (int)league.Type });
                foreach (var keyBasket in (league.BasketsId ?? []).Distinct())
                {
                    if (!baskets.TryGetValue(keyBasket, out var linkedBasket)) throw new CoreImportException("missing_basket");
                    plan.LeagueBaskets.Add(new() { GroupId = groupId, LeagueId = leagueId, BasketId = linkedBasket });
                }
                var seasons = new HashSet<int>();
                foreach (var annual in league.Years ?? [])
                {
                    AddSeason(annual.Year);
                    if (!seasons.Add(annual.Year)) throw new CoreImportException("league_season_collision");
                    plan.LeagueSeasons.Add(new() { GroupId = groupId, LeagueId = leagueId, SeasonIndex = annual.Year, CompetitionType = (int)annual.Type });
                    plan.Rules.Add(LegacyRulesMapping.Convert(groupId, leagueId, annual.Year, annual.Settings, sourceLeague =>
                    {
                        if (!(group.Leagues ?? []).Any(x => x.Id == sourceLeague)) throw new CoreImportException("missing_previous_league");
                        return Map("league", Composite(key, sourceLeague));
                    }));
                    foreach (var basketKey in (league.BasketsId ?? []).Distinct())
                    {
                        if (!baskets.TryGetValue(basketKey, out var basketId)) throw new CoreImportException("missing_basket");
                        // A league can have historical seasons without that basket's annual teams.
                        if (!basketSeasons.Contains((basketId, annual.Year))) continue;
                        plan.Participations.Add(new() { GroupId = groupId, LeagueId = leagueId, BasketId = basketId, SeasonIndex = annual.Year });
                    }
                }
            }
            plan.Memberships.AddRange(memberships.Values);
        }
        foreach (var account in accounts.Values)
            if ((account.Groups ?? []).Any(x => x.Role != IdentityRole.None && !legacyGroups.Contains(x.Id)))
                throw new CoreImportException("missing_group");
        LoadRosters(dataset, plan, Map, AddUser, AddSeason, options);
        LoadCards(dataset, plan, Map);
        LoadEditorialAndLogs(dataset, plan, Map, AddSeason);
        LoadPushPreferences(dataset, plan, AddUser);
        foreach (var document in dataset.Documents.Where(x => x.Container == "mcp-oauth"))
        {
            var (key, value) = Read<OAuthDocument>(document);
            if (value.Id != key || string.IsNullOrWhiteSpace(value.Kind) || string.IsNullOrWhiteSpace(value.Version) || plan.OAuthRecords.Any(x => x.Id == key))
                throw new CoreImportException("invalid_oauth_document");
            if (value.Payload is not null) { using var parsed = JsonDocument.Parse(value.Payload); }
            Map("oauth", key);
            plan.OAuthRecords.Add(new() { Id = key, Kind = value.Kind, Version = value.Version, Payload = value.Payload,
                DeleteAfterUtc = value.DeleteAfterUtc?.ToUniversalTime() });
        }
        var preferences = new Dictionary<string, Settings>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "settings"))
        {
            var (key, value) = Read<Settings>(document);
            var email = Email(key);
            if (!plan.Users.ContainsKey(email)) throw new CoreImportException("missing_settings_account");
            if (!preferences.TryAdd(email, value)) throw new CoreImportException("settings_collision");
        }
        foreach (var user in plan.Users)
        {
            var settings = preferences.GetValueOrDefault(user.Key) ?? new Settings();
            plan.Preferences.Add(new() { UserId = user.Value.Id, AiPrompt = settings.AiPrompt,
                AutoOptimalFormation = settings.AutoOptimalFormation, LiveWidgetEnabled = settings.LiveWidgetEnabled });
        }
        foreach (var pair in accounts)
        {
            var user = plan.Users[pair.Key];
            var state = pair.Value.State ?? new AppIdentityState();
            var selection = new AccountSelection { UserId = user.Id };
            var unresolved = false;
            if (state.GroupId is { Length: > 0 } groupKey)
            {
                var groupMap = plan.Maps.SingleOrDefault(x => x.Kind == "group" && x.LegacyKey == groupKey);
                if (groupMap is null) unresolved = true;
                else
                {
                    selection.GroupId = groupMap.TargetId;
                    if (!plan.Memberships.Any(x => x.GroupId == groupMap.TargetId && x.UserId == user.Id))
                        plan.Memberships.Add(new() { GroupId = groupMap.TargetId, UserId = user.Id, Roles = GroupRole.None });
                    if (state.LeagueId is { Length: > 0 } leagueKey)
                    {
                        var leagueMap = plan.Maps.SingleOrDefault(x => x.Kind == "league" && x.LegacyKey == Composite(groupKey, leagueKey));
                        if (leagueMap is null) unresolved = true; else selection.LeagueId = leagueMap.TargetId;
                    }
                    if (!string.IsNullOrWhiteSpace(state.MeOrLeaderEmail))
                    {
                        if (!plan.Users.TryGetValue(Email(state.MeOrLeaderEmail), out var owner)) unresolved = true;
                        else
                        {
                            selection.WorkingUserId = owner.Id;
                            if (!plan.Memberships.Any(x => x.GroupId == groupMap.TargetId && x.UserId == owner.Id))
                                plan.Memberships.Add(new() { GroupId = groupMap.TargetId, UserId = owner.Id, Roles = GroupRole.None });
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(state.LeagueId) || !string.IsNullOrEmpty(state.MeOrLeaderEmail)) unresolved = true;
            if (state.Year is { } year)
            {
                if (year is < 1 or > 200) unresolved = true;
                else { AddSeason(year); selection.Year = year; }
            }
            plan.Selections.Add(selection);
            if (unresolved) plan.UnresolvedSelections.Add(new() { UserId = user.Id, LegacyGroupId = state.GroupId,
                LegacyLeagueId = state.LeagueId, Year = state.Year, WorkingEmail = state.MeOrLeaderEmail });
        }
        if (plan.Groups.Count == 0 && plan.Users.Count == 0) throw new CoreImportException("no_core_documents");
        return plan;

        Guid Map(string kind, string legacyKey)
        {
            var existing = plan.Maps.FirstOrDefault(x => x.Kind == kind && x.LegacyKey == legacyKey);
            if (existing is not null) return existing.TargetId;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Composite(source, kind, legacyKey)));
            var id = new Guid(hash.AsSpan(0, 16));
            plan.Maps.Add(new() { SourceId = source, Kind = kind, LegacyKey = legacyKey, TargetId = id });
            return id;
        }
        UserAccount AddUser(string email, string displayName)
        {
            if (plan.Users.TryGetValue(email, out var existing)) return existing;
            var user = new UserAccount { Id = Map("user", email), Email = email, DisplayName = Name(displayName),
                IsEnabled = accounts.ContainsKey(email), IsHistoricalPlaceholder = !accounts.ContainsKey(email) };
            plan.Users.Add(email, user);
            return user;
        }
        void AddSeason(int index)
        {
            if (index is < 1 or > 200) throw new CoreImportException("invalid_season");
            plan.Seasons.Add(index);
        }
    }

    private static (string Key, T Value) Read<T>(LegacyJsonDocument document) where T : class
    {
        // Use the actual Rystem Entity converter, including k/v and historical Key/Value envelopes.
        var entity = document.Envelope.Deserialize<Entity<T, string>>();
        if (entity?.Key is not { Length: > 0 } key || entity.Value is null) throw new CoreImportException("invalid_legacy_envelope");
        return (key, entity.Value);
    }
    private static string Email(string? value, [CallerArgumentExpression(nameof(value))] string? field = null,
        [CallerFilePath] string readerFile = "", [CallerLineNumber] int readerLine = 0)
    {
        if (!TryEmail(value, out var email))
            throw new CoreImportException("invalid_email", field, Path.GetFileName(readerFile) + ":" + readerLine);
        return email;
    }
    private static bool TryEmail(string? value, out string email)
    {
        email = value?.Trim().ToLowerInvariant() ?? "";
        return email.Length is > 0 and <= 320 && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
    }
    private static bool ValidateIdentityKey(LegacyJsonDocument document, string? key, string email)
    {
        // A storage key is not necessarily the login email. Preserve email
        // corroboration for email-keyed records; for technical string IDs require
        // an exact match between the embedded key and physical Blob name. Never
        // guess an ID from a display name or use it as an authentication subject.
        var hasLookupEmail = TryEmail(document.BlobName, out var lookupEmail);
        if (hasLookupEmail && lookupEmail != email) throw new CoreImportException("identity_collision");
        if (TryEmail(key, out var keyEmail))
        {
            if (keyEmail != email) throw new CoreImportException("identity_collision");
            return false;
        }
        if (hasLookupEmail && lookupEmail == email) return true;
        if (!string.IsNullOrWhiteSpace(key) && key.Length <= 1024 &&
            string.Equals(key, document.BlobName, StringComparison.Ordinal)) return true;
        _ = Email(key); // Keep strict validation when no independent lookup agrees.
        return false;
    }
    private static string Name(string? value) => value?.Trim() is { Length: >= 1 and <= 200 } name
        ? name : throw new CoreImportException("invalid_name");
    private static string Composite(params string[] parts) => JsonSerializer.Serialize(parts);
}

public sealed class CorePlan
{
    public bool ImportLegacyAuctions { get; init; } = true;
    public bool ImportLegacyLogs { get; init; }
    public List<string> ReconciledIdentityDocuments { get; } = [];
    public Dictionary<string, UserAccount> Users { get; } = [];
    public List<FantasyGroup> Groups { get; } = [];
    public Guid[] GroupIds => Groups.Select(x => x.Id).ToArray();
    public List<GroupMembership> Memberships { get; } = [];
    public List<GroupBasket> Baskets { get; } = [];
    public List<BasketSeason> BasketSeasons { get; } = [];
    public List<GroupLeague> Leagues { get; } = [];
    public List<LeagueSeason> LeagueSeasons { get; } = [];
    public List<LeagueBasketSeason> Participations { get; } = [];
    public List<FantasyTeamSeason> Teams { get; } = [];
    public List<TeamOwnership> Owners { get; } = [];
    public List<LegacyIdentityMap> Maps { get; } = [];
    public HashSet<int> Seasons { get; } = [];
    public List<LeagueRules> Rules { get; } = [];
    public List<AccountPreferences> Preferences { get; } = [];
    public List<AccountSelection> Selections { get; } = [];
    public List<UnresolvedAccountSelection> UnresolvedSelections { get; } = [];
    public List<FootballClub> Clubs { get; } = [];
    public List<FootballPlayer> Players { get; } = [];
    public List<PlayerSeason> PlayerSeasons { get; } = [];
    public List<RosterSnapshot> Rosters { get; } = [];
    public List<FootballMatchday> Matchdays { get; } = [];
    public List<OAuthRecord> OAuthRecords { get; } = [];
    public List<GroupLeagueBasket> LeagueBaskets { get; } = [];
    public List<FootballDataset> Datasets { get; } = [];
    public List<FootballVote> Votes { get; } = [];
    public List<FootballChance> Chances { get; } = [];
    public List<FootballPlayerStatistic> Statistics { get; } = [];
    public List<FootballClubRank> ClubRanks { get; } = [];
    public List<FantasyCalendar> Calendars { get; } = [];
    public List<FantasyStandingSnapshot> Standings { get; } = [];
    public List<PlayedMatchSnapshot> PlayedMatches { get; } = [];
    public List<OpeningStandingSnapshot> OpeningStandings { get; } = [];
    public List<LeaguePlayerOrder> PlayerOrders { get; } = [];
    public List<MarketBook> MarketBooks { get; } = [];
    public List<MarketTrade> MarketTrades { get; } = [];
    public List<AuctionSession> Auctions { get; } = [];
    public List<HallArchive> HallArchives { get; } = [];
    public List<CardLibrary> CardLibraries { get; } = [];
    public List<RuleCard> Cards { get; } = [];
    public List<UserPushPreferences> PushPreferences { get; } = [];
    public List<PushDeviceBinding> PushDevices { get; } = [];
    public List<PushDayCheckpoint> PushCheckpoints { get; } = [];
    public List<LegacyPushActiveDay> PushActiveDays { get; } = [];
    public List<PushDelivery> PushDeliveries { get; } = [];
    public List<NewsEdition> News { get; } = [];
    public List<TeamAdvice> Advice { get; } = [];
    public List<ArchivedOperationalLog> Logs { get; } = [];
    public List<HallRankContribution> HallContributions { get; } = [];
}
