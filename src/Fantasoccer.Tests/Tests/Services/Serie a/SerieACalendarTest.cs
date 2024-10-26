using Fantasoccer.Business;

namespace Fantasoccer.Tests.Tests.Services
{
    public class SerieACalendarTest
    {
        private readonly ISerieAInformation _serieACalendar;

        public SerieACalendarTest(ISerieAInformation serieACalendar)
        {
            _serieACalendar = serieACalendar;
        }
        [Fact]
        public async Task CalendarAsync()
        {
            var calendar = await _serieACalendar.GetCalendarAsync();
            Assert.NotNull(calendar);
            Assert.Equal(38, calendar.Days.Count);
            foreach (var day in calendar.Days)
            {
                Assert.Equal(10, day.Games.Count);
                Assert.Equal(20, day.Games.SelectMany(x => new string[2] { x.Home.Name, x.Away.Name }).Distinct().Count());
            }
        }
    }
}
