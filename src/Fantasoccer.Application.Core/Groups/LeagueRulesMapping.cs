using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.Application.Core.Groups;

public static class LeagueRulesMapping
{
    public static LeagueSetting ToDomain(LeagueRules row) => new()
    {
        Votes = row.Votes.ToDictionary(x => (Role)x.PlayerRole, x => new VoteLeagueSetting { Goal = x.Goal, Penalty = x.Penalty,
            SufferedGoal = x.SufferedGoal, StoppedPenalty = x.StoppedPenalty, WrongedPenalty = x.WrongedPenalty, OwnGoal = x.OwnGoal,
            Assist = x.Assist, YellowCard = x.YellowCard, RedCard = x.RedCard, Injury = x.Injury, ManOfTheMatch = x.ManOfTheMatch }),
        Formation = (FormationType)row.Formation, StartingMoney = row.StartingMoney, DelayedDay = row.DelayedDay, CancelledDay = row.CancelledDay,
        PointForFirstGoal = row.PointForFirstGoal, PointForNextGoal = row.PointForNextGoal, PointForOwnGoal = row.PointForOwnGoal,
        DifferencePointForOwnGoal = row.DifferencePointForOwnGoal, PointInHome = row.PointInHome, PointForVictory = row.PointForVictory,
        PointForDefeat = row.PointForDefeat, PointForDraw = row.PointForDraw, PointForStrongDefense = row.PointForStrongDefense,
        PointForStrongDefense4 = row.PointForStrongDefense4, PointForStrongDefense5 = row.PointForStrongDefense5,
        PointForGoodPeople = row.PointForGoodPeople, PointForCleanSheet = row.PointForCleanSheet, MoneyForGoal = row.MoneyForGoal,
        MoneyForSufferedGoal = row.MoneyForSufferedGoal, RandomAuction = row.RandomAuction, RankWithValuePoints = row.RankWithValuePoints,
        Market = (MarketType)row.Market, LiveFormationChanges = row.LiveFormationChanges, AllowLiveModuleChange = row.AllowLiveModuleChange,
        Postponements = new() { WaitForRecovery = row.WaitForPostponedMatches, Goalkeeper = row.PoliticalGoalkeeper,
            Defender = row.PoliticalDefender, Midfielder = row.PoliticalMidfielder, Forward = row.PoliticalForward },
        OpeningCompetition = new() { Enabled = row.OpeningCompetitionEnabled, SerieADays = row.OpeningCompetitionSerieADays,
            Prizes = row.OpeningPrizes.OrderBy(x => x.Position).Select(x => new OpeningCompetitionPrize { Position = x.Position, Credits = x.Credits }).ToList() },
        Type = !row.HasTypeSettings ? null : new()
        {
            CalendarType = (LeagueCalendarType)row.CalendarType,
            Rounds = row.Rounds.OrderBy(x => x.Ordinal).Select(x => new LeagueRound { Name = x.Name!, Type = (RoundType)x.Type,
                FromStart = x.FromStart, FromRankingStartTeam = x.FromRankingStartTeam, FromRankingEndTeam = x.FromRankingEndTeam }).ToList(),
            Numbers = new() { MaxPlayersInTeam = row.MaxPlayersInTeam, MaxGoalKeepersInTeam = row.MaxGoalKeepersInTeam,
                MaxDefendersInTeam = row.MaxDefendersInTeam, MaxMidfieldersInTeam = row.MaxMidfieldersInTeam, MaxForwardsInTeam = row.MaxForwardsInTeam,
                MaxGoalKeepersInBench = row.MaxGoalKeepersInBench, MaxDefendersInBench = row.MaxDefendersInBench,
                MaxMidfieldersInBench = row.MaxMidfieldersInBench, MaxForwardsInBench = row.MaxForwardsInBench },
            FromPreviousYear = !row.HasPreviousYearSettings ? null : new() { LeaguesId = row.PreviousLeagues.Select(x => x.SourceLeagueId.ToString()).ToList(),
                MaxTeamsPerLeague = row.PreviousYearMaxTeamsPerLeague, RoundType = (RoundType)row.PreviousYearRoundType },
            CardTrainer = new() { MaxCardsPerType = row.CardLimits.ToDictionary(x => (CardType)x.CardType, x => x.Maximum) }
        }
    };

