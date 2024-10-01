using System.Text;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Engine.IngestionData
{
    public class PushNotificationJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<Group, string> _groupRepository;
        private readonly IRepository<LiveGroup, string> _liveGroupRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly IRepository<RealCalendar, int> _serieACalendar;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IRepository<PushNotificationSettings, string> _pushNotificationRepository;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _liveVoteRepository;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _notificationVoteRepository;
        public PushNotificationJob(IFantasoccerLogger logger,
            IRepository<Group, string> groupRepository,
            IRepository<LiveGroup, string> liveGroupRepository,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory,
            IRepository<Team, TeamDayKey> teamDayRepository,
            IRepository<RealCalendar, int> serieACalendar,
            IPushNotificationService pushNotificationService,
            IRepository<PushNotificationSettings, string> pushNotificationRepository)
        {
            _logger = logger;
            _groupRepository = groupRepository;
            _liveGroupRepository = liveGroupRepository;
            _teamDayRepository = teamDayRepository;
            _serieACalendar = serieACalendar;
            _pushNotificationService = pushNotificationService;
            _pushNotificationRepository = pushNotificationRepository;
            _liveVoteRepository = factory.Create("live")!;
            _notificationVoteRepository = factory.Create("notification")!;
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await _serieACalendar.GetAsync(actualYear).NoContext();
            if (realCalendar != null)
            {
                var liveDay = realCalendar.LiveDay ?? realCalendar.NextDay;
                if (liveDay != default)
                {
                    var yearDayKey = new YearDayKey
                    {
                        Year = actualYear,
                        Day = liveDay.SerieADay
                    };
                    var liveVotes = await _liveVoteRepository.GetAsync(yearDayKey);
                    if (liveVotes != null)
                    {
                        var notificationVotes = await _notificationVoteRepository.GetAsync(yearDayKey) ?? new VotedRealPlayerWrapper() { Players = new() };
                        var toNotifyPlayers = new List<VotedRealPlayer>();
                        var notificationsToSend = new List<NotificationToSend>();
                        foreach (var player in liveVotes.Players)
                        {
                            var currentPlayer = notificationVotes.Players.FirstOrDefault(x => x.Key == player.Key) ?? VotedRealPlayer.Empty;
                            var notificationToSend = GetNotification(player, currentPlayer);
                            if (notificationToSend != null)
                                notificationsToSend.Add(notificationToSend);
                        }
                        await _notificationVoteRepository.UpdateAsync(yearDayKey, liveVotes);
                        if (notificationsToSend.Any())
                        {
                            var notificationUsers = await _pushNotificationRepository.Where(x => x.MatchEvents).ToListAsync();
                            foreach (var group in await _liveGroupRepository.ToListAsync())
                            {
                                var teams = new List<Team>();
                                var groupKey = group.Key;
                                var groupValue = await _groupRepository.GetAsync(groupKey!);
                                foreach (var basket in groupValue!.Baskets)
                                {
                                    var currentYear = basket.Years.FirstOrDefault(x => x.Year == actualYear);
                                    if (currentYear != null)
                                    {
                                        foreach (var team in currentYear.Teams)
                                        {
                                            if (notificationUsers.Any(x => x.Key == team.Owner || team.AdditionalOwners?.Contains(x.Key!) == true))
                                            {
                                                var currentTeam = await _teamDayRepository.GetAsync(new TeamDayKey
                                                {
                                                    BasketId = basket.Id,
                                                    Day = liveDay.SerieADay,
                                                    Email = team.Owner,
                                                    Group = groupKey!,
                                                    Year = actualYear
                                                });
                                                if (currentTeam != null)
                                                    teams.Add(currentTeam);
                                            }
                                        }
                                    }
                                }
                                foreach (var notification in notificationsToSend)
                                {
                                    foreach (var team in teams.Where(x => x.Players.Any(t => t.Key == notification.PlayerKey)))
                                    {
                                        List<string> emails = team.AdditionalOwners != null ? [team.Owner, .. team.AdditionalOwners] : [team.Owner];
                                        foreach (var email in emails)
                                        {
                                            var notificationUser = notificationUsers.FirstOrDefault(x => x.Key == email);
                                            if (notificationUser != null && notificationUser.Value?.MatchEvents == true)
                                            {
                                                await _pushNotificationService.SendNotificationAsync(email,
                                                    notificationUser.Value,
                                                    new Notification
                                                    {
                                                        Body = notification.Body,
                                                        Title = "fantacalcio.xyz",
                                                        Data = new NotificationData
                                                        {
                                                            Uri = "https://fantacalcio.xyz/live"
                                                        }
                                                    });
                                            }
                                        }
                                    }
                                }
                            }
                            await _logger.LogAsync<PushNotificationJob>($"Notification parsate.").NoContext();
                        }
                    }
                }
            }
        }
        private NotificationToSend? GetNotification(VotedRealPlayer live, VotedRealPlayer current)
        {
            StringBuilder message = new();
            var needsNotification = false;
            Check(current.Vote.Goal, live.Vote.Goal, "Goal", true);
            Check(current.Vote.Assist, live.Vote.Assist, "Assist", true);
            Check(current.Vote.Penalty, live.Vote.Penalty, "Rigore", true);
            Check(current.Vote.OwnGoal, live.Vote.OwnGoal, "Autogol", true);
            Check(current.Vote.WrongedPenalty, live.Vote.WrongedPenalty, "Rigore sbagliato", true);
            Check(current.Vote.SufferedGoal, live.Vote.SufferedGoal, "Rigore subito", true);
            Check(current.Vote.StoppedPenalty, live.Vote.StoppedPenalty, "Rigore parato", true);
            Check(current.Vote.ManOfTheMatch ? 1 : 0, live.Vote.ManOfTheMatch ? 1 : 0, "Migliore in campo", true);
            Check(current.Vote.Status == Behaviour.YellowCard ? 1 : 0, live.Vote.Status == Behaviour.YellowCard ? 1 : 0, "Giallo", true);
            Check(current.Vote.Status == Behaviour.RedCard ? 1 : 0, live.Vote.Status == Behaviour.RedCard ? 1 : 0, "Rosso", true);
            void Check(float now, float previous, string action, bool isMaleAction)
            {
                if (message.Length > 0)
                    message.Append(", ");
                if (now > previous)
                {
                    message.Append(action);
                    needsNotification = true;
                }
                if (now < previous)
                {
                    message.Append($"{action} {(isMaleAction ? "annullato" : "annullata")}");
                    needsNotification = true;
                }
            }
            if (needsNotification)
            {
                return new NotificationToSend
                {
                    Body = $"{live.Name}: {message}",
                    PlayerKey = live.Key
                };
            }
            return null;
        }
        private sealed class NotificationToSend
        {
            public string PlayerKey { get; set; }
            public string Body { get; set; }
        }
        public Task OnException(Exception exception)
        => _logger.LogErrorAsync<LiveVotesJob>(exception);
    }
}
