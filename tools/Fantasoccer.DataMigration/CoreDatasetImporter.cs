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
    string[]? ExcludedContainers = null, string[]? ReconciledIdentityDocuments = null,
    AppliedIdentityResolution[]? ResolvedIdentityConflicts = null, int HistoricalNamedUsers = 0,
    AppliedPlayerCatalogResolution[]? ResolvedPlayerCatalogs = null, int UnresolvedPlayerCatalogEntries = 0,
    RecoveredRosterPlayer[]? RecoveredRosterPlayers = null, ResolvedClubCode[]? ResolvedClubCodes = null,
    int Clubs = 0, int ClubSeasons = 0, int[]? ClubCatalogueYears = null,
    MissingVoteClubDocument[]? MissingVoteClubSnapshots = null, int UnresolvedAnnualPlayerClubs = 0, int AmbiguousRosterClubRecoveries = 0,
    SkippedUnnamedVoteDocument[]? SkippedUnnamedVotes = null, LegacyPoliticalVoteDocument[]? LegacyPoliticalVotes = null,
    ExcludedRealCalendarDocument[]? ExcludedRealCalendars = null, DiscardedFutureScoreDocument[]? DiscardedFutureScores = null,
    SkippedInactiveSubstituteDocument[]? SkippedInactiveSubstitutes = null, SkippedUnusablePlayerRowDocument[]? SkippedUnusablePlayerRows = null,
    ExcludedNamelessRosterEntry[]? ExcludedNamelessRosterEntries = null);

public sealed class CoreImportException(string code, string? field = null, string? reader = null, string? documentSha256 = null,
    IdentityConflictDetails? identityConflict = null, PlayerValidationDetails? playerValidation = null,
    ClubValidationDetails? clubValidation = null, VoteValidationDetails? voteValidation = null) : Exception(code)
{
    // Source-code coordinates only; never the rejected email/payload/key.
    public string? Field { get; } = field;
    public string? Reader { get; } = reader;
    public string? DocumentSha256 { get; } = documentSha256;
    public IdentityConflictDetails? IdentityConflict { get; } = identityConflict;
    public PlayerValidationDetails? PlayerValidation { get; } = playerValidation;
    public ClubValidationDetails? ClubValidation { get; } = clubValidation;
    public VoteValidationDetails? VoteValidation { get; } = voteValidation;
}

