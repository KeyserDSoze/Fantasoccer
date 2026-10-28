using System.Globalization;
using System.Text.Json;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using RepositoryFramework;

namespace Fantasoccer.DataMigration;

public sealed partial class CoreDatasetImporter
{
    private static void LoadFantasyCompetitions(VerifiedJsonDataset dataset, CorePlan plan, Func<string, string, Guid> map,
        Func<RealPlayer, int, bool, PlayerSource, PlayerSeason> player, Func<RealTeam?, ClubSource, string> club)
    {
        Guid Existing(string kind, string key) => plan.Maps.SingleOrDefault(x => x.Kind == kind && x.LegacyKey == key)?.TargetId
            ?? throw new CoreImportException("missing_competition_reference");
        (Guid Group, Guid League) Scope(LeagueKey key)
        {
            var group = Existing("group", key.Group);
            var league = Existing("league", Composite(key.Group, key.League));
            if (!plan.LeagueSeasons.Any(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year))
                throw new CoreImportException("missing_competition_season");
            return (group, league);
        }
        FantasyTeamSeason Resolve(Guid group, Guid league, int year, string owner, LegacyJsonDocument document, string jsonPath)
        {
            var user = ExistingOwner(plan, group, owner, "missing_competition_owner");
            var baskets = plan.Participations.Where(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == year).Select(x => x.BasketId).ToHashSet();
            var teams = plan.Owners.Where(x => x.GroupId == group && x.SeasonIndex == year && x.UserId == user.Id && x.IsPrimary && baskets.Contains(x.BasketId))
                .Select(owner => plan.Teams.Single(team => team.Id == owner.TeamId)).ToArray();
            return teams.Length == 1 ? teams[0] : throw UnresolvedTeam(document, jsonPath);
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "marketwrapper"))
        {
            var entity = document.Envelope.Deserialize<Entity<MarketWrapper, LeagueKey>>() ?? throw new CoreImportException("invalid_market_envelope");
            var key = entity.Key ?? throw new CoreImportException("missing_competition_key");
            var (group, league) = Scope(key);
            if (plan.MarketBooks.Any(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year)) throw new CoreImportException("market_book_collision");
            plan.MarketBooks.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = key.Year });
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var ordinal = 0;
            foreach (var value in entity.Value?.Markets ?? throw new CoreImportException("missing_market_trades"))
            {
                if (string.IsNullOrWhiteSpace(value.Id) || !ids.Add(value.Id) || (int)value.Status is < 0 or > 6) throw new CoreImportException("invalid_market_trade");
                var buyer = Resolve(group, league, key.Year, value.Buyer, document, $"$.Value.m[{ordinal}].b");
                var seller = Resolve(group, league, key.Year, value.Seller, document, $"$.Value.m[{ordinal}].s");
                var trade = new MarketTrade { GroupId = group, LeagueId = league, SeasonIndex = key.Year,
                    Id = map("market", Composite(key.Group, key.League, key.Year.ToString(CultureInfo.InvariantCulture), value.Id)), Ordinal = ordinal++,
                    BuyerTeamId = buyer.Id, BuyerBasketId = buyer.BasketId, SellerTeamId = seller.Id, SellerBasketId = seller.BasketId,
                    BuyerOwner = value.Buyer, SellerOwner = value.Seller, MoneyFromBuyer = value.MoneyFromBuyer, MoneyFromSeller = value.MoneyFromSeller,
                    Status = (int)value.Status, SellerAccepted = value.SellerAccepted, LegacyCreatedUtc = Local(value.CreationTime),
                    CreatedAtUtc = new(DateTime.SpecifyKind(value.CreationTime, DateTimeKind.Utc)) };
                foreach (var (fromBuyer, rows) in new[] { (true, value.BuyerPlayers), (false, value.SellerPlayers) })
                    foreach (var (item, index) in (rows ?? []).Select((item, index) => (item, index)))
                    {
                        var origin = PlayerOrigin(document, $".m[{trade.Ordinal}].{(fromBuyer ? "bp" : "sp")}[{index}]");
                        var identity = player(item, key.Year, false, origin);
                        trade.Players.Add(new() { FromBuyer = fromBuyer, Ordinal = index, PlayerId = identity.PlayerId, ClubId = club(item.RealTeam, ClubOrigin(origin)),
                            PlayerRole = (int)item.Role, IsActive = item.IsActive, IsVisible = item.Visible, Price = item.Price, Revenue = item.Revenue,
                            Status = (int)item.Status, Position = (int)item.Position });
                    }
                foreach (var (approved, owners) in new[] { (true, value.Approvers), (false, value.Deniers) })
                    foreach (var (ownerEmail, index) in (owners ?? []).Select((value, index) => (value, index)))
                    {
                        var voter = Resolve(group, league, key.Year, ownerEmail, document, $"$.Value.m[{trade.Ordinal}].{(approved ? "a" : "d")}[{index}]");
                        if (trade.Votes.Any(x => x.TeamId == voter.Id)) throw new CoreImportException("market_vote_collision");
                        trade.Votes.Add(new() { TeamId = voter.Id, BasketId = voter.BasketId, OwnerEmail = ownerEmail, Approved = approved, Ordinal = trade.Votes.Count });
                    }
                plan.MarketTrades.Add(trade);
            }
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "randomizedplayers"))
        {
            var entity = document.Envelope.Deserialize<Entity<RealPlayersWrapper, LeagueKey>>() ?? throw new CoreImportException("invalid_randomized_players");
            var key = entity.Key ?? throw new CoreImportException("missing_competition_key");
            var (group, league) = Scope(key);
            if (plan.PlayerOrders.Any(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year)) throw new CoreImportException("player_order_collision");
            var order = new LeaguePlayerOrder { GroupId = group, LeagueId = league, SeasonIndex = key.Year };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (item, index) in (entity.Value?.Players ?? throw new CoreImportException("missing_randomized_players")).Select((value, index) => (value, index)))
            {
                var origin = PlayerOrigin(document, $".p[{index}]");
                var identity = player(item, key.Year, false, origin);
                if (!ids.Add(identity.PlayerId)) throw new CoreImportException("duplicate_player_in_order");
                order.Players.Add(new() { Ordinal = order.Players.Count, PlayerId = identity.PlayerId, ClubId = club(item.RealTeam, ClubOrigin(origin)),
                    PlayerRole = (int)item.Role, IsActive = item.IsActive, IsVisible = item.Visible });
            }
            plan.PlayerOrders.Add(order);
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "calendar"))
        {
            var entity = document.Envelope.Deserialize<Entity<Calendar, LeagueKey>>() ?? throw new CoreImportException("invalid_fantasy_calendar");
            var key = entity.Key ?? throw new CoreImportException("missing_competition_key");
            var (group, league) = Scope(key);
            var value = entity.Value ?? throw new CoreImportException("missing_fantasy_calendar");
            if (value.Year != key.Year || plan.Calendars.Any(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year))
                throw new CoreImportException("fantasy_calendar_collision");
            var calendar = new FantasyCalendar { GroupId = group, LeagueId = league, SeasonIndex = key.Year };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (stageKey, days) in value.Rounds ?? [])
            {
                var stage = new FantasyStage { GroupId = group, LeagueId = league, SeasonIndex = key.Year, Stage = stageKey, Ordinal = calendar.Stages.Count };
                foreach (var day in days ?? throw new CoreImportException("missing_fantasy_days"))
                {
                    if (day.SerieADay is < 1 or > 38) throw new CoreImportException("invalid_fantasy_matchday");
                    var row = new FantasyMatchday { GroupId = group, LeagueId = league, SeasonIndex = key.Year, Stage = stageKey,
                        Ordinal = stage.Days.Count, FantasyTurn = day.Number, SerieADay = day.SerieADay };
                    foreach (var game in day.Games ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(game.Id) || !ids.Add(game.Id)) throw new CoreImportException("fantasy_game_collision");
                        var home = string.IsNullOrWhiteSpace(game.HomeOwner) ? null : Resolve(group, league, key.Year, game.HomeOwner, document, $"$.Value.r.{stageKey}[{stage.Days.Count}].g[{row.Games.Count}].o");
                        var away = string.IsNullOrWhiteSpace(game.AwayOwner) ? null : Resolve(group, league, key.Year, game.AwayOwner, document, $"$.Value.r.{stageKey}[{stage.Days.Count}].g[{row.Games.Count}].u");
                        row.Games.Add(new() { GroupId = group, LeagueId = league, SeasonIndex = key.Year,
                            Id = map("match", Composite(key.Group, key.League, key.Year.ToString(CultureInfo.InvariantCulture), game.Id)),
                            Stage = stageKey, DayOrdinal = row.Ordinal, Ordinal = row.Games.Count, Number = game.Number,
                            HomeBasketId = home?.BasketId, HomeTeamId = home?.Id, AwayBasketId = away?.BasketId, AwayTeamId = away?.Id,
                            HomeName = game.Home ?? "", AwayName = game.Away ?? "", HomeOwner = game.HomeOwner, AwayOwner = game.AwayOwner,
                            Result = ImportResult(game.Result) });
                    }
                    stage.Days.Add(row);
                }
                calendar.Stages.Add(stage);
            }
            plan.Calendars.Add(calendar);
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "rank"))
        {
            var entity = document.Envelope.Deserialize<Entity<Rank, LeagueKey>>() ?? throw new CoreImportException("invalid_rank");
            var key = entity.Key ?? throw new CoreImportException("missing_competition_key");
            var value = entity.Value ?? throw new CoreImportException("missing_rank");
            var (group, league) = Scope(key);
            if (value.SerieADay is < 0 or > 38 || plan.Standings.Any(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year))
                throw new CoreImportException("rank_collision");
            var snapshot = new FantasyStandingSnapshot { GroupId = group, LeagueId = league, SeasonIndex = key.Year, SnapshotDay = 0, SerieADay = value.SerieADay };
            foreach (var (stageKey, teams) in value.RankedTeams ?? [])
            {
                var stage = new FantasyStandingStage { Stage = stageKey, Ordinal = snapshot.Stages.Count };
                var ids = new HashSet<Guid>();
                foreach (var team in teams ?? throw new CoreImportException("missing_rank_teams"))
                {
                    var metadata = Resolve(group, league, key.Year, team.Owner, document, $"$.Value.r.{stageKey}[{stage.Teams.Count}].o");
                    if (!ids.Add(metadata.Id) || !new[] { team.Point, team.ValuePoint, team.SufferedValuePoint }.All(float.IsFinite))
                        throw new CoreImportException("invalid_rank_team");
                    stage.Teams.Add(new() { Ordinal = stage.Teams.Count, BasketId = metadata.BasketId, TeamId = metadata.Id,
                        Name = team.Name, Owner = team.Owner, Points = team.Point, Victories = team.Victories, Draws = team.Draws, Defeats = team.Defeats,
                        GoalsFor = team.Goal, GoalsAgainst = team.SufferedGoal, ValuePoints = team.ValuePoint, SufferedValuePoints = team.SufferedValuePoint,
                        Money = team.Money, PlusMoney = team.PlusMoney });
                }
                snapshot.Stages.Add(stage);
            }
            plan.Standings.Add(snapshot);
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "openingstandings"))
        {
            var entity = document.Envelope.Deserialize<Entity<OpeningCompetitionStandings, OpeningCompetitionKey>>() ?? throw new CoreImportException("invalid_opening_standings");
            var key = entity.Key ?? throw new CoreImportException("missing_competition_key");
            var value = entity.Value ?? throw new CoreImportException("missing_opening_standings");
            var (group, league) = Scope(new() { Group = key.Group, League = key.League, Year = key.Year });
            if (key.Year != value.Year || key.Group != value.Group || key.League != value.League || value.SerieADays is < 1 or > 38 ||
                plan.OpeningStandings.Any(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year)) throw new CoreImportException("opening_standings_collision");
            var snapshot = new OpeningStandingSnapshot { GroupId = group, LeagueId = league, SeasonIndex = key.Year, SerieADays = value.SerieADays };
            var baskets = plan.LeagueBaskets.Where(x => x.GroupId == group && x.LeagueId == league).Select(x => x.BasketId).ToHashSet();
            foreach (var item in value.Teams)
            {
                var owner = ExistingOwner(plan, group, item.Owner, "missing_opening_owner");
                var owners = plan.Owners.Where(x => x.GroupId == group && x.SeasonIndex == key.Year - 1 && x.UserId == owner.Id && x.IsPrimary && baskets.Contains(x.BasketId)).ToArray();
                if (owners.Length != 1 || snapshot.Teams.Any(x => x.TeamId == owners[0].TeamId) || !float.IsFinite(item.Score))
                    throw UnresolvedTeam(document, $"$.Value.t[{snapshot.Teams.Count}].o");
                snapshot.Teams.Add(new() { TeamId = owners[0].TeamId, BasketId = owners[0].BasketId, TeamOriginYear = key.Year - 1, Ordinal = snapshot.Teams.Count,
                    Owner = item.Owner, Name = item.Name, Score = item.Score, Prize = item.Prize, Position = item.Position });
            }
            plan.OpeningStandings.Add(snapshot);
        }
        foreach (var document in dataset.Documents.Where(x => x.Container == "playedgame"))
        {
            var entity = document.Envelope.Deserialize<Entity<PlayedGame, GameKey>>() ?? throw new CoreImportException("invalid_played_game");
            var key = entity.Key ?? throw new CoreImportException("missing_competition_key");
            var (group, league) = Scope(new() { Group = key.Group, League = key.League, Year = key.Year });
            var value = entity.Value ?? throw new CoreImportException("missing_played_game");
            var id = Existing("match", Composite(key.Group, key.League, key.Year.ToString(CultureInfo.InvariantCulture), key.GameId));
            if (value.Id != key.GameId || key.Day is < 1 or > 38 || plan.PlayedMatches.Any(x => x.GroupId == group && x.LeagueId == league && x.SeasonIndex == key.Year && x.MatchId == id && x.Day == key.Day))
                throw new CoreImportException("played_game_collision");
            var match = plan.Calendars.SelectMany(x => x.Stages).SelectMany(x => x.Days).SelectMany(x => x.Games).Single(x => x.Id == id);
            var day = plan.Calendars.SelectMany(x => x.Stages).SelectMany(x => x.Days).Single(x => x.Games.Contains(match));
            if (day.SerieADay != key.Day) throw new CoreImportException("played_game_day_mismatch");
            var snapshot = new PlayedMatchSnapshot { GroupId = group, LeagueId = league, SeasonIndex = key.Year, MatchId = id, Day = key.Day,
                HomeName = value.Home, AwayName = value.Away, Result = ImportResult(value.Result) };
            foreach (var (isHome, rows) in new[] { (true, value.HomePlayers), (false, value.AwayPlayers) })
                foreach (var (item, ordinal) in (rows ?? []).Select((item, ordinal) => (item, ordinal)))
                {
                    var origin = PlayerOrigin(document, $"['{(isHome ? "1" : "2")}'][{ordinal}]");
                    var identity = player(item, key.Year, false, origin);
                    snapshot.Players.Add(new() { IsHome = isHome, Ordinal = ordinal, PlayerId = identity.PlayerId, ClubId = club(item.RealTeam, ClubOrigin(origin)),
                        PlayerRole = (int)item.Role, IsActive = item.IsActive, IsVisible = item.Visible, Price = item.Price, Revenue = item.Revenue,
                        Status = (int)item.Status, Position = (int)item.Position });
                }
            plan.PlayedMatches.Add(snapshot);
        }
    }

    private static SavedFantasyResult? ImportResult(GameResult? source)
    {
        if (source is null) return null;
        SavedFantasyPoint? Point(Point? point)
        {
            if (point is null) return null;
            if (!float.IsFinite(point.Value)) throw new CoreImportException("invalid_saved_result");
            return new() { Value = point.Value, DefensiveBonus = point.DefensiveBonus, GoodPeople = point.GoodPeople, OwnGoal = point.OwnGoal };
        }
        return new() { IsCancelled = source.IsCancelled, HomeGoals = source.HomeGoal, AwayGoals = source.AwayGoal, Home = Point(source.Home), Away = Point(source.Away) };
    }
}
