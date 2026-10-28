namespace Fantasoccer.Application.Core.Operations;

public static class SqlJobSchedule
{
    public static readonly string[] Names = ["calendar", "catalogue", "live", "official", "statistics", "chances", "formations", "results", "postponed-results", "market-expiry", "hall", "images", "push-retention", "cache-cleanup"];
    public static DateTimeOffset Next(string name, DateTimeOffset now)
    {
        if (!Names.Contains(name, StringComparer.Ordinal)) throw new ArgumentException("Unknown SQL job.", nameof(name));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        var start = TimeZoneInfo.ConvertTime(now, zone).Date;
        for (var offset = 0; offset <= 370; offset++)
        {
            var date = start.AddDays(offset);
            if (name == "hall" && date.DayOfWeek != DayOfWeek.Tuesday || name == "images" && (date.Day != 1 || date.Month is not (3 or 9))) continue;
            var candidates = new List<DateTimeOffset>();
            for (var hour = 0; hour < 24; hour++)
            for (var minute = 0; minute < 60; minute++)
            {
                var matches = name switch
                {
                    "calendar" => hour >= 12 && minute % 5 == 0,
                    "catalogue" => hour == 5 && minute == 0,
                    "live" or "formations" => true,
                    "official" => hour is >= 3 and <= 10 && minute % 15 == 0,
                    "statistics" => hour is >= 3 and <= 10 && minute is 2 or 17 or 32 or 47,
                    "results" => hour is >= 3 and <= 10 && minute is 5 or 20 or 35 or 50,
                    "postponed-results" => minute % 5 == 0,
                    "chances" => hour is 6 or 10 or 12 or 14 or 18 or 20 && minute == 0,
                    "market-expiry" => hour == 2 && minute == 0,
                    "push-retention" => hour == 4 && minute == 10,
                    "cache-cleanup" => hour == 4 && minute == 20,
                    "hall" or "images" => hour == 3 && minute == 0,
                    _ => false
                };
                if (!matches) continue;
                var wall = DateTime.SpecifyKind(date.AddHours(hour).AddMinutes(minute), DateTimeKind.Unspecified);
                if (zone.IsInvalidTime(wall)) continue;
                if (zone.IsAmbiguousTime(wall))
                    candidates.AddRange(zone.GetAmbiguousTimeOffsets(wall).Select(offset => new DateTimeOffset(wall, offset).ToUniversalTime()));
                else candidates.Add(new(TimeZoneInfo.ConvertTimeToUtc(wall, zone)));
            }
            var next = candidates.Where(x => x > now).Order().FirstOrDefault();
            if (next != default) return next;
        }
        throw new InvalidOperationException("No scheduled time within one year.");
    }
}