    public static LeagueSettingDto ToDto(LeagueRules row) => new()
    {
        Votes = row.Votes.ToDictionary(x => x.PlayerRole, x => new VoteWeightDto(x.Goal, x.Penalty, x.SufferedGoal, x.StoppedPenalty,
            x.WrongedPenalty, x.OwnGoal, x.Assist, x.YellowCard, x.RedCard, x.Injury, x.ManOfTheMatch)),
        Formation = row.Formation, StartingMoney = row.StartingMoney, DelayedDay = row.DelayedDay, CancelledDay = row.CancelledDay,
        PointForFirstGoal = row.PointForFirstGoal, PointForNextGoal = row.PointForNextGoal, PointForOwnGoal = row.PointForOwnGoal,
        DifferencePointForOwnGoal = row.DifferencePointForOwnGoal, PointInHome = row.PointInHome, PointForVictory = row.PointForVictory,
        PointForDefeat = row.PointForDefeat, PointForDraw = row.PointForDraw, PointForStrongDefense = row.PointForStrongDefense,
        PointForStrongDefense4 = row.PointForStrongDefense4, PointForStrongDefense5 = row.PointForStrongDefense5,
        PointForGoodPeople = row.PointForGoodPeople, PointForCleanSheet = row.PointForCleanSheet, MoneyForGoal = row.MoneyForGoal,
        MoneyForSufferedGoal = row.MoneyForSufferedGoal, RandomAuction = row.RandomAuction, RankWithValuePoints = row.RankWithValuePoints,
        Market = row.Market, LiveFormationChanges = row.LiveFormationChanges, AllowLiveModuleChange = row.AllowLiveModuleChange,
        Postponements = new(row.WaitForPostponedMatches, row.PoliticalGoalkeeper, row.PoliticalDefender, row.PoliticalMidfielder, row.PoliticalForward),
        OpeningCompetition = new(row.OpeningCompetitionEnabled, row.OpeningCompetitionSerieADays,
            row.OpeningPrizes.OrderBy(x => x.Position).Select(x => new OpeningPrizeDto(x.Position, x.Credits)).ToArray()),
        TypeSettings = !row.HasTypeSettings ? null : new(row.CalendarType,
            row.Rounds.OrderBy(x => x.Ordinal).Select(x => new LeagueRoundDto(x.Name, x.Type, x.FromStart, x.FromRankingStartTeam, x.FromRankingEndTeam)).ToArray(),
            new(row.MaxPlayersInTeam, row.MaxGoalKeepersInTeam, row.MaxDefendersInTeam, row.MaxMidfieldersInTeam, row.MaxForwardsInTeam,
                row.MaxGoalKeepersInBench, row.MaxDefendersInBench, row.MaxMidfieldersInBench, row.MaxForwardsInBench),
            !row.HasPreviousYearSettings ? null : new(row.PreviousLeagues.Select(x => x.SourceLeagueId).Order().ToArray(), row.PreviousYearMaxTeamsPerLeague, row.PreviousYearRoundType),
            new(row.CardLimits.ToDictionary(x => x.CardType, x => x.Maximum)))
    };

