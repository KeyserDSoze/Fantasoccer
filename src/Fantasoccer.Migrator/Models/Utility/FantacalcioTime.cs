using System;
using System.Threading.Tasks;

namespace FantacalcioV4
{
    public static class FantacalcioTime
    {
        public static int ActualYear
        {
            get
            {
                int actualYear = DateTime.UtcNow.Year - 2011;
                if (DateTime.UtcNow.Month < 8 || (DateTime.UtcNow.Month == 8 && DateTime.UtcNow.Day < 10))
                    actualYear--;
                return actualYear;
            }
        }
        public static string ActualLabelYear
            => LabelYear(DateTime.UtcNow);
        public static string LabelYear(DateTime date)
        {
            if (date.Month < 8 || (date.Month == 8 && date.Day < 10))
                return $"{(date.Year - 1)}/{date.Year}";
            return $"{date.Year}/{(date.Year + 1)}";
        }
        public static string LabelYear(int year) 
            => $"{2011 + year}/{(2012 + year)}";
        public static string LabelYearWithSmallerYear(int year)
           => $"{2011 + year}-{(12 + year)}";

        public static DateTime ItalyNow
        {
            get
            {
                DateTime italyNow = DateTime.UtcNow.AddHours(1);
                DateTime lastMarchSunday = new DateTime(italyNow.Year, 3, 31, 3, 0, 0, 0);
                while (lastMarchSunday.DayOfWeek != DayOfWeek.Sunday)
                    lastMarchSunday = lastMarchSunday.AddDays(-1);
                DateTime lastOctoberSunday = new DateTime(italyNow.Year, 10, 31, 3, 0, 0, 0);
                while (lastOctoberSunday.DayOfWeek != DayOfWeek.Sunday)
                    lastOctoberSunday = lastOctoberSunday.AddDays(-1);
                if (italyNow > lastMarchSunday && italyNow < lastOctoberSunday)
                    return italyNow.AddHours(1);
                return italyNow;
            }
        }
        public static int DayIdByDayYear(int yearId, int serieADay) => (yearId - 1) * 38 + serieADay;
        public static int SerieADayByDayId(int dayId) => dayId - (YearByDayId(dayId) - 1) * 38;
        public static int YearByDayId(int dayId) => dayId / 38 + (dayId % 38 == 0 ? 0 : 1);
        public static int DayByDayId(int dayId, LeagueSetting settings) => (SerieADayByDayId(dayId) - settings.DelayedDay) % 36;
        public static (int Start, int End) CalculateDayIdForSeason(int yearId)
            => (38 * yearId + 1, 38 * yearId + 38);
    }
}