public sealed partial class CoreDatasetImporter(FantasoccerDbContext database, CoreImportOptions? options = null)
{
    public const int MappingVersion = 40;
    private static readonly IReadOnlySet<string> LegacyAuctionContainers = new HashSet<string>(StringComparer.Ordinal) { "auction", "randomizedplayers" };
    public static readonly IReadOnlySet<string> Containers = new HashSet<string>(StringComparer.Ordinal)
        { "group", "appidentity", "settings", "team", "dailyteams", "openingteams", "openingdailyteams", "realplayerswrapper", "realteamwrapper", "realcalendar", "mcp-oauth",
            "live", "notification", "official", "chancedrealplayerwrapper", "statplayerswrapper", "realrank", "calendar", "rank", "playedgame", "openingstandings", "randomizedplayers", "marketwrapper", "auction", "halloffame", "cards", "pushnotification", "pushnotificationsent", "news", "teamwithai" };
    public static readonly IReadOnlySet<string> Tables = new HashSet<string>(StringComparer.Ordinal) { "logs" };
    // These are intentionally not translated into live relational documents.
    // The verified export/restore retains every payload byte and manifest hash.
    public static readonly IReadOnlyDictionary<string, string> PreservedContainers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["images"] = "Blob files: retained in file storage; not SQL gameplay records",
        ["logos"] = "Blob files: retained with imported/versioned references",
        ["cardimages"] = "Blob files: retained with imported/versioned references",
        ["livegroup"] = "Private historical derived snapshots: archived in the verified dump; live reads use the SQL projection",
        ["dailyrank"] = "Historical daily standings: retained in the verified dump only; historical reads reconstruct saved calendar/results with the annual rules",
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
                await database.Players.AnyAsync(cancellationToken) || await database.Clubs.AnyAsync(cancellationToken) || await database.ClubSeasons.AnyAsync(cancellationToken) ||
                await database.FootballMatchdays.AnyAsync(cancellationToken) || await database.FootballDatasets.AnyAsync(cancellationToken) || await database.CardLibraries.AnyAsync(cancellationToken) ||
                await database.ArchivedOperationalLogs.AnyAsync(cancellationToken) || await database.NewsEditions.AnyAsync(cancellationToken) || await database.TeamAdvice.AnyAsync(cancellationToken) ||
                await database.UnresolvedPlayerCatalogEntries.AnyAsync(cancellationToken) || await database.LegacyPoliticalVoteEntries.AnyAsync(cancellationToken))
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
        await database.UnresolvedPlayerCatalogEntries.Where(x => x.SourceId == state.SourceId).ExecuteDeleteAsync(cancellationToken);
        await database.LegacyPoliticalVoteEntries.Where(x => x.SourceId == state.SourceId).ExecuteDeleteAsync(cancellationToken);
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
        var playerAliases = maps.Where(x => x.Kind == "player").Select(x => x.TargetId).ToArray();
        var clubAliases = maps.Where(x => x.Kind == "club").Select(x => x.TargetId).ToArray();
        var importedPlayers = await database.Players.Where(x => x.LegacyId != null && playerAliases.Contains(x.LegacyId.Value)).Select(x => x.Id).ToArrayAsync(cancellationToken);
        var importedClubs = await database.Clubs.Where(x => x.LegacyId != null && clubAliases.Contains(x.LegacyId.Value)).Select(x => x.Id).ToArrayAsync(cancellationToken);
        await database.ClubSeasons.Where(x => importedClubs.Contains(x.ClubId)).ExecuteDeleteAsync(cancellationToken);
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
        database.ClubSeasons.AddRange(plan.ClubSeasons);
        database.Players.AddRange(plan.Players);
        database.UnresolvedPlayerCatalogEntries.AddRange(plan.UnresolvedPlayerCatalogEntries);
        database.LegacyPoliticalVoteEntries.AddRange(plan.LegacyPoliticalVoteEntries);
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
        var plannedClubCodes = plan.Clubs.Select(x => x.Id).ToArray();
        if (await database.Groups.CountAsync(x => plan.GroupIds.Contains(x.Id), cancellationToken) != plan.Groups.Count ||
            await database.TeamSeasons.CountAsync(x => plan.GroupIds.Contains(x.GroupId), cancellationToken) != plan.Teams.Count ||
            await database.UnresolvedPlayerCatalogEntries.CountAsync(x => x.SourceId == state.SourceId, cancellationToken) != plan.UnresolvedPlayerCatalogEntries.Count ||
            await database.ClubSeasons.CountAsync(x => plannedClubCodes.Contains(x.ClubId), cancellationToken) != plan.ClubSeasons.Count ||
            await database.LegacyPoliticalVoteEntries.CountAsync(x => x.SourceId == state.SourceId, cancellationToken) != plan.LegacyPoliticalVoteEntries.Count)
            throw new CoreImportException("reconciliation_failed");
        await transaction.CommitAsync(cancellationToken);
        return Report(dataset, plan, 0);
    }

    public static CoreImportReport Report(VerifiedJsonDataset dataset, CorePlan plan, int skipped = 0) => new(
        MappingVersion, dataset.Documents.Count, skipped, plan.Users.Count, plan.Groups.Count, plan.Teams.Count,
        dataset.Manifest.Containers.Where(x => !Containers.Contains(x.Name) && !PreservedContainers.ContainsKey(x.Name) && x.Blobs.Count > 0).Select(x => x.Name)
            .Concat(dataset.Manifest.Tables.Where(x => x.EntityCount > 0 && !Tables.Contains(x.Name)).Select(x => "table:" + x.Name)).Order().ToArray(),
         [.. plan.UnresolvedSelections.Count == 0 ? Array.Empty<string>() : new[] { "Unresolved historical account selections: review UnresolvedAccountSelections" },
          .. plan.UnresolvedPlayerCatalogEntries.Count > 0
              ? new[] { "Unnamed catalog rows were preserved in SQL UnresolvedPlayerCatalogEntries and the verified dump, not imported as usable players. Optional checksum pins remain enforced; review resolvedPlayerCatalogs. Roster references remain strictly validated." } : Array.Empty<string>(),
          .. plan.RecoveredRosterPlayers.Count > 0
              ? new[] { "Identifiable players missing from the yearly catalogue were created, linked or had missing annual club metadata completed from roster snapshots. Review recoveredRosterPlayers; no identity was inferred from club, role or order." } : Array.Empty<string>(),
          .. plan.MissingVoteClubSnapshots.Count > 0
              ? new[] { "Named minimal legacy votes/baselines without club metadata were preserved with a NULL historical ClubCode, never replaced by the current annual club. Review missingVoteClubSnapshots. Their votes and flags were not discarded or corrected." } : Array.Empty<string>(),
           .. plan.SkippedUnnamedVotes.Count > 0
              ? new[] { "Legacy vote/baseline entries with missing, empty or whitespace-only names were excluded from SQL by operator-approved policy, even when hasVote is true. No player/club was invented and no unresolved-vote SQL archive was created. Original bytes remain in the verified dump. Review skippedUnnamedVotes; named entries and other contexts retain their existing validations." } : Array.Empty<string>(),
          .. plan.LegacyPoliticalVotes.Count > 0
              ? new[] { "Attested legacy political substitutes were preserved in LegacyPoliticalVoteEntries with exact original JSON and provenance, never as actual official votes. Review legacyPoliticalVotes. Effective political values are calculated only from the calendar's postponement history and the league settings; actual votes are not overwritten and other duplicate conflicts remain errors." } : Array.Empty<string>(),
          .. plan.SkippedUnusablePlayerRows.Count > 0
              ? new[] { "Probable-lineup and statistics rows whose name is missing or forms no player key (for example \"----\") were excluded from SQL by operator decision; no player was created. Named rows keep all validations. Original bytes remain in the verified dump. Review skippedUnusablePlayerRows." } : Array.Empty<string>(),
          .. plan.ExcludedNamelessRosterEntries.Count > 0
              ? new[] { "Sold or removed roster entries without any name or stored player key were excluded from SQL by operator decision. Their price, revenue, status and position are listed in excludedNamelessRosterEntries; costs recomputed from those historical rosters no longer include them. Active nameless entries remain errors. Original bytes remain in the verified dump." } : Array.Empty<string>(),
          .. plan.SkippedInactiveSubstitutes.Count > 0
              ? new[] { "Official legacy 6 d'ufficio entries given to inactive players (vote 6, hasVote/isFinal true, isIn false, no events) were excluded from SQL by operator decision: they are not votes and created no player or annual row. Original bytes remain in the verified dump. Review skippedInactiveSubstitutes." } : Array.Empty<string>(),
          .. plan.ExcludedRealCalendars.Count > 0
              ? new[] { "Real calendars whose dated games all belong to another season (saved under the previous season by the legacy job) were not imported: no matches, results, postponements or club memberships were taken from them. Those seasons have no real calendar in SQL; saved fantasy results are unchanged and recalculations of those days are refused. Original bytes remain in the verified dump. Review excludedRealCalendars." } : Array.Empty<string>(),
          .. plan.DiscardedFutureScores.Count > 0
              ? new[] { "Scores on real games scheduled after the export started cannot be results; those games were imported without a result (to be played). Original bytes remain in the verified dump. Review discardedFutureScores." } : Array.Empty<string>(),
          .. plan.PlayerSeasons.Any(x => x.ClubId is null)
              ? new[] { "Some annual player records still have no verified club. A same-year season roster can fill only an unambiguous missing annual club; daily/opening snapshots and competing clubs do not choose it." } : Array.Empty<string>(),
          .. plan.ResolvedClubCodes.Count > 0
              ? new[] { "Missing club codes were resolved from the versioned exact-name provider catalogue. Review resolvedClubCodes; no name prefixes, unknown aliases or conflicting explicit codes were used." } : Array.Empty<string>(),
          .. plan.Users.Values.Any(x => x.Email is null)
               ? new[] { "Named historical owners are existing users without login emails, inactive in their groups. Group administrators can manage their membership by user ID; no emails, roles or authentication bindings were invented." } : Array.Empty<string>(),
          .. plan.ResolvedIdentityConflicts.Count > 0
              ? new[] { "Explicit checksum-pinned identity resolutions selected the email-lookup profiles. Roles and selections were not merged; review resolvedIdentityConflicts. Original copies remain in the verified dump." } : Array.Empty<string>(),
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
                   .Select(x => "table:logs: historical logs excluded from SQL; original rows preserved in verified dump"))
               .Concat(plan.ResolvedIdentityConflicts.Select(x => "appidentity: unselected duplicate " + x.OtherDocumentSha256 + " preserved in verified dump; selected " + x.PreferredDocumentSha256))
               .Concat(plan.ResolvedPlayerCatalogs.Select(x => "realplayerswrapper: " + x.UnresolvedEntries + " unnamed catalog entries preserved in SQL and verified dump; document " + x.DocumentSha256))
               .Concat(plan.ExcludedRealCalendars.Select(x => "realcalendar: season " + x.SeasonIndex + " document " + x.DocumentSha256 + " contains another season's games; excluded from SQL, original bytes preserved in verified dump")).ToArray(),
           ExcludedContainers: (plan.ImportLegacyAuctions ? Enumerable.Empty<string>() : LegacyAuctionContainers)
               .Concat(plan.ImportLegacyLogs ? [] : new[] { "table:logs" }).Append("dailyrank").Order().ToArray(),
          ReconciledIdentityDocuments: plan.ReconciledIdentityDocuments.ToArray(),
          ResolvedIdentityConflicts: plan.ResolvedIdentityConflicts.ToArray(),
          HistoricalNamedUsers: plan.Users.Values.Count(x => x.Email is null),
          ResolvedPlayerCatalogs: plan.ResolvedPlayerCatalogs.ToArray(), UnresolvedPlayerCatalogEntries: plan.UnresolvedPlayerCatalogEntries.Count,
          RecoveredRosterPlayers: plan.RecoveredRosterPlayers.ToArray(), ResolvedClubCodes: plan.ResolvedClubCodes.ToArray(),
          Clubs: plan.Clubs.Count, ClubSeasons: plan.ClubSeasons.Count,
          ClubCatalogueYears: plan.Datasets.Where(x => x.Kind == FootballClubSeason.CatalogueDatasetKind).Select(x => x.SeasonIndex).Order().ToArray(),
          MissingVoteClubSnapshots: plan.MissingVoteClubSnapshots.ToArray(),
          UnresolvedAnnualPlayerClubs: plan.PlayerSeasons.Count(x => x.ClubId is null), AmbiguousRosterClubRecoveries: plan.AmbiguousRosterClubRecoveries,
           SkippedUnnamedVotes: plan.SkippedUnnamedVotes.ToArray(), LegacyPoliticalVotes: plan.LegacyPoliticalVotes.ToArray(),
           ExcludedRealCalendars: plan.ExcludedRealCalendars.ToArray(), DiscardedFutureScores: plan.DiscardedFutureScores.ToArray(),
           SkippedInactiveSubstitutes: plan.SkippedInactiveSubstitutes.ToArray(), SkippedUnusablePlayerRows: plan.SkippedUnusablePlayerRows.ToArray(),
           ExcludedNamelessRosterEntries: plan.ExcludedNamelessRosterEntries.ToArray());

    private static CorePlan BuildPlan(VerifiedJsonDataset dataset, CoreImportOptions options)
    {
        var plan = new CorePlan { ImportLegacyAuctions = options.ImportLegacyAuctions, ImportLegacyLogs = options.ImportLegacyLogs,
            ImportLegacyNamedOwners = options.ImportLegacyNamedOwners };
        var source = dataset.Manifest.SourceId;
        var logoFiles = dataset.Manifest.Containers.Where(x => x.Name == "logos").SelectMany(x => x.Blobs).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var accounts = new Dictionary<string, AppIdentity>(StringComparer.Ordinal);
        var identityCandidates = new List<IdentityCandidate>();
        foreach (var document in dataset.Documents.Where(x => x.Container == "appidentity"))
        {
            try
            {
                var envelope = document.Envelope.Deserialize<Entity<AppIdentity, string>>();
                var account = envelope?.Value ?? throw new CoreImportException("invalid_legacy_envelope");
                var email = Email(account.Email);
                var reconciled = ValidateIdentityKey(document, envelope!.Key, email);
                identityCandidates.Add(new(document, account, email, reconciled));
            }
            catch (CoreImportException fault) when (fault.DocumentSha256 is null)
            {
                throw new CoreImportException(fault.Message, fault.Field, fault.Reader, document.Sha256, fault.IdentityConflict);
            }
        }
        var identitySelection = SelectIdentityCandidates(identityCandidates, options.IdentityResolutions);
        foreach (var candidate in identitySelection.Selected) accounts.Add(candidate.Email, candidate.Account);
        foreach (var candidate in identitySelection.Selected)
        {
            var user = AddUser(candidate.Email, candidate.Account.Username ?? candidate.Email);
            if (!candidate.KeyReconciled) continue;
            plan.ReconciledIdentityDocuments.Add(candidate.Document.Sha256);
            plan.Maps.Add(new() { SourceId = source, Kind = "identity-document",
                LegacyKey = Composite(DocumentArchiveReference(candidate.Document), candidate.Document.Sha256), TargetId = user.Id });
        }
        foreach (var (preferred, other) in identitySelection.Resolved)
        {
            var applied = new AppliedIdentityResolution(preferred.Document.Sha256, other.Document.Sha256,
                DocumentArchiveReference(preferred.Document), DocumentArchiveReference(other.Document),
                TryEmail(other.Document.BlobName, out var otherLookup) && otherLookup == preferred.Email);
            plan.ResolvedIdentityConflicts.Add(applied);
            plan.Maps.Add(new() { SourceId = source, Kind = "identity-resolution",
                LegacyKey = Composite(applied.EmailLookupAlias ? "case-only-email-lookups" : "email-lookup-over-technical-key",
                    applied.PreferredArchiveReference, applied.PreferredDocumentSha256, applied.OtherArchiveReference, applied.OtherDocumentSha256),
                TargetId = plan.Users[preferred.Email].Id });
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
                var roles = (GroupRole)(int)member.Role;
                var email = roles == GroupRole.None ? OwnerKey(plan, groupId, member.Email) : Email(member.Email);
                if ((int)roles is < 0 or > 15 || memberships.ContainsKey(email)) throw new CoreImportException("membership_collision");
                if (roles != GroupRole.None && !accounts.ContainsKey(email)) throw new CoreImportException("missing_active_account");
                var user = AddOwner(groupId, member.Email, member.Username ?? member.Email);
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
                        var email = OwnerKey(plan, groupId, team.Owner);
                        if (!owners.Add(email) || !names.Add(Name(team.Name))) throw new CoreImportException("team_collision");
                        var teamId = Map("team", Composite(key, basket.Id, annual.Year.ToString(System.Globalization.CultureInfo.InvariantCulture), email));
                        var logoKey = $"{key}/{annual.Year}/{team.Owner}.png";
                        plan.Teams.Add(new() { Id = teamId, GroupId = groupId, BasketId = basketId, SeasonIndex = annual.Year,
                            Name = Name(team.Name), NormalizedName = Name(team.Name).ToLowerInvariant(), LogoBlobName = logoFiles.Contains(logoKey) ? logoKey : null });
                        foreach (var owner in (team.AdditionalOwners ?? []).Append(team.Owner).Distinct(StringComparer.Ordinal))
                        {
                            var ownerKey = OwnerKey(plan, groupId, owner);
                            var user = AddOwner(groupId, owner, owner);
                            if (!memberships.ContainsKey(ownerKey))
                                memberships.Add(ownerKey, new() { GroupId = groupId, UserId = user.Id, Roles = GroupRole.None });
                            if (plan.Owners.Any(x => x.TeamId == teamId && x.UserId == user.Id)) continue;
                            plan.Owners.Add(new() { GroupId = groupId, TeamId = teamId, BasketId = basketId,
                                SeasonIndex = annual.Year, UserId = user.Id, IsPrimary = ownerKey == email });
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
        LoadRosters(dataset, plan, Map, AddUser, AddOwner, AddSeason, options);
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
                        if (!plan.Users.TryGetValue(OwnerKey(plan, groupMap.TargetId, state.MeOrLeaderEmail), out var owner)) unresolved = true;
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
                IsEnabled = true, IsHistoricalPlaceholder = !accounts.ContainsKey(email) };
            plan.Users.Add(email, user);
            return user;
        }
        UserAccount AddOwner(Guid groupId, string owner, string displayName)
        {
            var key = OwnerKey(plan, groupId, owner);
            if (TryEmail(owner, out var email)) return AddUser(email, displayName);
            if (plan.Users.TryGetValue(key, out var existing)) return existing;
            var user = new UserAccount { Id = Map("user", key), Email = null, DisplayName = Name(displayName),
                IsEnabled = true, IsHistoricalPlaceholder = true };
            plan.Users.Add(key, user);
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
        if (hasLookupEmail && lookupEmail != email)
            throw new CoreImportException("identity_collision", identityConflict: new("blob_email_mismatch", CurrentArchiveReference: DocumentArchiveReference(document)));
        if (TryEmail(key, out var keyEmail))
        {
            if (keyEmail != email)
                throw new CoreImportException("identity_collision", identityConflict: new("embedded_key_email_mismatch", CurrentArchiveReference: DocumentArchiveReference(document)));
            return false;
        }
        if (hasLookupEmail && lookupEmail == email) return true;
        if (!string.IsNullOrWhiteSpace(key) && key.Length <= 1024 &&
            string.Equals(key, document.BlobName, StringComparison.Ordinal)) return true;
        _ = Email(key); // Keep strict validation when no independent lookup agrees.
        return false;
    }
    private static string DocumentArchiveReference(LegacyJsonDocument document) => "blobs/"
        + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(document.Container + "\0" + document.BlobName))) + ".bin";

    private static IdentityConflictDetails CompareIdentityDocuments(LegacyJsonDocument currentDocument, AppIdentity current,
        LegacyJsonDocument previousDocument, AppIdentity previous, string email)
    {
        static JsonElement Profile(LegacyJsonDocument document) => document.Envelope.TryGetProperty("v", out var value)
            ? value : document.Envelope.GetProperty("Value");
        // Compare memberships independent of ordering. Names are separate from
        // role assignments; nothing is selected/merged based on these diagnostics.
        var currentRoles = (current.Groups ?? []).Select(x => (Id: x?.Id, Role: x is null ? (int?)null : (int)x.Role)).OrderBy(x => x.Id, StringComparer.Ordinal).ThenBy(x => x.Role);
        var previousRoles = (previous.Groups ?? []).Select(x => (Id: x?.Id, Role: x is null ? (int?)null : (int)x.Role)).OrderBy(x => x.Id, StringComparer.Ordinal).ThenBy(x => x.Role);
        var currentLabels = (current.Groups ?? []).Select(x => (Id: x?.Id, Name: x?.Name, IsNull: x is null)).OrderBy(x => x.Id, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.IsNull);
        var previousLabels = (previous.Groups ?? []).Select(x => (Id: x?.Id, Name: x?.Name, IsNull: x is null)).OrderBy(x => x.Id, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.IsNull);
        return new("duplicate_profile_email", previousDocument.Sha256,
            DocumentArchiveReference(currentDocument), DocumentArchiveReference(previousDocument),
            JsonElement.DeepEquals(Profile(currentDocument), Profile(previousDocument)),
            string.Equals(current.Username, previous.Username, StringComparison.Ordinal),
            JsonElement.DeepEquals(JsonSerializer.SerializeToElement(current.State), JsonSerializer.SerializeToElement(previous.State)),
            currentRoles.SequenceEqual(previousRoles), currentLabels.SequenceEqual(previousLabels),
            TryEmail(currentDocument.BlobName, out var currentLookup) && currentLookup == email,
            TryEmail(previousDocument.BlobName, out var previousLookup) && previousLookup == email);
    }
    private static string Name(string? value) => value?.Trim() is { Length: >= 1 and <= 200 } name
        ? name : throw new CoreImportException("invalid_name");
    private static string Composite(params string[] parts) => JsonSerializer.Serialize(parts);
}

