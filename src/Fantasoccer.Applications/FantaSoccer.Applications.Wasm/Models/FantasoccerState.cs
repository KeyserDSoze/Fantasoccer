namespace Fantasoccer.Applications.Wasm
{
    public class FantasoccerState
    {
        public AppIdentity? Identity { get; private set; }
        public string? GroupId => Identity?.State?.GroupId ?? Identity?.Groups?.FirstOrDefault()?.Id;
        public string? LeagueId => Identity?.State?.LeagueId ?? SelectedGroup?.Leagues?.FirstOrDefault()?.Id;
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
                if (SelectedYearlyLeague == null)
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
                Identity!.State.GroupId = GroupId;
                Identity!.State.LeagueId = LeagueId;
                Identity!.State.MeOrLeaderEmail = Identity.Email;
                Identity!.State.Year = year;
                UpdateUi();
                return UpdateAsync();
            }
            return Task.CompletedTask;
        }
        public async ValueTask SetIdentityAsync(AppIdentity? identity)
        {
            Identity = identity;
            if (GetGroup != null && Identity?.Groups != null)
            {
                var groups = new List<Group>();
                foreach (var group in Identity.Groups)
                {
                    var theGroup = await GetGroup(group.Id);
                    if (theGroup != null)
                        groups.Add(theGroup);
                }
                Groups = groups;
            }
            UpdateUi();
        }
        public async ValueTask ChangeGroupAsync(string? groupId)
        {
            if (groupId != null && groupId != GroupId && GetGroup != null && Identity != null)
            {
                Identity.State.GroupId = groupId;
                Identity.State.LeagueId = SelectedGroup?.Leagues.FirstOrDefault()?.Id;
                Identity.State.MeOrLeaderEmail = Identity.Email;
                if (SelectedYearlyLeague == null)
                    Identity.State.Year = FantacalcioTime.ActualYear;
                await UpdateAsync();
                UpdateUi();
            }
        }
        public Task ChangeLeagueAsync(string leagueId)
        {
            if (IsAuthenticated)
            {
                Identity!.State.GroupId = GroupId;
                Identity!.State.LeagueId = leagueId;
                Identity!.State.MeOrLeaderEmail = Identity.Email;
                if (SelectedYearlyLeague == null)
                    Identity.State.Year = FantacalcioTime.ActualYear;
                UpdateUi();
                return UpdateAsync();
            }
            return Task.CompletedTask;
        }
        public Group? SelectedGroup => Groups?.Find(x => x.Id == GroupId) ?? Groups?.FirstOrDefault();
        public IEnumerable<AnnualTeam> YourTeams => GetTeams(GroupId, LeagueId, Year, Identity?.Email);
        public IEnumerable<AnnualTeam> GetTeams(string? groupId, string? leagueId, int year, string? owner)
        {
            if (groupId != null && leagueId != null)
            {
                var group = Groups?.Find(x => x.Id == groupId);
                var league = group?.Leagues?.Find(x => x.Id == leagueId);
                if (league != null)
                {
                    foreach (var basketId in league.BasketsId)
                    {
                        var basket = group!.Baskets.Find(x => x.Id == basketId)?.Years?.Find(x => x.Year == year);
                        if (basket != null)
                        {
                            foreach (var team in basket.Teams)
                            {
                                if (owner == null || team.IsOwner(owner))
                                    yield return team;
                            }
                        }
                    }
                }
            }
        }
        public List<AnnualTeam>? GetTeamsFromBasket(string? groupId, string? basketId, int year)
        {
            if (groupId != null && basketId != null)
            {
                var group = Groups?.Find(x => x.Id == groupId);
                var basket = group?.Baskets?.Find(x => x.Id == basketId)?.Years?.Find(x => x.Year == year);
                if (basket != null)
                {
                    return basket.Teams;
                }
                else
                {
                    var basketX = group?.Baskets?.Find(x => x.Id == basketId);
                    if (basketX != null)
                    {
                        basketX.Years ??= new();
                        var actualYear = basketX.Years.Find(x => x.Year == year);
                        if (actualYear == null)
                        {
                            actualYear = new YearlyBasket { Teams = new(), Year = year };
                            basketX.Years.Add(actualYear);
                            return actualYear.Teams;
                        }
                    }
                }
            }
            return null;
        }
        public List<Group>? Groups { get; private set; }
        public bool HasAGroup => SelectedGroup != null;
        public LeagueSetting? SelectedLeagueSettings => SelectedYearlyLeague?.Settings;
        public League? SelectedLeague => LeagueId != null ? SelectedGroup?.Leagues.Find(x => x.Id == LeagueId) : null;
        public AnnualLeague? SelectedYearlyLeague => SelectedLeague?.Years?.Find(x => x.Year == Year);
        public bool HasALeague => SelectedLeague != null;
        public IEnumerable<Basket> SelectedBaskets
        {
            get
            {
                var league = SelectedLeague;
                if (league != null && SelectedGroup?.Baskets != null)
                {
                    foreach (var x in SelectedGroup.Baskets.Where(x => league.BasketsId.Contains(x.Id)))
                    {
                        yield return x;
                    }
                }
            }
        }
        public IEnumerable<YearlyBasket> SelectedYearlyBaskets
        {
            get
            {
                foreach (var basket in SelectedBaskets)
                {
                    var years = basket?.Years;
                    if (years != null)
                    {
                        var year = years.Find(x => x.Year == Year);
                        if (year != null)
                            yield return year;
                    }
                }
            }
        }
        public IEnumerable<AnnualTeam>? Teams
        {
            get
            {
                foreach (var basket in SelectedYearlyBaskets)
                {
                    foreach (var team in basket.Teams)
                        yield return team;
                }
            }
        }
        public IEnumerable<AnnualTeam>? MyTeams => Identity != null ? Teams?.Where(x => x.IsOwner(Identity.Email)) : null;
        public AnnualTeam? SelectedTeam => Identity != null ? MyTeams?.FirstOrDefault(x => x.Owner == MeOrLeaderEmail) : null;
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
        public LeagueKey GetLeagueKey(string leagueId) => new() { Group = GroupId!, League = leagueId, Year = Year };
        public TeamKey TeamKey => new()
        {
            Group = GroupId!,
            Year = Year,
            Email = MeOrLeaderEmail!,
            BasketId = SelectedBasketId
        };
        public string SelectedBasketId 
            => SelectedGroup!.GetBasketId(MeOrLeaderEmail!, Year!)!;
        public bool IsAdmin()
        {
            if (Identity == null)
                return false;
            return SelectedGroup?.AdminUsers.Any(x => x.Email == Identity.Email) == true;
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
            BasketId = SelectedGroup!.GetBasketId(email!, Year!)!,
        };
        public TeamDayKey TeamDayKey(int day, string? email = null) => new()
        {
            Group = GroupId!,
            Email = email ?? MeOrLeaderEmail!,
            BasketId = SelectedGroup!.GetBasketId(email ?? MeOrLeaderEmail!, Year!)!,
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
