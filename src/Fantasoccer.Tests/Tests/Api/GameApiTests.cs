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
    public async Task Save_live_team_uses_selected_league_rule_for_shared_basket()
    {
        await SeedLiveGameAsync(FantacalcioTime.ItalyNow.AddHours(-1), includeLeagueWithoutLiveChanges: true);
        _client.DefaultRequestHeaders.Add("X-Test-User", Owner);

        var response = await SaveAsync(CreateTeam(Owner, "Home"));

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private Task<HttpResponseMessage> SaveAsync(Team team, string editMode = "live", bool asAdmin = false)
        => _client.PostAsync(
            $"/Game/SaveTeam?groupId={GroupId}&leagueId={LeagueId}&year={FantacalcioTime.ActualYear}&gameId={GameId}&editMode={editMode}&asAdmin={asAdmin}",
            new StringContent(JsonSerializer.Serialize(team), Encoding.UTF8, "application/json"),
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
                Name = "Game league",
                Type = LeagueType.League,
                BasketsId = [BasketId],
                Years = [new AnnualLeague { Year = FantacalcioTime.ActualYear, Settings = settings }]
            }
        };
        if (includeLeagueWithoutLiveChanges)
        {
            leagues.Add(new League
            {
                Id = "game-api-restrictive-league",
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
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await _users.DeleteAsync(Owner, cancellationToken);
        await _users.DeleteAsync(Admin, cancellationToken);
    }

    private static LeagueKey CalendarKey()
        => new() { Group = GroupId, League = LeagueId, Year = FantacalcioTime.ActualYear };

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
