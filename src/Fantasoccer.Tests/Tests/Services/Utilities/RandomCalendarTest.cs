using System.Population.Random;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests
{
    public class RandomCalendarTest
    {
        private readonly IFactory<ILeagueCalculator> _leagueCalculatorFactory;
        private readonly IPopulation<AnnualTeam> _populationService;

        public RandomCalendarTest(IFactory<ILeagueCalculator> leagueCalculatorFactory, IPopulation<AnnualTeam> populationService)
        {
            _leagueCalculatorFactory = leagueCalculatorFactory;
            _populationService = populationService;
        }
        [Theory]
        [InlineData(10, LeagueType.League, 36, 1)]
        [InlineData(16, LeagueType.Cup, 12, 4)]
        public void Create(int numberOfTeams, LeagueType leagueType, int numberOfDays, int rounds)
        {
            var leagueCalculator = _leagueCalculatorFactory.Create(leagueType.ToString())!;
            var calendar = leagueCalculator.GetRandomCalendar(2, new AnnualLeague
            {
                Year = FantacalcioTime.ActualYear,
                Settings = LeagueSetting.Default,
            }, _populationService.Populate(numberOfTeams));

            Assert.NotNull(calendar);
            Assert.Equal(calendar.Rounds.Count, rounds);
            Assert.Equal(calendar.Rounds.First().Value.Count, numberOfDays);
        }
    }
}
