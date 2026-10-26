using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Data.Common;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Teams;
using System.Text.Json;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class CoreFactAttribute : FactAttribute
{
    public CoreFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlFixture.EnvironmentVariable)))
            Skip = "Run the local PostgreSQL/Azurite integration suite.";
    }
}

public sealed partial class CoreMigrationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private StorageEndpoints _storage = null!;
    private string _dump = null!;
    private bool _ownsContainers;
    private readonly List<string> _extraContainers = [];
    private Group _first = null!;
    private Group _second = null!;
    private const string Email = "friend@example.test";
    private const string OtherEmail = "other@example.test";

    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _storage = new(configured);
        _storage.RequireLocalEmulator();
        if (_storage.Blobs.AccountName != "migrationtest") throw new InvalidOperationException("Dedicated test source required.");
        foreach (var name in new[] { "group", "appidentity" })
            if ((await _storage.Blobs.GetBlobContainerClient(name).ExistsAsync()).Value)
                throw new InvalidOperationException("Core fixture containers already exist; refusing to overwrite them.");
        foreach (var name in new[] { "group", "appidentity" }) await _storage.Blobs.GetBlobContainerClient(name).CreateAsync();
        _ownsContainers = true;
        _dump = Path.Combine(Directory.Exists("/tmp/opencode") ? "/tmp/opencode" : Path.GetTempPath(), "fantasoccer-core-test-" + Guid.NewGuid().ToString("N"));
        _first = LegacyGroup("first", IdentityRole.Admin);
        _second = LegacyGroup("second", IdentityRole.Participant);
        await PutGroupAsync(_first);
        await PutGroupAsync(_second);
        await PutAccountAsync(Email, IdentityRole.Admin, IdentityRole.Participant);
        await PutAccountAsync(OtherEmail, IdentityRole.Participant, IdentityRole.Participant);
        await new StorageExporter(_storage).ExportAsync(_dump, sourceQuiesced: true);
    }

    [CoreFact]
    public async Task Actual_Rystem_envelopes_import_into_relations_and_second_run_is_unchanged()
    {
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.False(report.FullMigrationComplete);
        Assert.DoesNotContain("AnnualLeague.Settings", report.PendingFields);
        Assert.Equal(4, await database.LeagueRules.CountAsync());
        Assert.Equal(1000, (await database.LeagueRules.FirstAsync()).StartingMoney);
        Assert.Equal(2, report.Groups);
        Assert.Equal(2, report.Users);
        Assert.Equal(2, report.Teams);
        Assert.Equal(4, await database.LeagueBasketSeasons.CountAsync());
        Assert.Equal(2, await database.BasketSeasons.CountAsync());
        Assert.Equal(4, await database.TeamOwnerships.CountAsync());
        var firstIds = await database.Groups.OrderBy(x => x.Name).Select(x => x.Id).ToArrayAsync();
        database.ChangeTracker.Clear();
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(report.Documents, repeated.SkippedDocuments);
        Assert.Equal(firstIds, await database.Groups.OrderBy(x => x.Name).Select(x => x.Id).ToArrayAsync());
    }

    [CoreFact]
    public async Task Account_selection_preferences_and_full_rule_children_are_imported_without_compact_payloads()
    {
        await PutAccountAsync(Email, IdentityRole.Admin, IdentityRole.Participant, new() { GroupId = "first", LeagueId = "league", Year = 15, MeOrLeaderEmail = OtherEmail });
        var settingsContainer = await ExtraContainerAsync("settings");
        await settingsContainer.GetBlobClient(Email).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Settings, string>.Default(
            new() { AiPrompt = "Private preferences", AutoOptimalFormation = true, LiveWidgetEnabled = false }, Email))));
        _first.Leagues[0].Years[0].Settings.Type = new()
        {
            Rounds = [new() { Name = "Round", FromStart = true, Type = RoundType.Elimination }],
            FromPreviousYear = new() { LeaguesId = ["cup"], MaxTeamsPerLeague = 2, RoundType = RoundType.Rank },
            CardTrainer = new() { MaxCardsPerType = new() { [CardType.Strategy] = 2 } }
        };
        await PutGroupAsync(_first);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var account = scope.ServiceProvider.GetRequiredService<AccountApplicationService>();
        var profile = await account.CurrentAsync(Caller());
        var group = await scope.ServiceProvider.GetRequiredService<GameGroupApplicationService>().GetAsync(Caller(), profile.State.GroupId!.Value);
        Assert.Equal(profile.State.LeagueId, group.Leagues.Single(x => x.Name == "League").Id);
        Assert.Equal(15, profile.State.Year);
        Assert.Equal(OtherEmail, profile.State.MeOrLeaderEmail);
        Assert.Equal("Private preferences", (await account.SettingsAsync(Caller())).AiPrompt);
        var rules = group.Leagues.Single(x => x.Name == "League").Years[0].Settings;
        Assert.Equal("Round", rules.TypeSettings!.Rounds[0].Name);
        Assert.Equal(2, rules.TypeSettings.CardTrainer.MaxCardsPerType[(int)CardType.Strategy]);
        Assert.Equal(group.Leagues.Single(x => x.Name == "Cup").Id, Assert.Single(rules.TypeSettings.FromPreviousYear!.LeaguesId));
        await using var database = fixture.CreateContext();
        Assert.Empty(await database.UnresolvedAccountSelections.ToArrayAsync());
    }

    [CoreFact]
    public async Task Shared_players_rosters_sold_entries_and_competition_snapshots_have_relational_identity()
    {
        _first.Baskets[0].Years.Add(new() { Year = 14, Teams = [new() { Name = "Previous team", Owner = Email, AdditionalOwners = [OtherEmail] }] });
        await PutGroupAsync(_first);
        var club = new RealTeam { Name = "Club", Abbreviation = "clb" };
        var player = new RealPlayer { Name = "Shared player", RealTeam = club, Role = Role.Forward, IsActive = true, Visible = true };
        var players = await ExtraContainerAsync("realplayerswrapper");
        await players.GetBlobClient("15").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<RealPlayersWrapper, int>.Default(new() { Players = [player] }, 15))));
        var season = await ExtraContainerAsync("team");
        var daily = await ExtraContainerAsync("dailyteams");
        var opening = await ExtraContainerAsync("openingteams");
        Team TeamData(int price) => new() { Name = "Team", Owner = Email, AdditionalOwners = [OtherEmail], MoneyFromRank = 17,
            OpeningCompetitionPrize = 9, Players = [new() { Name = player.Name, RealTeam = club, Role = Role.Forward, IsActive = true,
                Visible = true, Status = PlayerInTeamStatus.Sold, Price = 100, Revenue = 125, Position = FantaSoccerRole.Tribune },
                new() { Name = player.Name, RealTeam = club, Role = Role.Forward, IsActive = true, Visible = true,
                    Status = PlayerInTeamStatus.Active, Price = price, Position = FantaSoccerRole.Forward }] };
        foreach (var group in new[] { "first", "second" })
            await season.GetBlobClient(group).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Team, TeamKey>.Default(TeamData(30),
                new() { Group = group, BasketId = "same-local-basket", Year = 15, Email = Email }))));
        await daily.GetBlobClient("first-day").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Team, TeamDayKey>.Default(TeamData(20),
            new() { Group = "first", BasketId = "same-local-basket", Year = 15, Day = 3, Email = Email }))));
        foreach (var league in new[] { "league", "cup" })
            await opening.GetBlobClient(league).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Team, OpeningTeamKey>.Default(TeamData(40),
                new() { Group = "first", League = league, BasketId = "same-local-basket", Year = 15, Email = Email }))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        Assert.Equal(1, await database.Players.CountAsync());
        Assert.Equal(1, await database.PlayerSeasons.CountAsync());
        Assert.Equal(1, await database.Clubs.CountAsync());
        Assert.Equal(5, await database.RosterSnapshots.CountAsync());
        var roster = await database.RosterSnapshots.Where(x => x.Kind == RosterKind.Matchday).AsSplitQuery().SingleAsync();
        Assert.Equal(2, roster.Players.Count);
        Assert.Equal(125, roster.Players.Single(x => x.Status == 3).Revenue);
        Assert.Equal(20, roster.Players.Single(x => x.Status == 0).Price);
        Assert.Equal(2, await database.RosterSnapshots.Where(x => x.Kind == RosterKind.OpeningSeason).Select(x => x.CompetitionId).Distinct().CountAsync());
        Assert.All(await database.RosterSnapshots.Where(x => x.Kind == RosterKind.OpeningSeason).ToArrayAsync(), x => Assert.Equal(14, x.TeamOriginYear));
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
    }

    [CoreFact]
    public async Task OAuth_protocol_documents_are_copied_with_versions_retention_and_private_JSON_intact()
    {
        var container = await ExtraContainerAsync("mcp-oauth");
        var doc = new OAuthDocument { Id = "application-test", Kind = "application", Version = "source-version", Payload = "{\"clientId\":\"fixture-client\"}" };
        await container.GetBlobClient(doc.Id).UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<OAuthDocument, string>.Default(doc, doc.Id))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var row = await database.OAuthRecords.SingleAsync();
        Assert.Equal(doc.Id, row.Id);
        Assert.Equal(doc.Version, row.Version);
        Assert.Equal("fixture-client", JsonDocument.Parse(row.Payload!).RootElement.GetProperty("clientId").GetString());
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
    }

    [CoreFact]
    public async Task Italian_wall_clock_calendar_dates_are_preserved_and_normalized_explicitly_to_UTC()
    {
        var container = await ExtraContainerAsync("realcalendar");
        var date = new DateTime(2026, 10, 25, 20, 45, 0, DateTimeKind.Utc); // Legacy ItalyNow retained a UTC Kind on local wall time.
        var calendar = new RealCalendar { Year = 15, Days = [new() { Year = 15, SerieADay = 1, Games =
            [new() { Home = new() { Name = "Home" }, Away = new() { Name = "Away" }, Date = date }] }] };
        await container.GetBlobClient("15").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<RealCalendar, int>.Default(calendar, 15))));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var game = await database.FootballMatches.SingleAsync();
        Assert.Equal(new DateTime(2026, 10, 25, 20, 45, 0), game.LegacyKickoffLocal);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 19, 45, 0, TimeSpan.Zero), game.KickoffUtc);
    }

    [CoreFact]
    public async Task Two_concurrent_importers_do_not_duplicate_data()
    {
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var results = await Task.WhenAll(new CoreDatasetImporter(first).ImportAsync(_dump), new CoreDatasetImporter(second).ImportAsync(_dump));
        Assert.Equal(1, results.Count(x => x.SkippedDocuments == x.Documents));
        Assert.Equal(2, await first.Groups.CountAsync());
    }

    [CoreFact]
    public async Task Source_changes_remove_old_children_and_keep_stable_identity()
    {
        await using var database = fixture.CreateContext();
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        var id = await database.Groups.Where(x => x.Name == "first").Select(x => x.Id).SingleAsync();
        _first.Name = "Renamed source";
        _first.Baskets[0].Years[0].Teams.Clear();
        await PutGroupAsync(_first);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal("Renamed source", (await database.Groups.SingleAsync(x => x.Id == id)).Name);
        Assert.Equal(1, await database.TeamSeasons.CountAsync());
        Assert.Equal(2, await database.TeamOwnerships.CountAsync());
    }

    [CoreFact]
    public async Task Conflicting_memberships_are_rejected_without_any_target_write()
    {
        await PutAccountAsync(Email, IdentityRole.SuperAdmin, IdentityRole.Participant);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var error = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Equal("membership_conflict", error.Message);
        Assert.Empty(await database.Groups.ToArrayAsync());
        Assert.Empty(await database.Users.ToArrayAsync());
    }

    [CoreFact]
    public async Task Active_members_without_an_account_are_reported_instead_of_silently_provisioned()
    {
        await _storage.Blobs.GetBlobContainerClient("appidentity").GetBlobClient(OtherEmail).DeleteAsync();
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var error = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Equal("missing_active_account", error.Message);
        Assert.Empty(await database.Users.ToArrayAsync());
    }

    [CoreFact]
    public async Task Shared_service_uses_current_membership_and_blocks_cross_group_write_and_self_promotion()
    {
        await ImportAsync();
        await using var services = Services();
        Guid firstId, secondId;
        await using (var scope = services.CreateAsyncScope())
        {
            var application = scope.ServiceProvider.GetRequiredService<GroupApplicationService>();
            var groups = await application.ListAsync(Caller());
            firstId = groups.Single(x => x.Name == "first").GroupId;
            secondId = groups.Single(x => x.Name == "second").GroupId;
            Assert.Equal((int)GroupRole.Admin, groups.Single(x => x.GroupId == firstId).Roles);
            Assert.Equal((int)GroupRole.Participant, groups.Single(x => x.GroupId == secondId).Roles);
        }
        await AssertFaultAsync(services, service => service.RenameAsync(Caller(), secondId, new("Not allowed")), 403);
        await AssertFaultAsync(services, service => service.SetMemberAsync(Caller(), firstId, new(Email, "Friend", 8)), 403);
        await using (var database = fixture.CreateContext())
        {
            var membership = await database.GroupMemberships.SingleAsync(x => x.GroupId == secondId && x.User.Email == Email);
            membership.Roles = GroupRole.None;
            await database.SaveChangesAsync();
        }
        await AssertFaultAsync(services, service => service.GetAsync(Caller(), secondId), 403);
    }

    [CoreFact]
    public async Task Successful_interactive_write_closes_import_and_does_not_overwrite_another_users_profile()
    {
        await ImportAsync();
        await using var services = Services();
        await using (var scope = services.CreateAsyncScope())
        {
            var application = scope.ServiceProvider.GetRequiredService<GroupApplicationService>();
            var group = (await application.ListAsync(Caller())).Single(x => x.Name == "first");
            await application.SetMemberAsync(Caller(), group.GroupId, new(OtherEmail, "Forged global name", 1));
        }
        await using var database = fixture.CreateContext();
        Assert.Equal("Other", (await database.Users.SingleAsync(x => x.Email == OtherEmail)).DisplayName);
        var error = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Equal("interactive_writes_already_started", error.Message);
    }

    [CoreFact]
    public async Task Basket_sharing_and_new_group_creation_use_readable_contracts_and_do_not_grant_global_roles()
    {
        await ImportAsync();
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var application = scope.ServiceProvider.GetRequiredService<GroupApplicationService>();
        var created = await application.CreateAsync(Caller(globalAdmin: true), new("New friends"));
        var basket = await application.CreateBasketAsync(Caller(), created.GroupId, new("New basket", 15));
        await application.CreateLeagueAsync(Caller(), created.GroupId, new("League", 15, [basket]));
        await application.CreateLeagueAsync(Caller(), created.GroupId, new("Cup", 15, [basket]));
        var account = await application.GetAccountAsync(Caller());
        await application.CreateTeamAsync(Caller(), created.GroupId, new(basket, 15, "New team", account.UserId, []));
        var result = await application.GetAsync(Caller(), created.GroupId);
        Assert.Single(result.Baskets);
        Assert.Equal(2, result.Leagues.Length);
        Assert.Single(result.Teams);
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(json.TryGetProperty("groupId", out _));
        Assert.True(json.GetProperty("teams")[0].TryGetProperty("seasonIndex", out _));
        Assert.False(json.TryGetProperty("g", out _));
        Assert.False(account.CanCreateGroups);
    }

    [CoreFact]
    public async Task Failure_immediately_before_commit_rolls_back_every_imported_row_and_can_be_retried()
    {
        await using (var connection = fixture.CreateContext())
        await using (var failing = new FantasoccerDbContext(new DbContextOptionsBuilder<FantasoccerDbContext>()
            .UseNpgsql(connection.Database.GetConnectionString()).AddInterceptors(new RejectCommit()).Options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CoreDatasetImporter(failing).ImportAsync(_dump));
        }
        await using var database = fixture.CreateContext();
        Assert.Empty(await database.Groups.ToArrayAsync());
        Assert.Empty(await database.CoreImportCheckpoints.ToArrayAsync());
        Assert.Empty(await database.CoreMigrationStates.ToArrayAsync());
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(2, await database.Groups.CountAsync());
    }

    [CoreFact]
    public async Task Database_itself_rejects_cross_group_ownership_even_when_both_memberships_exist()
    {
        await ImportAsync();
        await using var database = fixture.CreateContext();
        var teams = await database.TeamSeasons.OrderBy(x => x.GroupId).ToArrayAsync();
        var user = await database.Users.SingleAsync(x => x.Email == OtherEmail);
        database.TeamOwnerships.Add(new()
        {
            GroupId = teams[0].GroupId, BasketId = teams[0].BasketId, SeasonIndex = teams[0].SeasonIndex,
            TeamId = teams[1].Id, UserId = user.Id
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(Npgsql.PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
    }

    [CoreFact]
    public async Task Last_manager_cannot_remove_itself_and_other_group_basket_cannot_be_attached()
    {
        await ImportAsync();
        await using var services = Services();
        Guid first, basket;
        await using (var scope = services.CreateAsyncScope())
        {
            var application = scope.ServiceProvider.GetRequiredService<GroupApplicationService>();
            var groups = await application.ListAsync(Caller());
            first = groups.Single(x => x.Name == "first").GroupId;
            basket = (await application.GetAsync(Caller(), groups.Single(x => x.Name == "second").GroupId)).Baskets[0].BasketId;
        }
        await AssertFaultAsync(services, service => service.SetMemberAsync(Caller(), first, new(Email, "Friend", 0)), 409);
        await AssertFaultAsync(services, service => service.CreateLeagueAsync(Caller(), first, new("Invalid", 15, [basket])), 400);
        await using var database = fixture.CreateContext();
        Assert.Null((await database.CoreMigrationStates.SingleAsync()).InteractiveWritesStartedAtUtc);
    }

    private sealed class RejectCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated interruption immediately before commit.");
    }

    private ServiceProvider Services() => new ServiceCollection().AddLogging()
        .AddScoped(_ => fixture.CreateContext()).AddGroupApplication()
        .Configure<GlobalAdministratorOptions>(options => options.Emails = [Email]).BuildServiceProvider(validateScopes: true);
    private async Task ImportAsync()
    {
        await using var database = fixture.CreateContext();
        await new CoreDatasetImporter(database).ImportAsync(_dump);
    }
    private static ClaimsPrincipal Caller(bool globalAdmin = false) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Email, Email) }.Concat(globalAdmin ? [new Claim(ClaimTypes.Role, "super_admin")] : []), "test"));
    private static async Task AssertFaultAsync(ServiceProvider services, Func<GroupApplicationService, Task> action, int status)
    {
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(status, (await Assert.ThrowsAsync<ApplicationFault>(() => action(scope.ServiceProvider.GetRequiredService<GroupApplicationService>()))).Status);
    }
    private async Task PutGroupAsync(Group group) => await _storage.Blobs.GetBlobContainerClient("group").GetBlobClient(group.Id)
        .UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<Group, string>.Default(group, group.Id))), overwrite: true);
    private async Task PutAccountAsync(string email, IdentityRole firstRole, IdentityRole secondRole, AppIdentityState? state = null)
    {
        var account = new AppIdentity { Email = email, Username = email == Email ? "Friend" : "Other", State = state ?? new(), Groups =
            [new() { Id = "first", Name = "first", Role = firstRole }, new() { Id = "second", Name = "second", Role = secondRole }] };
        await _storage.Blobs.GetBlobContainerClient("appidentity").GetBlobClient(email)
            .UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(Entity<AppIdentity, string>.Default(account, email))), overwrite: true);
    }
    private static Group LegacyGroup(string id, IdentityRole role) => new()
    {
        Id = id, Name = id, Users = [new() { Email = Email, Username = "Friend", Role = role }, new() { Email = OtherEmail, Username = "Other", Role = IdentityRole.Participant }],
        Baskets = [new() { Id = "same-local-basket", Name = "Basket", Years = [new() { Year = 15, Teams = [new() { Name = "Team", Owner = Email, AdditionalOwners = [OtherEmail] }] }] }],
        Leagues = [new() { Id = "league", Name = "League", BasketsId = ["same-local-basket"], Years = [new() { Year = 15, Settings = LeagueSetting.Default }] },
            new() { Id = "cup", Name = "Cup", BasketsId = ["same-local-basket"], Years = [new() { Year = 15, Settings = LeagueSetting.Default }] }]
    };

    public async ValueTask DisposeAsync()
    {
        if (!_ownsContainers) return;
        foreach (var name in _extraContainers) await _storage.Blobs.GetBlobContainerClient(name).DeleteIfExistsAsync();
        foreach (var name in new[] { "group", "appidentity" }) await _storage.Blobs.GetBlobContainerClient(name).DeleteIfExistsAsync();
        if (Directory.Exists(_dump)) Directory.Delete(_dump, recursive: true);
        // Reset only the unique database created by this fixture. A populated
        // opening schema cannot be losslessly downgraded to the early prototype.
        await fixture.DisposeAsync();
        await fixture.InitializeAsync();
    }

    private async Task<Azure.Storage.Blobs.BlobContainerClient> ExtraContainerAsync(string name)
    {
        var container = _storage.Blobs.GetBlobContainerClient(name);
        Assert.False((await container.ExistsAsync()).Value);
        await container.CreateAsync();
        _extraContainers.Add(name);
        return container;
    }
}
