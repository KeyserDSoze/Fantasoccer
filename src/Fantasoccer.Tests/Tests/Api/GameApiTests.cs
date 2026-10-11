using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class GameApiTests : IAsyncLifetime
{
    private const string GroupId = "game-api-live-test";
    private const string LeagueId = "game-api-league";
    private const string SecondaryLeagueId = "game-api-restrictive-league";
    private const string BasketId = "game-api-basket";
    private const string GameId = "game-api-match";
    private const string Owner = "game-owner@test.local";
    private const string Opponent = "game-opponent@test.local";
    private const string Admin = "game-admin@test.local";
    private readonly HttpClient _client;
    private readonly IRepository<AppIdentity, string> _users;
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<Calendar, LeagueKey> _calendars;
    private readonly IRepository<RealCalendar, int> _realCalendars;
    private readonly IRepository<Team, TeamDayKey> _teams;
    private RealCalendar? _calendarSnapshot;

    public GameApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _users = factory.Services.GetRequiredService<IRepository<AppIdentity, string>>();
        _groups = factory.Services.GetRequiredService<IRepository<Group, string>>();
        _calendars = factory.Services.GetRequiredService<IRepository<Calendar, LeagueKey>>();
        _realCalendars = factory.Services.GetRequiredService<IRepository<RealCalendar, int>>();
        _teams = factory.Services.GetRequiredService<IRepository<Team, TeamDayKey>>();
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _calendarSnapshot = await _realCalendars.GetAsync(FantacalcioTime.ActualYear, cancellationToken);
        await _realCalendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        await CleanupAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CleanupAsync(cancellationToken);
        await _realCalendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        if (_calendarSnapshot != null)
            await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, _calendarSnapshot, cancellationToken);
    }

    [Fact]
    public async Task Save_team_requires_authentication()
    {
        var response = await _client.PostAsync(
            "/Game/SaveTeam?groupId=missing",
            new StringContent("{\"n\":\"Test\",\"o\":\"missing@example.com\",\"a\":[],\"p\":[]}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Save_team_rejects_unknown_authenticated_user()
    {
        _client.DefaultRequestHeaders.Add("X-Test-User", "missing@example.com");

        var response = await _client.PostAsync(
            "/Game/SaveTeam?groupId=missing",
            new StringContent("{\"n\":\"Test\",\"o\":\"missing@example.com\",\"a\":[],\"p\":[]}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Utente non valido", body);
    }

    [Fact]
    public async Task Save_live_team_records_change_author_and_players()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var submitted = CreateTeam(Owner, "Home");
        var starter = submitted.Players.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = submitted.Players.First(x => x.Position == FantaSoccerRole.FirstBackupForward);
        (starter.Position, backup.Position) = (backup.Position, starter.Position);
        var storedBeforeSave = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        Assert.Equal(2, storedBeforeSave?.ActivePlayers.Count(player =>
            submitted.ActivePlayers.First(x => x.Key == player.Key).Position != player.Position));

        var response = await SaveAsync(submitted);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(1, saved.FormationChanges);
        var change = Assert.Single(saved.FormationChangeHistory);
        Assert.Equal("Game Owner", change.Author);
        Assert.Equal(2, change.Players.Count);
        Assert.Contains(change.Players, x => x.Name == starter.Name);
        Assert.Contains(change.Players, x => x.Name == backup.Name);
    }

    [Fact]
    public async Task Save_upcoming_game_accepts_live_formation_change()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var submitted = CreateTeam(Owner, "Home");
        var starter = submitted.Players.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = submitted.Players.First(x => x.Position == FantaSoccerRole.FirstBackupForward);
        (starter.Position, backup.Position) = (backup.Position, starter.Position);

        var response = await SaveAsync(submitted);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_incomplete_formation_returns_formation_validation_error()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var submitted = CreateTeam(Owner, "Home");
        submitted.Players.First(player => player.Position == FantaSoccerRole.GoalKeeper).Position = FantaSoccerRole.Tribune;

        var response = await SaveAsync(submitted, "formation");

        response.EnsureSuccessStatusCode();
        Assert.Contains("Meno di 11 giocatori titolari schierati", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_live_team_rejects_changes_after_deadline()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-5));
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var submitted = CreateTeam(Owner, "Home");
        var starter = submitted.Players.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = submitted.Players.First(x => x.Position == FantaSoccerRole.FirstBackupForward);
        (starter.Position, backup.Position) = (backup.Position, starter.Position);

        var response = await SaveAsync(submitted);

        response.EnsureSuccessStatusCode();
        Assert.Contains("finestra per le modifiche live è terminata", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        Assert.Equal(0, saved?.FormationChanges);
        Assert.Empty(saved?.FormationChangeHistory ?? []);
    }

    [Fact]
    public async Task Save_current_game_rejects_formation_edit_mode()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);

        var response = await SaveAsync(CreateTeam(Owner, "Home"), "formation");

        response.EnsureSuccessStatusCode();
        Assert.Contains("puoi modificare solo la prossima giornata", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Super_admin_can_read_current_game_in_formation_mode()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Admin);

        var response = await _client.GetAsync(
            $"/Game/Get?groupId={GroupId}&leagueId={LeagueId}&year={FantacalcioTime.ActualYear}&gameId={GameId}&editMode=formation&asAdmin=true",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"c\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Super_admin_can_save_current_game_in_formation_mode()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Admin);

        var response = await SaveAsync(CreateTeam(Owner, "Home"), "formation", asAdmin: true);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Super_admin_can_read_completed_game_in_formation_mode()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-12));
        _client.DefaultRequestHeaders.Add("X-Test-User", Admin);

        var response = await _client.GetAsync(
            $"/Game/Get?groupId={GroupId}&leagueId={LeagueId}&year={FantacalcioTime.ActualYear}&gameId={GameId}&editMode=formation&asAdmin=true",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"c\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Super_admin_can_save_completed_game_in_formation_mode()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-12));
        _client.DefaultRequestHeaders.Add("X-Test-User", Admin);
        var submitted = CreateTeam(Owner, "Home");
        var starter = submitted.Players.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = submitted.Players.First(x => x.Position == FantaSoccerRole.FirstBackupForward);
        (starter.Position, backup.Position) = (backup.Position, starter.Position);

        var response = await SaveAsync(submitted, "formation", asAdmin: true);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        Assert.Equal(FantaSoccerRole.FirstBackupForward, saved!.ActivePlayers.First(x => x.Key == starter.Key).Position);
        Assert.Equal(FantaSoccerRole.Forward, saved.ActivePlayers.First(x => x.Key == backup.Key).Position);
    }

    [Fact]
    public async Task Super_admin_can_calculate_another_team_optimal_formation()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Admin);

        var response = await _client.GetAsync(
            $"/Game/OptimalFormation?groupId={GroupId}&leagueId={LeagueId}&year={FantacalcioTime.ActualYear}&day=7&owner={Owner}&asAdmin=true",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Save_live_team_uses_main_league_rule_for_shared_basket()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1), includeLeagueWithoutLiveChanges: true);
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);

        var response = await SaveAsync(CreateTeam(Owner, "Home"));

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Secondary_league_shows_main_league_live_changes()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1), includeLeagueWithoutLiveChanges: true);
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);

        var game = await GetSecondaryGameAsync();

        Assert.True(game.CanEdit);
        Assert.True(game.IsLiveFormationWindow);
        Assert.Equal(2, game.LiveFormationChangesRemaining);
        Assert.NotNull(game.Teams);
        var team = Assert.Single(game.Teams, x => x.Owner == Owner);
        Assert.Equal(2, team.FormationChangesMaximum);
        Assert.Equal(2, team.FormationChangesRemaining);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secondary_league_changes_update_source_team_and_share_limit(bool useSwitch)
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1), includeLeagueWithoutLiveChanges: true);
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var submitted = CreateTeam(Owner, "Home");
        var starter = submitted.ActivePlayers.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = submitted.ActivePlayers.First(x => x.Position == FantaSoccerRole.FirstBackupForward);
        (starter.Position, backup.Position) = (backup.Position, starter.Position);

        var response = useSwitch
            ? await SwitchInLeagueAsync(SecondaryLeagueId, starter.Key, backup.Key)
            : await SaveAsync(submitted, leagueId: SecondaryLeagueId);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(starter.Position, saved.ActivePlayers.First(x => x.Key == starter.Key).Position);
        Assert.Equal(1, saved.FormationChanges);
        Assert.Single(saved.FormationChangeHistory);
        Assert.Equal(1, (await GetSecondaryGameAsync()).LiveFormationChangesRemaining);

        // Consuming the last change in the main league must also exhaust the secondary view.
        var secondResponse = await SwitchAsync(starter.Key, backup.Key);
        Assert.Contains("\"i\":true", await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, (await GetSecondaryGameAsync()).LiveFormationChangesRemaining);
        var thirdResponse = await SwitchInLeagueAsync(SecondaryLeagueId, starter.Key, backup.Key);
        Assert.Contains("numero massimo", await thirdResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secondary_league_does_not_allow_non_owner_changes(bool useSwitch)
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1), includeLeagueWithoutLiveChanges: true);
        _client.DefaultRequestHeaders.Add("X-Test-User", Admin);
        var team = CreateTeam(Owner, "Home");
        var starter = team.ActivePlayers.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = team.ActivePlayers.First(x => x.Position == FantaSoccerRole.FirstBackupForward);

        var response = useSwitch
            ? await SwitchInLeagueAsync(SecondaryLeagueId, starter.Key, backup.Key)
            : await SaveAsync(team, leagueId: SecondaryLeagueId);

        Assert.Contains("Non sei l'owner", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, (await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken))?.FormationChanges);
    }

    [Fact]
    public async Task Secondary_league_uses_current_co_owners_from_source_basket()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1), includeLeagueWithoutLiveChanges: true);
        var group = await _groups.GetAsync(GroupId, TestContext.Current.CancellationToken);
        group!.Baskets.Single().Years.Single().Teams.First(x => x.Owner == Owner).AdditionalOwners = [Admin.ToUpperInvariant()];
        await _groups.UpdateAsync(GroupId, group, TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Add("X-Test-User", Admin);

        var game = await GetSecondaryGameAsync();
        Assert.NotNull(game.Teams);
        Assert.Contains(Admin.ToUpperInvariant(), game.Teams.First(x => x.Owner == Owner).AdditionalOwners);
        Assert.Equal(2, game.LiveFormationChangesRemaining);
        var response = await SaveAsync(CreateTeam(Owner, "Home"), leagueId: SecondaryLeagueId);
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secondary_league_cannot_bypass_disabled_source_changes(bool useSwitch)
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1), includeLeagueWithoutLiveChanges: true);
        var group = await _groups.GetAsync(GroupId, TestContext.Current.CancellationToken);
        group!.Leagues.First(x => x.Id == LeagueId).Years.Single().Settings!.LiveFormationChanges = 0;
        group.Leagues.First(x => x.Id == SecondaryLeagueId).Years.Single().Settings!.LiveFormationChanges = 5;
        await _groups.UpdateAsync(GroupId, group, TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);

        Assert.False((await GetSecondaryGameAsync()).CanEdit);
        var team = CreateTeam(Owner, "Home");
        var response = useSwitch
            ? await SwitchInLeagueAsync(SecondaryLeagueId,
                team.ActivePlayers.First(x => x.Position == FantaSoccerRole.Forward).Key,
                team.ActivePlayers.First(x => x.Position == FantaSoccerRole.FirstBackupForward).Key)
            : await SaveAsync(team, leagueId: SecondaryLeagueId);
        Assert.Contains("non sono abilitate", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secondary_league_cannot_bypass_live_deadline(bool useSwitch)
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-5), includeLeagueWithoutLiveChanges: true);
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        Assert.False((await GetSecondaryGameAsync()).CanEdit);
        var team = CreateTeam(Owner, "Home");
        var response = useSwitch
            ? await SwitchInLeagueAsync(SecondaryLeagueId,
                team.ActivePlayers.First(x => x.Position == FantaSoccerRole.Forward).Key,
                team.ActivePlayers.First(x => x.Position == FantaSoccerRole.FirstBackupForward).Key)
            : await SaveAsync(team, leagueId: SecondaryLeagueId);
        Assert.Contains("finestra per le modifiche live è terminata", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Switch_live_players_swaps_only_the_selected_pair()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var before = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        var starter = before!.ActivePlayers.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = before.ActivePlayers.First(x => x.Position == FantaSoccerRole.FirstBackupForward);

        var response = await SwitchAsync(starter.Key, backup.Key);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(FantaSoccerRole.FirstBackupForward, saved.ActivePlayers.First(x => x.Key == starter.Key).Position);
        Assert.Equal(FantaSoccerRole.Forward, saved.ActivePlayers.First(x => x.Key == backup.Key).Position);
        Assert.Equal(1, saved.FormationChanges);
        Assert.Equal(2, Assert.Single(saved.FormationChangeHistory).Players.Count);
    }

    [Fact]
    public async Task Switch_live_players_rejects_different_main_roles()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var before = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        var midfielder = before!.ActivePlayers.First(x => x.Role == Role.Midfielder);
        var forward = before.ActivePlayers.First(x => x.Role == Role.Forward);

        var response = await SwitchAsync(midfielder.Key, forward.Key);

        response.EnsureSuccessStatusCode();
        Assert.Contains("stesso ruolo principale", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        Assert.Equal(0, saved?.FormationChanges);
    }

    [Fact]
    public async Task Switch_live_players_rejects_an_exhausted_limit()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1));
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var before = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        before!.FormationChanges = 2;
        await _teams.UpdateAsync(TeamKey(Owner), before, TestContext.Current.CancellationToken);
        var starter = before.ActivePlayers.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = before.ActivePlayers.First(x => x.Position == FantaSoccerRole.FirstBackupForward);

        var response = await SwitchAsync(starter.Key, backup.Key);

        response.EnsureSuccessStatusCode();
        Assert.Contains("numero massimo", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = await _teams.GetAsync(TeamKey(Owner), TestContext.Current.CancellationToken);
        Assert.Equal(2, saved?.FormationChanges);
        Assert.Empty(saved?.FormationChangeHistory ?? []);
    }

    private async Task<GameWrapper> GetSecondaryGameAsync()
    {
        var response = await _client.GetAsync(
            $"/Game/Get?groupId={GroupId}&leagueId={SecondaryLeagueId}&year={FantacalcioTime.ActualYear}&gameId={GameId}&editMode=live",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<GameWrapper>(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
    }

    private Task<HttpResponseMessage> SaveAsync(Team team, string editMode = "live", bool asAdmin = false, string leagueId = LeagueId)
        => _client.PostAsync(
            $"/Game/SaveTeam?groupId={GroupId}&leagueId={leagueId}&year={FantacalcioTime.ActualYear}&gameId={GameId}&editMode={editMode}&asAdmin={asAdmin}",
            new StringContent(JsonSerializer.Serialize(team), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> SwitchAsync(params string[] playerKeys)
        => SwitchInLeagueAsync(LeagueId, playerKeys);

    private Task<HttpResponseMessage> SwitchInLeagueAsync(string leagueId, params string[] playerKeys)
        => _client.PostAsync(
            $"/Game/Switch?groupId={GroupId}&leagueId={leagueId}&year={FantacalcioTime.ActualYear}&gameId={GameId}&asAdmin=false",
            new StringContent(JsonSerializer.Serialize(new { playerKeys }), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    private async Task SeedLiveGameAsync(DateTime gameStart, bool includeLeagueWithoutLiveChanges = false)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var settings = LeagueSetting.Default;
        settings.LiveFormationChanges = 2;
        var leagues = new List<League>
        {
            new()
            {
                Id = LeagueId,
                IsMain = true,
                Name = "Game league",
                Type = LeagueType.League,
                BasketsId = [BasketId],
                Years = [new AnnualLeague { Year = FantacalcioTime.ActualYear, Settings = settings }]
            }
        };
        if (includeLeagueWithoutLiveChanges)
        {
            // Another main league in the same group must not supply this basket's edit rules.
            leagues.Insert(0, new League
            {
                Id = "game-api-unrelated-main-league",
                Name = "Other main league",
                IsMain = true,
                Type = LeagueType.League,
                BasketsId = ["another-basket"],
                Years = [new AnnualLeague { Year = FantacalcioTime.ActualYear, Settings = LeagueSetting.Default }]
            });
            leagues.Add(new League
            {
                Id = SecondaryLeagueId,
                Name = "Restrictive league",
                Type = LeagueType.League,
                BasketsId = [BasketId],
                Years = [new AnnualLeague { Year = FantacalcioTime.ActualYear, Settings = LeagueSetting.Default }]
            });
        }
        await _users.InsertAsync(Owner, new AppIdentity
        {
            Username = "Game Owner",
            Email = Owner,
            Groups = [new GroupForIdentity { Id = GroupId, Name = "Game group", Role = IdentityRole.Participant }]
        }, cancellationToken);
        await _users.InsertAsync(Admin, new AppIdentity
        {
            Username = "Game Admin",
            Email = Admin,
            Groups = [new GroupForIdentity { Id = GroupId, Name = "Game group", Role = IdentityRole.SuperAdmin }]
        }, cancellationToken);
        await _groups.InsertAsync(GroupId, new Group
        {
            Id = GroupId,
            Name = "Game group",
            Users =
            [
                new UserOfAGroup { Username = "Game Owner", Email = Owner, Role = IdentityRole.Participant },
                new UserOfAGroup { Username = "Game Admin", Email = Admin, Role = IdentityRole.SuperAdmin }
            ],
            Leagues = leagues,
            Baskets =
            [
                new Basket
                {
                    Id = BasketId,
                    Name = "Game basket",
                    Years =
                    [
                        new YearlyBasket
                        {
                            Year = FantacalcioTime.ActualYear,
                            Teams =
                            [
                                new AnnualTeam { Name = "Home", Owner = Owner, AdditionalOwners = [] },
                                new AnnualTeam { Name = "Away", Owner = Opponent, AdditionalOwners = [] }
                            ]
                        }
                    ]
                }
            ]
        }, cancellationToken);
        await _calendars.InsertAsync(CalendarKey(), new Calendar
        {
            Year = FantacalcioTime.ActualYear,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["round"] =
                [
                    new Day
                    {
                        SerieADay = 7,
                        Number = 1,
                        Games =
                        [
                            new CalendarGame
                            {
                                Id = GameId,
                                Number = 1,
                                Home = "Home",
                                HomeOwner = Owner,
                                Away = "Away",
                                AwayOwner = Opponent
                            }
                        ]
                    }
                ]
            }
        }, cancellationToken);
        if (includeLeagueWithoutLiveChanges)
        {
            var calendar = await _calendars.GetAsync(CalendarKey(), cancellationToken);
            await _calendars.InsertAsync(CalendarKey(SecondaryLeagueId), calendar!, cancellationToken);
        }
        await _realCalendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                .. Enumerable.Range(1, 6).Select(day => new RealDay
                {
                    Year = FantacalcioTime.ActualYear,
                    SerieADay = day,
                    Games = [new RealGame { Date = gameStart.AddDays(-day - 1) }]
                }),
                new RealDay { Year = FantacalcioTime.ActualYear, SerieADay = 7, Games = [new RealGame { Date = gameStart }] }
            ]
        }, cancellationToken);
        await _teams.InsertAsync(TeamKey(Owner), CreateTeam(Owner, "Home"), cancellationToken);
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await _teams.DeleteAsync(TeamKey(Owner), cancellationToken);
        await _calendars.DeleteAsync(CalendarKey(), cancellationToken);
        await _calendars.DeleteAsync(CalendarKey(SecondaryLeagueId), cancellationToken);
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await _users.DeleteAsync(Owner, cancellationToken);
        await _users.DeleteAsync(Admin, cancellationToken);
    }

    private static LeagueKey CalendarKey(string leagueId = LeagueId)
        => new() { Group = GroupId, League = leagueId, Year = FantacalcioTime.ActualYear };

    private static TeamDayKey TeamKey(string owner)
        => new() { Group = GroupId, BasketId = BasketId, Year = FantacalcioTime.ActualYear, Day = 7, Email = owner };

    private static Team CreateTeam(string owner, string name)
    {
        var players = new List<Player>();
        Add(Role.GoalKeeper, FantaSoccerRole.GoalKeeper, 1);
        Add(Role.Defensor, FantaSoccerRole.Defensor, 3);
        Add(Role.Midfielder, FantaSoccerRole.Midfielder, 4);
        Add(Role.Forward, FantaSoccerRole.Forward, 3);
        Add(Role.Forward, FantaSoccerRole.FirstBackupForward, 1);
        return new Team { Name = name, Owner = owner, AdditionalOwners = [], Players = players };

        void Add(Role role, FantaSoccerRole position, int count)
        {
            players.AddRange(Enumerable.Range(1, count).Select(index => new Player
            {
                Name = $"{role}-{position}-{(char)('A' + index - 1)}",
                Role = role,
                Position = position,
                Status = PlayerInTeamStatus.Active,
                RealTeam = new RealTeam { Name = "Roma" }
            }));
        }
    }
}