    public static LeagueRules FromDto(Guid groupId, Guid leagueId, int year, LeagueSettingDto dto)
    {
        if (dto.Votes is null || !dto.Votes.ContainsKey(-1) || dto.StartingMoney < 25 || dto.PointForFirstGoal < 1 ||
            dto.PointForNextGoal < 1 || dto.DelayedDay is < 0 or > 37 || dto.CancelledDay is < 0 or > 38 || dto.LiveFormationChanges < 0 ||
            dto.Formation is < 0 or > 1 || dto.Market is < 0 or > 2 || dto.OpeningCompetition is null || dto.Postponements is null ||
            !new[] { dto.Postponements.Goalkeeper, dto.Postponements.Defender, dto.Postponements.Midfielder, dto.Postponements.Forward }
                .All(value => float.IsFinite(value) && value is >= 0 and <= 10) ||
            dto.Votes.Any(x => x.Key is < -1 or > 3 || !new[] { x.Value.Goal, x.Value.Penalty, x.Value.SufferedGoal, x.Value.StoppedPenalty,
                x.Value.WrongedPenalty, x.Value.OwnGoal, x.Value.Assist, x.Value.YellowCard, x.Value.RedCard }.All(float.IsFinite)))
            throw new ApplicationFault(400, "invalid_league_settings", "Regolamento non valido.");
        var row = new LeagueRules
        {
            GroupId = groupId, LeagueId = leagueId, SeasonIndex = year,
            Formation = dto.Formation, StartingMoney = dto.StartingMoney, DelayedDay = dto.DelayedDay, CancelledDay = dto.CancelledDay,
            PointForFirstGoal = dto.PointForFirstGoal, PointForNextGoal = dto.PointForNextGoal, PointForOwnGoal = dto.PointForOwnGoal,
            DifferencePointForOwnGoal = dto.DifferencePointForOwnGoal, PointInHome = dto.PointInHome, PointForVictory = dto.PointForVictory,
            PointForDefeat = dto.PointForDefeat, PointForDraw = dto.PointForDraw, PointForStrongDefense = dto.PointForStrongDefense,
            PointForStrongDefense4 = dto.PointForStrongDefense4, PointForStrongDefense5 = dto.PointForStrongDefense5,
            PointForGoodPeople = dto.PointForGoodPeople, PointForCleanSheet = dto.PointForCleanSheet, MoneyForGoal = dto.MoneyForGoal,
            MoneyForSufferedGoal = dto.MoneyForSufferedGoal, RandomAuction = dto.RandomAuction, RankWithValuePoints = dto.RankWithValuePoints,
            Market = dto.Market, LiveFormationChanges = dto.LiveFormationChanges, AllowLiveModuleChange = dto.AllowLiveModuleChange,
            WaitForPostponedMatches = dto.Postponements.WaitForRecovery, PoliticalGoalkeeper = dto.Postponements.Goalkeeper,
            PoliticalDefender = dto.Postponements.Defender, PoliticalMidfielder = dto.Postponements.Midfielder, PoliticalForward = dto.Postponements.Forward,
            OpeningCompetitionEnabled = dto.OpeningCompetition.Enabled, OpeningCompetitionSerieADays = dto.OpeningCompetition.SerieADays,
            OpeningPrizes = dto.OpeningCompetition.Prizes.Select(x => new LeagueOpeningPrize { Position = x.Position, Credits = x.Credits }).ToArray(),
            Votes = dto.Votes.Select(x => new LeagueVoteWeight { PlayerRole = x.Key, Goal = x.Value.Goal, Penalty = x.Value.Penalty,
                SufferedGoal = x.Value.SufferedGoal, StoppedPenalty = x.Value.StoppedPenalty, WrongedPenalty = x.Value.WrongedPenalty,
                OwnGoal = x.Value.OwnGoal, Assist = x.Value.Assist, YellowCard = x.Value.YellowCard, RedCard = x.Value.RedCard,
                Injury = x.Value.Injury, ManOfTheMatch = x.Value.ManOfTheMatch }).ToArray()
        };
        if (dto.TypeSettings is { } type)
        {
            row.HasTypeSettings = true; row.CalendarType = type.CalendarType;
            row.MaxPlayersInTeam = type.Numbers.MaxPlayersInTeam; row.MaxGoalKeepersInTeam = type.Numbers.MaxGoalKeepersInTeam;
            row.MaxDefendersInTeam = type.Numbers.MaxDefendersInTeam; row.MaxMidfieldersInTeam = type.Numbers.MaxMidfieldersInTeam;
            row.MaxForwardsInTeam = type.Numbers.MaxForwardsInTeam; row.MaxGoalKeepersInBench = type.Numbers.MaxGoalKeepersInBench;
            row.MaxDefendersInBench = type.Numbers.MaxDefendersInBench; row.MaxMidfieldersInBench = type.Numbers.MaxMidfieldersInBench;
            row.MaxForwardsInBench = type.Numbers.MaxForwardsInBench;
            row.Rounds = type.Rounds.Select((x, i) => new LeagueStageRule { Ordinal = i, Name = x.Name, Type = x.Type,
                FromStart = x.FromStart, FromRankingStartTeam = x.FromRankingStartTeam, FromRankingEndTeam = x.FromRankingEndTeam }).ToArray();
            row.CardLimits = type.CardTrainer.MaxCardsPerType.Select(x => new LeagueCardLimit { CardType = x.Key, Maximum = x.Value }).ToArray();
            if (type.FromPreviousYear is { } previous)
            {
                row.HasPreviousYearSettings = true; row.PreviousYearMaxTeamsPerLeague = previous.MaxTeamsPerLeague; row.PreviousYearRoundType = previous.RoundType;
                row.PreviousLeagues = previous.LeaguesId.Distinct().Select(x => new PreviousLeagueQualification { SourceLeagueId = x }).ToArray();
            }
        }
        if (row.OpeningPrizes.GroupBy(x => x.Position).Any(x => x.Count() > 1) || row.OpeningPrizes.Any(x => x.Position < 1 || x.Credits < 0))
            throw new ApplicationFault(400, "invalid_opening_prizes", "Premi della competizione iniziale non validi.");
        return row;
    }

    public static LeagueRules Default(Guid groupId, Guid leagueId, int year)
    {
        var rules = new LeagueRules { GroupId = groupId, LeagueId = leagueId, SeasonIndex = year };
        rules.Votes.Add(new() { PlayerRole = -1, Goal = 3, Penalty = 3, SufferedGoal = -1, StoppedPenalty = 3,
            WrongedPenalty = -3, OwnGoal = -3, Assist = 1, YellowCard = -0.5f, RedCard = -1, ManOfTheMatch = 2 });
        rules.OpeningPrizes = [new() { Position = 1, Credits = 50 }, new() { Position = 2, Credits = 30 }, new() { Position = 3, Credits = 20 }];
        return rules;
    }
}
