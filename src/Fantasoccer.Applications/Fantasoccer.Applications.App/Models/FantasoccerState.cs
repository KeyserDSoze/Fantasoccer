using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Authorization;
using RepositoryFramework;

namespace Fantasoccer.Applications.App
{
    public class FantasoccerStateManager
    {
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly IRepository<FantasoccerState, string> _repository;
        public FantasoccerStateManager(AuthenticationStateProvider authenticationStateProvider,
            IRepository<FantasoccerState, string> repository)
        {
            _authenticationStateProvider = authenticationStateProvider;
            _repository = repository;
        }
    }
    public class FantasoccerState
    {
        public AppIdentity? Identity { get; private set; }
        public string? GroupId => Identity?.State?.GroupId ?? Identity?.Groups?.FirstOrDefault()?.Id;
        public string? LeagueId => Identity?.State?.LeagueId ?? Identity?.Groups?.FirstOrDefault()?.Leagues?.FirstOrDefault()?.Id;
        public int Year => Identity?.State?.Year ?? FantacalcioTime.ActualYear;
        public string? MeOrLeaderEmail => Identity?.State?.MeOrLeaderEmail ?? Identity?.Email;
        public bool IsAuthenticated => Identity != null;
        public Task ChangeTeamAsync(string groupId, string leagueId, string email)
        {
            if (IsAuthenticated)
            {
                Identity!.State.GroupId = groupId;
                Identity.State.LeagueId = leagueId;
                Identity.State.MeOrLeaderEmail = email;
                Identity.State.Year = FantacalcioTime.ActualYear;
                UpdateUi();
                return UpdateAsync();
            }
            return Task.CompletedTask;
        }
        public Task ChangeYearAsync(int year)
        {
            if (IsAuthenticated)
            {
                Identity!.State.Year = year;
                UpdateUi();
                return UpdateAsync();
            }
            return Task.CompletedTask;
        }
        private async ValueTask LoadGroupAsync(string? groupId)
        {
            if (groupId != null && GetGroup != null)
            {
                Group = await GetGroup(groupId);
            }
        }
        public async ValueTask SetIdentityAsync(AppIdentity? identity)
        {
            Identity = identity;
            var groupId = Identity?.State?.GroupId ?? Identity?.Groups.FirstOrDefault()?.Id;
            if (groupId != null)
            {
                await LoadGroupAsync(groupId);
                UpdateUi();
            }
        }
        public async ValueTask ChangeGroupAsync(string? groupId)
        {
            if (groupId != null && groupId != GroupId && GetGroup != null)
            {
                await LoadGroupAsync(groupId);
                if (Identity?.State?.GroupId != null)
                {
                    Identity!.State.GroupId = groupId;
                    Identity!.State.LeagueId = Identity.Groups?.Find(x => x.Id == groupId)?.Leagues?.FirstOrDefault()?.Id;
                    Identity!.State.MeOrLeaderEmail = Identity.Email;
                    Identity!.State.Year = FantacalcioTime.ActualYear;
                    await UpdateAsync();
                }
                UpdateUi();
            }
        }
        public Task ChangeLeagueAsync(string leagueId)
        {
            if (IsAuthenticated)
            {
                Identity!.State.LeagueId = leagueId;
                Identity!.State.MeOrLeaderEmail = Identity.Email;
                Identity!.State.Year = FantacalcioTime.ActualYear;
                UpdateUi();
                return UpdateAsync();
            }
            return Task.CompletedTask;
        }
        public Group? Group { get; private set; }
        public bool HasAGroup => Group != null;
        public LeagueSetting? LeagueSettings => AnnualLeague?.Settings;
        public League? League => LeagueId != null ? Group?.Leagues.Find(x => x.Id == LeagueId) : null;
        public bool HasALeague => League != null;
        public AnnualLeague? AnnualLeague => League?.Years.Find(x => x.Year == Year);
        public Basket? Basket
        {
            get
            {
                var league = AnnualLeague;
                if (league != null && Group?.Baskets != null)
                {
                    return Group.Baskets.Find(x => league.BasketsId.Contains(x.Id));
                }
                return null;
            }
        }
        public YearlyBasket? YearlyBasket
        {
            get
            {
                var years = Basket?.Years;
                if (years != null)
                {
                    return years.Find(x => x.Year == Year);
                }
                return null;
            }
        }
        public List<AnnualTeam>? Teams => YearlyBasket?.Teams;
        public IEnumerable<AnnualTeam>? MyTeams => Identity != null ? Teams?.Where(x => x.IsOwner(Identity.Email)) : null;
        public required Action AppUiUpdate { get; init; }
        public void UpdateUi()
        {
            AppUiUpdate?.Invoke();
        }
        public required Func<AppIdentity, Task> UpdateState { get; init; }
        public Task UpdateAsync()
        {
            if (Identity != null)
                return UpdateState(Identity);
            return Task.CompletedTask;
        }
        public Func<string, Task<Group?>>? GetGroup { get; set; }
        public LeagueKey LeagueKey => new() { Group = GroupId!, League = LeagueId!, Year = Year };
        public TeamKey TeamKey => new()
        {
            Group = GroupId!,
            Year = Year,
            Email = MeOrLeaderEmail!,
            BasketId = Basket?.Id!
        };
        public bool IsAdmin()
        {
            if (Identity == null)
                return false;
            return Group?.AdminUsers.Any(x => x.Email == Identity.Email) == true;
        }
        public AnnualLeague? GetLeague(Group group)
        {
            var league = group.Leagues.Find(x => x.Id == LeagueId);
            if (league != null)
            {
                var yearLeague = league.Years.Find(x => x.Year == Year);
                return yearLeague;
            }
            return default;
        }
        public TeamKey OtherTeamKey(string email) => new()
        {
            Group = GroupId!,
            Year = Year,
            Email = email,
            BasketId = Basket?.Id!,
        };
        public TeamDayKey TeamDayKey(int day, string? email = null) => new()
        {
            Group = GroupId!,
            Email = email ?? MeOrLeaderEmail!,
            BasketId = Basket?.Id!,
            Year = Year,
            Day = day
        };
        public YearDayKey YearDayKey(int day) => new()
        {
            Day = day,
            Year = Year
        };
    }
}
