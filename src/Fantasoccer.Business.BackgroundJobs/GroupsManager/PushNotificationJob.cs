using System.ComponentModel;
using System.Text;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Engine.GroupsManager
{
    public class PushNotificationJob : IBackgroundJob
    {
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<Group, string> _groupRepository;
        private readonly IRepository<LiveGroup, string> _liveGroupRepository;
        private readonly IRepository<Team, TeamDayKey> _teamDayRepository;
        private readonly IRepository<RealCalendar, int> _serieACalendar;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IRepository<Calendar, LeagueKey> _calendarRepository;
        private readonly IRepository<PushNotificationSettings, string> _pushNotificationRepository;
        private readonly IRepository<PushNotificationSent, YearDayKey> _pushNotificationSentRepository;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _liveVoteRepository;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _notificationVoteRepository;
        public PushNotificationJob(IFantasoccerLogger logger,
            IRepository<Group, string> groupRepository,
            IRepository<LiveGroup, string> liveGroupRepository,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory,
            IRepository<Team, TeamDayKey> teamDayRepository,
            IRepository<RealCalendar, int> serieACalendar,
            IPushNotificationService pushNotificationService,
            IRepository<Calendar, LeagueKey> calendarRepository,
            IRepository<PushNotificationSettings, string> pushNotificationRepository,
            IRepository<PushNotificationSent, YearDayKey> pushNotificationSentRepository)
        {
            _logger = logger;
            _groupRepository = groupRepository;
            _liveGroupRepository = liveGroupRepository;
            _teamDayRepository = teamDayRepository;
            _serieACalendar = serieACalendar;
            _pushNotificationService = pushNotificationService;
            _calendarRepository = calendarRepository;
            _pushNotificationRepository = pushNotificationRepository;
            _pushNotificationSentRepository = pushNotificationSentRepository;
            _liveVoteRepository = factory.Create("live")!;
            _notificationVoteRepository = factory.Create("notification")!;
        }
        public async Task ActionToDoAsync(CancellationToken cancellationToken)
        {
            var actualYear = FantacalcioTime.ActualYear;
            var realCalendar = await _serieACalendar.GetAsync(actualYear).NoContext();
            if (realCalendar != null)
            {
                var liveDay = realCalendar.LiveDay;
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
                            var notificationUsers = await _pushNotificationRepository.Where(x => x.MatchEvents || x.OpponentMatchEvents).ToListAsync();
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
                                var currentOpponents = new Dictionary<string, List<OpponentNotification>>();
                                foreach (var league in group.Value!.Leagues)
                                {
                                    var currentCalendar = await _calendarRepository.GetAsync(new LeagueKey
                                    {
                                        Group = groupKey!,
                                        League = league.Id,
                                        Year = actualYear
                                    });
                                    var currentDay = currentCalendar?.Rounds.SelectMany(x => x.Value).FirstOrDefault(x => x.SerieADay == liveDay.SerieADay);
                                    if (currentDay != null)
                                    {
                                        foreach (var game in currentDay.Games)
                                        {
                                            AddOpponent(game.HomeOwner, game.AwayOwner);
                                            AddOpponent(game.AwayOwner, game.HomeOwner);

                                            void AddOpponent(string owner, string opponent)
                                            {
                                                var notificationUser = notificationUsers.FirstOrDefault(x => x.Key == opponent);
                                                if (notificationUser != null && notificationUser.Value?.OpponentMatchEvents == true)
                                                {
                                                    if (!currentOpponents.ContainsKey(owner))
                                                        currentOpponents.Add(owner, []);
                                                    currentOpponents[owner].Add(new OpponentNotification
                                                    {
                                                        League = league.Name,
                                                        Owner = owner,
                                                        Opponent = opponent
                                                    });
                                                }
                                            }
                                        }
                                    }
                                }
                                foreach (var notification in notificationsToSend)
                                {
                                    foreach (var team in teams.Where(x => x.Players.Any(t => t.Key == notification.PlayerKey)))
                                    {
                                        await SendNotificationAsync(team, notification, null, null, x => x?.Value?.MatchEvents == true);
                                        if (currentOpponents.ContainsKey(team.Owner))
                                        {
                                            var opponents = currentOpponents[team.Owner];
                                            foreach (var opponent in opponents)
                                            {
                                                var opponentTeam = teams.FirstOrDefault(x => x.Owner == opponent.Opponent);
                                                if (opponentTeam != null)
                                                    await SendNotificationAsync(opponentTeam, notification, $"[{opponent.League}]", $"({team.Name.Trim()})", x => x?.Value?.OpponentMatchEvents == true);
                                            }
                                        }
                                    }
                                }
                            }
                            async ValueTask SendNotificationAsync(Team currentTeam, NotificationToSend notification, string? prefix, string? postfix, Func<Entity<PushNotificationSettings, string>?, bool> predicate)
                            {
                                List<string> emails = currentTeam.AdditionalOwners != null ? [currentTeam.Owner, .. currentTeam.AdditionalOwners] : [currentTeam.Owner];
                                foreach (var email in emails)
                                {
                                    var notificationUser = notificationUsers.FirstOrDefault(x => x.Key == email);
                                    if (notificationUser?.Value != null && predicate(notificationUser))
                                    {
                                        var body = !string.IsNullOrWhiteSpace(prefix) ? $"{prefix} {notification.Body}" : notification.Body;
                                        if (!string.IsNullOrWhiteSpace(postfix))
                                            body = $"{body} {postfix}";
                                        await _pushNotificationService.SendNotificationAsync(email,
                                            notificationUser.Value,
                                            new Notification
                                            {
                                                Body = body,
                                                Title = "fantacalcio.xyz",
                                                Data = new NotificationData
                                                {
                                                    Uri = "https://fantacalcio.xyz/live"
                                                }
                                            });
                                    }
                                }
                            }
                            await _logger.LogAsync<PushNotificationJob>($"Notification parsate.").NoContext();
                        }
                    }
                }
                if (realCalendar.NextDay?.Games.Any(t => t.Date != null && Math.Abs(FantacalcioTime.ItalyNow.Subtract(t.Date.Value).TotalHours) < 8) == true)
                {
                    var yearlyDayKey = new YearDayKey
                    {
                        Day = realCalendar.NextDay.SerieADay,
                        Year = actualYear
                    };
                    if (!await _pushNotificationSentRepository.ExistAsync(yearlyDayKey))
                    {
                        var notificationUsers = await _pushNotificationRepository.Where(x => x.DeploymentReminder).ToListAsync();
                        foreach (var user in notificationUsers)
                        {
                            await _pushNotificationService.SendNotificationAsync(user.Key!, user.Value!, new Notification
                            {
                                Body = "Ricorda di schierare la formazione!",
                                Title = "fantacalcio.xyz",
                                Data = new NotificationData
                                {
                                    Uri = "https://fantacalcio.xyz/edit"
                                }
                            });
                        }
                        await _pushNotificationSentRepository.InsertAsync(yearlyDayKey, new PushNotificationSent
                        {
                            DeploymentReminder = true,
                            EndDay = false
                        });
                    }
                }
                //todo add end day to this notification
            }
        }
        private sealed class OpponentNotification
        {
            public string League { get; set; }
            public string Owner { get; set; }
            public string Opponent { get; set; }
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
            Check(current.Vote.SufferedGoal, live.Vote.SufferedGoal, "Goal subito", true);
            Check(current.Vote.StoppedPenalty, live.Vote.StoppedPenalty, "Rigore parato", true);
            Check(current.Vote.ManOfTheMatch ? 1 : 0, live.Vote.ManOfTheMatch ? 1 : 0, "Migliore in campo", true);
            Check(current.Vote.Status == Behaviour.YellowCard ? 1 : 0, live.Vote.Status == Behaviour.YellowCard ? 1 : 0, "Giallo", true);
            Check(current.Vote.Status == Behaviour.RedCard ? 1 : 0, live.Vote.Status == Behaviour.RedCard ? 1 : 0, "Rosso", true);
            void Check(float previous, float now, string action, bool isMaleAction)
            {
                if (now != previous && message.Length > 0)
                {
                    message.Append(", ");
                }
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
        => _logger.LogErrorAsync<PushNotificationJob>(exception);
    }
}
