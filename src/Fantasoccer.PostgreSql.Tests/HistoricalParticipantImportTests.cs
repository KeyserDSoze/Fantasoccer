using System.Security.Claims;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.HallOfFame;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Fantasoccer.LegacyModels;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Named_owners_require_opt_in_and_become_real_users_inactive_only_in_their_group_with_stable_history()
    {
        const string label = "Friend"; // Same display name as an authenticated user: never infer a match.
        _first.Baskets[0].Years.Add(new() { Year = 14, Teams = [
            new() { Name = "Old team", Owner = label, AdditionalOwners = [] },
            new() { Name = "Other old team", Owner = label.ToLowerInvariant(), AdditionalOwners = [] }] });
        _second.Baskets[0].Years.Add(new() { Year = 14, Teams = [new() { Name = "Different group", Owner = label, AdditionalOwners = [] }] });
        _first.Leagues[0].Years.Add(new() { Year = 14, Settings = LeagueSetting.Default });
        await PutGroupAsync(_first); await PutGroupAsync(_second);
        var rosters = await ExtraContainerAsync("team");
        await rosters.GetBlobClient("old-roster").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<Team, TeamKey>.Default(
            new() { Name = "Saved old team", Owner = label, AdditionalOwners = [], Players = [], MoneyFromRank = 19 },
            new() { Group = "first", BasketId = "same-local-basket", Year = 14, Email = label }))));
        var ranks = await ExtraContainerAsync("rank");
        await ranks.GetBlobClient("old-rank").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<Rank, LeagueKey>.Default(
            new() { SerieADay = 5, RankedTeams = new() { ["Historical"] = [new() { Name = "Saved old team", Owner = label, Point = 7 }] } },
            new() { Group = "first", League = "league", Year = 14 }))));
        var halls = await ExtraContainerAsync("halloffame");
        await halls.GetBlobClient("old-hall").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(LegacyEntity<HallOfFame, GeneralLeagueKey>.Default(
            new() { Ranks = [new() { Owner = label, Name = "Saved old team", Point = 10 }] }, new() { Group = "first", League = "league" }))));
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var refusal = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("invalid_email", refusal.Message);
        var originalHashes = manifest.Containers.SelectMany(x => x.Blobs).Select(x => x.Sha256).Order().ToArray();
        var etag = (await _storage.Blobs.GetBlobContainerClient("group").GetBlobClient("first").GetPropertiesAsync()).Value.ETag;
        var options = new CoreImportOptions(ImportLegacyNamedOwners: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Equal(3, report.HistoricalNamedUsers);
        var users = await database.Users.Where(x => x.Email == null).ToArrayAsync();
        Assert.Equal(3, users.Length); Assert.All(users, x => Assert.True(x.IsEnabled));
        Assert.All(users, x => Assert.True(x.IsHistoricalPlaceholder));
        Assert.All(await database.GroupMemberships.Where(x => x.User.Email == null).ToArrayAsync(), x => Assert.Equal(GroupRole.None, x.Roles));
        Assert.Empty(await database.ExternalLogins.ToArrayAsync());
        var roster = await database.RosterSnapshots.SingleAsync();
        var rank = (await database.FantasyStandingSnapshots.AsSplitQuery().SingleAsync()).Stages.Single().Teams.Single();
        Assert.Equal(rank.TeamId, roster.TeamId); Assert.Contains(roster.OwnerUserId, users.Select(x => x.Id));
        Assert.Equal(19, roster.MoneyFromRank); Assert.Equal(7, rank.Points); Assert.Equal(label, rank.Owner);
        var repeated = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
        Assert.Equal(users.Select(x => x.Id).Order(), (await database.Users.Where(x => x.Email == null).Select(x => x.Id).ToArrayAsync()).Order());
        Assert.Equal(etag, (await _storage.Blobs.GetBlobContainerClient("group").GetBlobClient("first").GetPropertiesAsync()).Value.ETag);
        var verified = await DumpArchive.VerifyAsync(_dump);
        Assert.Equal(originalHashes, verified.Containers.SelectMany(x => x.Blobs).Select(x => x.Sha256).Order());
        await using var services = Services(); await using var scope = services.CreateAsyncScope();
        var groups = scope.ServiceProvider.GetRequiredService<GroupApplicationService>();
        var first = (await groups.ListAsync(Caller())).Single(x => x.Name == "first").GroupId;
        var details = await groups.GetAsync(Caller(), first);
        var member = details.Members.Single(x => x.UserId == roster.OwnerUserId);
        Assert.False(member.IsActive); Assert.Null(member.Email);
        var accounts = scope.ServiceProvider.GetRequiredService<AccountApplicationService>();
        Assert.Null(await accounts.FindForLoginAsync(users[0].OwnerReference));
        var withoutEmail = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, label)], "test"));
        Assert.Equal(403, (await Assert.ThrowsAsync<ApplicationFault>(() => accounts.CurrentAsync(withoutEmail))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApplicationFault>(() => groups.GetAccountAsync(withoutEmail))).Status);
        var gameGroups = scope.ServiceProvider.GetRequiredService<GameGroupApplicationService>();
        var gameGroup = await gameGroups.GetAsync(Caller(), first);
        var ownerReference = "user:" + member.UserId.ToString("D");
        Assert.Contains(gameGroup.Users, x => x.Email == ownerReference && x.Role == 0);
        Assert.Contains(gameGroup.Baskets.SelectMany(x => x.Years).SelectMany(x => x.Teams), x => x.Owner == ownerReference && x.TeamId == roster.TeamId);
        var bulkUpdated = await gameGroups.UpdateAsync(Caller(), first, gameGroup with
        {
            Users = gameGroup.Users.Select(x => x.Email == ownerReference ? x with { Role = 2 } : x).ToArray()
        });
        Assert.Contains(bulkUpdated.Users, x => x.Email == ownerReference && x.Role == 2);
        await groups.UpdateMemberAsync(Caller(), first, member.UserId, new(true, 2, "recovered@example.test"));
        var projected = await gameGroups.GetAsync(Caller(), first);
        Assert.Contains(projected.Baskets.SelectMany(x => x.Years).SelectMany(x => x.Teams), x => x.Owner == "recovered@example.test" && x.TeamId == roster.TeamId);
        var scopedDatabase = scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var rosterService = new TeamRosterApplicationService(scopedDatabase, authorization, null!, TimeProvider.System, Options.Create(new AssetLinkOptions()));
        var rosterDto = await rosterService.GetAsync(Caller(), first, roster.BasketId, 14, ownerReference);
        Assert.NotNull(rosterDto); Assert.Equal(roster.TeamId, rosterDto.TeamId); Assert.Equal("recovered@example.test", rosterDto.Owner);
        var hall = await scopedDatabase.HallArchives.SingleAsync(x => x.GroupId == first);
        var refresh = new HallRefreshApplicationService(scopedDatabase, authorization, null!, TimeProvider.System);
        await refresh.ConfirmBaselineAsync(Caller(), first, hall.LeagueId, new(hall.Version));
        Assert.Equal(member.UserId, (await scopedDatabase.HallRankContributions.SingleAsync()).OwnerUserId);
        Assert.Equal(10, hall.Ranks.Single().Points);
        database.ChangeTracker.Clear();
        Assert.Equal(member.UserId, (await database.Users.SingleAsync(x => x.Email == "recovered@example.test")).Id);
        Assert.Equal(roster.TeamId, (await database.RosterSnapshots.SingleAsync()).TeamId);
        Assert.Equal(label, (await database.FantasyStandingSnapshots.AsSplitQuery().SingleAsync()).Stages.Single().Teams.Single().Owner);
        Assert.Equal("interactive_writes_already_started", (await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database, options).ImportAsync(_dump))).Message);
    }

    [CoreFact]
    public async Task Named_owner_opt_in_never_accepts_malformed_emails_empty_values_or_active_non_email_group_members()
    {
        var options = new CoreImportOptions(ImportLegacyNamedOwners: true);
        foreach (var value in new[] { "bad@@example.test", "", " ", new string('x', 201), "control\nlabel" })
        {
            _first.Baskets[0].Years[0].Teams[0].Owner = value;
            await PutGroupAsync(_first); await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            Assert.Equal("invalid_email", (await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump, options: options))).Message);
        }
        _first.Baskets[0].Years[0].Teams[0].Owner = Email;
        _first.Users.Add(new() { Email = "Historical participant", Username = "Historical participant", Role = IdentityRole.Admin });
        await PutGroupAsync(_first); await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        Assert.Equal("invalid_email", (await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump, options: options))).Message);
        await using var database = fixture.CreateContext(); Assert.Empty(await database.Users.ToArrayAsync());
    }
}
