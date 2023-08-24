namespace Fantasoccer.Business.Api.Jobs
{
    internal class NewCalendar
    {
        private readonly GroupApi Group;
        public NewCalendar(GroupApi group)
            => Group = group;
        public async Task CreateNewYearAndCalendarAsync(string groupName, int year, bool forceDeletion)
        {
            if (!await Group.AreWeInNewYear(groupName, year))
            {
                var substituted = new Dictionary<string, string> {
                    { "valerio86", "SirJem"},
                    { "lucamar88", "perugini" },
                    { "Andrea", "Champion" },
                    { "e.lisei@vetrya.com", "Corvine" },
                };
                await Group.RecalculateWinningsAsync(groupName).NoContext();
                await Group.SubscribePeopleToNewYearAsync(groupName, year, substituted, forceDeletion).NoContext();
                await Group.CreateNewYearAsync(groupName, year, substituted).NoContext();
                await Group.CreateNewCalendarsAsync(groupName, year).NoContext();
            }
        }
    }
}