public sealed class CorePlan
{
    public bool ImportLegacyAuctions { get; init; } = true;
    public bool ImportLegacyLogs { get; init; }
    public bool ImportLegacyNamedOwners { get; init; }
    public List<string> ReconciledIdentityDocuments { get; } = [];
    public List<AppliedIdentityResolution> ResolvedIdentityConflicts { get; } = [];
    public List<AppliedPlayerCatalogResolution> ResolvedPlayerCatalogs { get; } = [];
    public List<UnresolvedPlayerCatalogEntry> UnresolvedPlayerCatalogEntries { get; } = [];
    public List<RecoveredRosterPlayer> RecoveredRosterPlayers { get; } = [];
    public List<ResolvedClubCode> ResolvedClubCodes { get; } = [];
    public List<MissingVoteClubDocument> MissingVoteClubSnapshots { get; } = [];
    public List<SkippedUnnamedVoteDocument> SkippedUnnamedVotes { get; } = [];
    public List<LegacyPoliticalVoteDocument> LegacyPoliticalVotes { get; } = [];
    public List<LegacyPoliticalVoteEntry> LegacyPoliticalVoteEntries { get; } = [];
    public List<SkippedInactiveSubstituteDocument> SkippedInactiveSubstitutes { get; } = [];
    public List<SkippedUnusablePlayerRowDocument> SkippedUnusablePlayerRows { get; } = [];
    public List<ExcludedNamelessRosterEntry> ExcludedNamelessRosterEntries { get; } = [];
    public List<ExcludedRealCalendarDocument> ExcludedRealCalendars { get; } = [];
    public List<DiscardedFutureScoreDocument> DiscardedFutureScores { get; } = [];
    public int AmbiguousRosterClubRecoveries { get; set; }
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
    public List<FootballClubSeason> ClubSeasons { get; } = [];
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
