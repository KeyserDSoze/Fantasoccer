using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.DataMigration;

internal static class LegacyRulesMapping
{
    public static LeagueRules Convert(Guid groupId, Guid leagueId, int year, LeagueSetting settings, Func<string, Guid> sourceLeague)
    {
        if (settings is null || settings.Votes is null) throw new CoreImportException("missing_league_settings");
        var row = new LeagueRules
        {
            GroupId = groupId, LeagueId = leagueId, SeasonIndex = year,
            Formation = (int)settings.Formation, StartingMoney = settings.StartingMoney, DelayedDay = settings.DelayedDay,
            CancelledDay = settings.CancelledDay, PointForFirstGoal = settings.PointForFirstGoal, PointForNextGoal = settings.PointForNextGoal,
            PointForOwnGoal = settings.PointForOwnGoal, DifferencePointForOwnGoal = settings.DifferencePointForOwnGoal,
            PointInHome = settings.PointInHome, PointForVictory = settings.PointForVictory, PointForDefeat = settings.PointForDefeat,
            PointForDraw = settings.PointForDraw, PointForStrongDefense = settings.PointForStrongDefense,
            PointForStrongDefense4 = settings.PointForStrongDefense4, PointForStrongDefense5 = settings.PointForStrongDefense5,
            PointForGoodPeople = settings.PointForGoodPeople, PointForCleanSheet = settings.PointForCleanSheet,
            MoneyForGoal = settings.MoneyForGoal, MoneyForSufferedGoal = settings.MoneyForSufferedGoal,
            RandomAuction = settings.RandomAuction, RankWithValuePoints = settings.RankWithValuePoints, Market = (int)settings.Market,
            LiveFormationChanges = settings.LiveFormationChanges, AllowLiveModuleChange = settings.AllowLiveModuleChange,
            Votes = settings.Votes.Select(x => new LeagueVoteWeight { PlayerRole = (int)x.Key, Goal = x.Value.Goal,
                Penalty = x.Value.Penalty, SufferedGoal = x.Value.SufferedGoal, StoppedPenalty = x.Value.StoppedPenalty,
                WrongedPenalty = x.Value.WrongedPenalty, OwnGoal = x.Value.OwnGoal, Assist = x.Value.Assist,
                YellowCard = x.Value.YellowCard, RedCard = x.Value.RedCard, Injury = x.Value.Injury, ManOfTheMatch = x.Value.ManOfTheMatch }).ToList()
        };
        if (settings.OpeningCompetition is not { } opening) throw new CoreImportException("missing_opening_settings");
        row.OpeningCompetitionEnabled = opening.Enabled; row.OpeningCompetitionSerieADays = opening.SerieADays;
        row.OpeningPrizes = (opening.Prizes ?? []).Select(x => new LeagueOpeningPrize { Position = x.Position, Credits = x.Credits }).ToList();
        if (settings.Type is { } type)
        {
            row.HasTypeSettings = true; row.CalendarType = (int)type.CalendarType;
            var numbers = type.Numbers ?? throw new CoreImportException("missing_league_numbers");
            row.MaxPlayersInTeam = numbers.MaxPlayersInTeam; row.MaxGoalKeepersInTeam = numbers.MaxGoalKeepersInTeam;
            row.MaxDefendersInTeam = numbers.MaxDefendersInTeam; row.MaxMidfieldersInTeam = numbers.MaxMidfieldersInTeam;
            row.MaxForwardsInTeam = numbers.MaxForwardsInTeam; row.MaxGoalKeepersInBench = numbers.MaxGoalKeepersInBench;
            row.MaxDefendersInBench = numbers.MaxDefendersInBench; row.MaxMidfieldersInBench = numbers.MaxMidfieldersInBench;
            row.MaxForwardsInBench = numbers.MaxForwardsInBench;
            row.Rounds = (type.Rounds ?? []).Select((x, i) => new LeagueStageRule { Ordinal = i, Name = x.Name, Type = (int)x.Type,
                FromStart = x.FromStart, FromRankingStartTeam = x.FromRankingStartTeam, FromRankingEndTeam = x.FromRankingEndTeam }).ToList();
            row.CardLimits = (type.CardTrainer?.MaxCardsPerType ?? []).Select(x => new LeagueCardLimit { CardType = (int)x.Key, Maximum = x.Value }).ToList();
            if (type.FromPreviousYear is { } previous)
            {
                row.HasPreviousYearSettings = true; row.PreviousYearMaxTeamsPerLeague = previous.MaxTeamsPerLeague; row.PreviousYearRoundType = (int)previous.RoundType;
                row.PreviousLeagues = previous.LeaguesId.Distinct().Select(x => new PreviousLeagueQualification { SourceLeagueId = sourceLeague(x) }).ToList();
            }
        }
        return row;
    }
}
