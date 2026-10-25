namespace Fantasoccer
{
    public static class FantacalcioTime
    {
        public static int ActualYear => SeasonYearAt(DateTime.UtcNow);

        // Shared by normal application logic and the MCP context. The season
        // changes on August 10 UTC, not on January 1 or the first Serie A match.
        public static int SeasonYearAt(DateTime utcDate)
            => utcDate.Year - 2011 - (utcDate.Month < 8 || (utcDate.Month == 8 && utcDate.Day < 10) ? 1 : 0);
        public static string ActualLabelYear
            => LabelYear(DateTime.UtcNow);
        public static string LabelYear(DateTime date, char splitter = '/', bool lastYearMin = false)
        {
            if (date.Month < 8 || (date.Month == 8 && date.Day < 10))
                return $"{(date.Year - 1)}{splitter}{(!lastYearMin ? date.Year.ToString() : date.Year.ToString().Substring(2))}";
            return $"{date.Year}{splitter}{(!lastYearMin ? (date.Year + 1).ToString() : (date.Year + 1).ToString().Substring(2))}";
        }
        public static string LabelYear(int year)
            => $"{2011 + year}/{(2012 + year)}";
        public static string LabelYearWithSmallerYear(int year)
           => $"{2011 + year}-{(12 + year)}";

        public static DateTime ItalyNow => ToItalyNow(DateTime.UtcNow);
        public static DateTime ToItalyNow(DateTime date)
        {
            var italyNow = date.AddHours(1);
            var lastMarchSunday = new DateTime(italyNow.Year, 3, 31, 3, 0, 0, 0);
            while (lastMarchSunday.DayOfWeek != DayOfWeek.Sunday)
                lastMarchSunday = lastMarchSunday.AddDays(-1);
            var lastOctoberSunday = new DateTime(italyNow.Year, 10, 31, 3, 0, 0, 0);
            while (lastOctoberSunday.DayOfWeek != DayOfWeek.Sunday)
                lastOctoberSunday = lastOctoberSunday.AddDays(-1);
            if (italyNow > lastMarchSunday && italyNow < lastOctoberSunday)
                return italyNow.AddHours(1);
            return italyNow;
        }
        public static int DayIdByDayYear(int yearId, int serieADay) => (yearId - 1) * 38 + serieADay;
        public static int SerieADayByDayId(int dayId) => dayId - (YearByDayId(dayId) - 1) * 38;
        public static int YearByDayId(int dayId) => dayId / 38 + (dayId % 38 == 0 ? 0 : 1);
        public static int DayByDayId(int dayId, LeagueSetting settings) => (SerieADayByDayId(dayId) - settings.DelayedDay) % 36;
        public static (int Start, int End) CalculateDayIdForSeason(int yearId)
            => (38 * yearId + 1, 38 * yearId + 38);
    }
}
